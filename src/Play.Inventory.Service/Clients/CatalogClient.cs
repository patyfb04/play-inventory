using Play.Inventory.Dtos;

namespace Play.Inventory.Service.Clients
{
    public class CatalogClient
    {
        private readonly HttpClient _httpClient;

        public CatalogClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyCollection<CatalogItemDto>> GetCatalogItemsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/items");

                if (!response.IsSuccessStatusCode)
                {
                    // Log and return empty list
                    Console.WriteLine($"[CatalogClient] Non-success status: {response.StatusCode}");
                    return Array.Empty<CatalogItemDto>();
                }

                var items = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<CatalogItemDto>>();

                return items ?? Array.Empty<CatalogItemDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CatalogClient] Error calling Catalog: {ex.Message}");
                return Array.Empty<CatalogItemDto>();
            }
        }
    }
}
