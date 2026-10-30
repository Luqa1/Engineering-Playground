namespace EngineeringPlayground.Cqrs.Api.Responses;

public sealed record InventoryDashboardResponse(
    int TotalProducts,
    int OutOfStockCount,
    int LowStockCount,
    decimal TotalInventoryValue,
    IReadOnlyList<InventoryAttentionItemResponse> ProductsNeedingAttention);

public sealed record InventoryAttentionItemResponse(
    Guid Id,
    string Name,
    int AvailableQuantity,
    string StockStatus,
    decimal InventoryValue);
