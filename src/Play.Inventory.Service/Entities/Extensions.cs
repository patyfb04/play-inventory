using Play.Inventory.Dtos;

namespace Play.Inventory.Service.Entities
{
    public static class Extensions
    {
        public static InventoryItemDto AsDto(this InventoryItem item, string name, string description) {
            return new InventoryItemDto(item.CatalogItemId, name, description, item.Quantity,  item.AcquiredDate);
        }
    }
}
