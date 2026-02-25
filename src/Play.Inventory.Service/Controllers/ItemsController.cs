using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Play.Common.Repositories;
using Play.Inventory.Contracts;
using Play.Inventory.Dtos;
using Play.Inventory.Service.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Play.Inventory.Service.Controllers
{
    [ApiController]
    [Route("items")]
    public class ItemsController : ControllerBase
    {
        private readonly IRepository<InventoryItem> _inventoryRepository;
        private readonly IRepository<CatalogItem> _catalogItemsRepository;
        private const string AdminRole = "Admin";
        private readonly IPublishEndpoint _publishedEndpoint;

        public ItemsController(
            IRepository<InventoryItem> itemsRepository, 
            IRepository<CatalogItem> catalogItemsRepository,
             IPublishEndpoint publishedEndpoint)
        {
            _inventoryRepository = itemsRepository;
            _catalogItemsRepository = catalogItemsRepository;
            _publishedEndpoint = publishedEndpoint;
        }


        [HttpGet("sync")]
        [Authorize("InventoryReadOrAdmin")]
        public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetAllAsync()
        {
            try { 
                var inventoryItemEntities = await _inventoryRepository.GetAllAsync();

                var catalogItemIdsList = inventoryItemEntities
                    .Select(c => c.CatalogItemId)
                    .Distinct()
                    .ToList();

                var catalogItemEntities = await _catalogItemsRepository
                    .GetAllAsync(item => catalogItemIdsList.Contains(item.Id));

                var catalogDict = catalogItemEntities.ToDictionary(c => c.Id);

                var inventoryItemsDtos = inventoryItemEntities
                    .Select(inventoryItem =>
                    {
                        if (!catalogDict.TryGetValue(inventoryItem.CatalogItemId, out var catalogItem))
                            return null;

                        return inventoryItem.AsDto(catalogItem.Name, catalogItem.Description);
                    })
                    .Where(dto => dto != null);

                return Ok(inventoryItemsDtos); 
            }
            catch (Exception ex)
            {
                Console.WriteLine("Items Controller EXCEPTION:");
                Console.WriteLine(ex.ToString());
                throw;
            }

        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetAsync(Guid userId) 
        {

            if (userId == Guid.Empty)
            {
                return BadRequest();
            }

            var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (Guid.Parse(currentUserId) != userId)
            {
                if (!User.IsInRole(AdminRole))
                {
                    return Forbid();
                }
            }

            var inventoryItemEntities = await _inventoryRepository.GetAllAsync(item => item.UserId.Equals(userId));
            var catalogItemIds = inventoryItemEntities.Select(c=> c.CatalogItemId);
            var catalogItemEntities = await _catalogItemsRepository.GetAllAsync(item => catalogItemIds.Contains(item.Id));

            var inventoryItemsDtos = inventoryItemEntities.Select(inventoryItem =>
            {
                var catalogItem = catalogItemEntities.SingleOrDefault(catalogItem => catalogItem.Id == inventoryItem.CatalogItemId);
                if (catalogItem == null)
                {
                    return null; 
                }
                return inventoryItem.AsDto(catalogItem.Name, catalogItem.Description);
            }).Where(dto => dto != null);

            return Ok(inventoryItemsDtos);
        }

        [HttpPost]
        [Authorize(Roles = AdminRole)]
        public async Task<ActionResult> PostAsync(GrantItemsDto grantItemsDto)
        {
            var inventoryItem = await _inventoryRepository.GetAsync(item => item.UserId == grantItemsDto.UserId && item.CatalogItemId == grantItemsDto.CatalogItemId);
            if (inventoryItem == null) {
                inventoryItem = new InventoryItem()
                {
                    CatalogItemId = grantItemsDto.CatalogItemId,
                    Quantity = grantItemsDto.Quantity,
                    UserId = grantItemsDto.UserId,
                    AcquiredDate = DateTimeOffset.UtcNow
                };

                await _inventoryRepository.CreateAsync(inventoryItem);
            }
            else
            {
                inventoryItem.Quantity += grantItemsDto.Quantity;
                await _inventoryRepository.UpdateAsync(inventoryItem);
            }

            await _publishedEndpoint.Publish(new InventoryItemUpdated(
                                inventoryItem.UserId,
                                inventoryItem.CatalogItemId,
                                inventoryItem.Quantity));

            return Ok();
        }
    }
}
