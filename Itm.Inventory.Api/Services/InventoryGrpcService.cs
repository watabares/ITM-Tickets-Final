using Grpc.Core;
using Itm.Inventory.Grpc;

namespace Itm.Inventory.Api.Services;

/// <summary>
/// Implementación del servicio gRPC de Inventario.
/// Permite comunicación binaria de alta velocidad con Order.Api.
/// </summary>
public class InventoryGrpcService : InventoryGrpc.InventoryGrpcBase
{
    private readonly ILogger<InventoryGrpcService> _logger;

    // Base de datos en memoria compartida (en producción sería un DbContext inyectado)
    private static readonly List<InventoryItem> _inventoryDb = new()
    {
        new(1, 50, "BOLETA-MEDELLIN-VIP"),
        new(2, 100, "BOLETA-MEDELLIN-GENERAL"),
        new(3, 50, "BOLETA-MADRID-VIP"),
        new(4, 100, "BOLETA-MADRID-GENERAL"),
        new(5, 200, "BOLETA-COMBO-2MUNDOS")
    };

    public InventoryGrpcService(ILogger<InventoryGrpcService> logger)
    {
        _logger = logger;
    }

    public override Task<GetStockReply> GetStock(GetStockRequest request, ServerCallContext context)
    {
        var item = _inventoryDb.FirstOrDefault(i => i.ProductId == request.ProductId);

        if (item is null)
        {
            return Task.FromResult(new GetStockReply
            {
                ProductId = request.ProductId,
                Stock = 0,
                Sku = "",
                Found = false
            });
        }

        _logger.LogInformation("[gRPC] GetStock ProductId={ProductId} Stock={Stock}", item.ProductId, item.Stock);

        return Task.FromResult(new GetStockReply
        {
            ProductId = item.ProductId,
            Stock = item.Stock,
            Sku = item.Sku,
            Found = true
        });
    }

    public override Task<ReduceStockReply> ReduceStock(ReduceStockRequest request, ServerCallContext context)
    {
        var item = _inventoryDb.FirstOrDefault(i => i.ProductId == request.ProductId);

        if (item is null)
        {
            return Task.FromResult(new ReduceStockReply
            {
                Success = false,
                Message = "Producto no encontrado en inventario",
                NewStock = 0
            });
        }

        if (item.Stock < request.Quantity)
        {
            return Task.FromResult(new ReduceStockReply
            {
                Success = false,
                Message = $"Stock insuficiente. Disponible: {item.Stock}",
                NewStock = item.Stock
            });
        }

        // Reservar stock (thread-safe con lock en producción)
        var index = _inventoryDb.IndexOf(item);
        _inventoryDb[index] = item with { Stock = item.Stock - request.Quantity };

        var correlationId = context.RequestHeaders.GetValue("x-correlation-id") ?? "N/A";
        _logger.LogInformation("[gRPC] ReduceStock ProductId={ProductId} Qty={Qty} NewStock={NewStock} CorrelationId={CorrId}",
            request.ProductId, request.Quantity, _inventoryDb[index].Stock, correlationId);

        return Task.FromResult(new ReduceStockReply
        {
            Success = true,
            Message = "Stock reservado exitosamente via gRPC",
            NewStock = _inventoryDb[index].Stock
        });
    }

    public override Task<ReduceStockReply> ReleaseStock(ReduceStockRequest request, ServerCallContext context)
    {
        var item = _inventoryDb.FirstOrDefault(i => i.ProductId == request.ProductId);

        if (item is null)
        {
            return Task.FromResult(new ReduceStockReply
            {
                Success = false,
                Message = "Producto no encontrado para compensación",
                NewStock = 0
            });
        }

        var index = _inventoryDb.IndexOf(item);
        _inventoryDb[index] = item with { Stock = item.Stock + request.Quantity };

        _logger.LogWarning("[gRPC] SAGA COMPENSACIÓN: ReleaseStock ProductId={ProductId} Qty={Qty} NewStock={NewStock}",
            request.ProductId, request.Quantity, _inventoryDb[index].Stock);

        return Task.FromResult(new ReduceStockReply
        {
            Success = true,
            Message = "Stock liberado (compensación SAGA) via gRPC",
            NewStock = _inventoryDb[index].Stock
        });
    }
}

// Record interno para el inventario en memoria
internal record InventoryItem(int ProductId, int Stock, string Sku);
