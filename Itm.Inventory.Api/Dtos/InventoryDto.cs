namespace Itm.Inventory.Api.Dtos;

public record InventoryItemDto(int ProductId, int Stock, string Sku);
public record ReduceStockDto(int ProductId, int Quantity);
