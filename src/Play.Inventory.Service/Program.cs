using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Play.Common.Identity;
using Play.Common.MassTransit;
using Play.Common.Messaging;
using Play.Common.Repositories;
using Play.Common.Settings;
using Play.Inventory.Service.Clients;
using Play.Inventory.Service.Entities;
using Play.Inventory.Service.Policies;
using Play.Inventory.Service.Services;
using Polly;
using Polly.Timeout;
using Serilog;


var builder = WebApplication.CreateBuilder(args);
const string AllowedOriginSetting = "AllowedOrigin";

BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
BsonSerializer.RegisterSerializer(typeof(Guid), new GuidSerializer(GuidRepresentation.Standard));
BsonSerializer.RegisterSerializer(typeof(Guid?), new NullableSerializer<Guid>(new GuidSerializer(GuidRepresentation.Standard)));

Log.Logger = new LoggerConfiguration()
             .WriteTo.Console()
             //.WriteTo.File("logs/invent_log.txt")
             .MinimumLevel.Information()
             .CreateLogger();

builder.Host.UseSerilog();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true);

builder.Services.Configure<CosmosDbSettings>(
    builder.Configuration.GetSection(nameof(CosmosDbSettings)));

builder.Services.Configure<ServiceBusSettings>(
    builder.Configuration.GetSection(nameof(ServiceBusSettings)));

builder.Services.Configure<ServiceSettings>(
    builder.Configuration.GetSection(nameof(ServiceSettings)));

builder.Services.Configure<MassTransitSettings>(
    builder.Configuration.GetSection(nameof(MassTransitSettings)));

builder.Services.Configure<ClientServicesSettings>(
    builder.Configuration.GetSection("ClientServicesSettings"));

builder.Services.AddSingleton<InventoryCatalogSyncService>();

builder.Services
    .AddCosmosDb()
    .AddCosmosRepository<InventoryItem>("inventoryitems")
    .AddCosmosRepository<CatalogItem>("catalogitems")
    .AddMassTransitWithAzureServiceBus()
    .AddJwtBearerAuthentication();

// register token provider and handler for outgoing client calls
builder.Services.AddSingleton<ITokenProvider, ClientCredentialsTokenProvider>();
builder.Services.AddTransient<TokenDelegatingHandler>();

builder.Services.AddSingleton<IAuthorizationHandler, InventoryReadOrAdminHandler>();

builder.Services.AddAuthorization(options =>
{
    // Define a policy for read-only access to the inventory from the other services
    options.AddPolicy("InventoryReadOrAdmin", policy =>
          policy.AddRequirements(new InventoryReadOrAdminRequirement()));
});

var clientServicesSettings = builder.Configuration
    .GetSection(nameof(ClientServicesSettings))
    .Get<ClientServicesSettings>();

var catalogService = clientServicesSettings.ClientServices
    .FirstOrDefault(s => s.ServiceName.Equals("CatalogService", StringComparison.OrdinalIgnoreCase));

AddCatalogClient(builder.Services, catalogService?.ServiceUrl);

builder.Services.AddControllers();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Inventory.Service", Version = "v1" });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Sync the catalog items with inventory items on startup
// using (var scope = app.Services.CreateScope())
// {
//     var sync = scope.ServiceProvider.GetRequiredService<InventoryCatalogSyncService>();
//     await sync.RunAsync();
// }

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Inventory.Service v1"));

    app.UseCors(config => {
        config.WithOrigins(builder.Configuration[AllowedOriginSetting])
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
}
else
{
    app.UseHttpsRedirection();
}



app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

 static void AddCatalogClient(IServiceCollection serviceCollection, string catalogUrl)
{
    Random jitterer = new Random();

    serviceCollection.AddHttpClient<CatalogClient>(client =>
    {
        client.BaseAddress = new Uri(catalogUrl);
    })
    .AddHttpMessageHandler<TokenDelegatingHandler>()
    .AddTransientHttpErrorPolicy(policy => policy.Or<TimeoutRejectedException>().WaitAndRetryAsync(
        5, // 5 attempts
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)), // exponentinal backoff
        onRetry: (outcome, timespan, retryAttemp) =>
        {
            // Use Serilog static logger instead of building a service provider here
            Log.Logger.ForContext("SourceContext", typeof(CatalogClient).FullName)
                .Warning("Delaying for {Delay} seconds, then making retry {Retry}", timespan, retryAttemp);
        }))
    .AddTransientHttpErrorPolicy(policy => policy.Or<TimeoutRejectedException>().CircuitBreakerAsync(
              3,
              TimeSpan.FromSeconds(15),
              onBreak: (outcome, timespan) => {
                  Log.Logger.ForContext("SourceContext", typeof(CatalogClient).FullName)
                      .Warning("Opening the Circuit for {Seconds} seconds...", timespan.TotalSeconds);
              },
              onReset: () =>
              {
                  Log.Logger.ForContext("SourceContext", typeof(CatalogClient).FullName)
                      .Warning("Closing the Circuit...");
              }
          ))
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(1));
}

app.MapHealthChecks("/health");

app.Run();
