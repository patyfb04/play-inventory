using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Play.Common.Entities;

namespace Play.Inventory.Service.Entities
{
    public class InventoryItem : IEntity
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public Guid CatalogItemId { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public int Quantity { get; set; }

        public DateTimeOffset AcquiredDate {get; set;}

        public HashSet<Guid> MessagesIds { get; set; } = new ();
    }
}
