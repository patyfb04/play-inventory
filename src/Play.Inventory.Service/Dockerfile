FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG configuration=Release
WORKDIR /src

# Copy nuget.config and packages folder
COPY nuget.config ./
COPY packages ./packages

COPY ["Play.Inventory/src/Play.Inventory.Service/Play.Inventory.Service.csproj", "Play.Inventory/src/Play.Inventory.Service/"]
COPY ["Play.Inventory/src/Play.Inventory.Contracts/Play.Inventory.Contracts.csproj", "Play.Inventory/src/Play.Inventory.Contracts/"]
RUN dotnet restore "Play.Inventory/src/Play.Inventory.Service/Play.Inventory.Service.csproj"
COPY . .
WORKDIR "/src/Play.Inventory/src/Play.Inventory.Service"
RUN dotnet build "Play.Inventory.Service.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Play.Inventory.Service.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Play.Inventory.Service.dll"]