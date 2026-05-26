namespace Itm.Price.Api.Dtos;

    // Por qué: Separamos el concepto de "Precio" del concepto de "Producto"
    // Currency es vital: No es lo mismo 1000 USD que 1000 COP.
    public record PriceDto(int ProductId, decimal Amount, string Currency);
    