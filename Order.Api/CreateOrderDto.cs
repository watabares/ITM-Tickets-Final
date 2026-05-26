namespace Order.Api;

public record CreateOrderDto(int ProductId, int Quantity, string Sede = "Medellin", string? UserEmail = null);
