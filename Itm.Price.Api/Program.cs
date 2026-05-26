using Itm.Price.Api.Dtos; // Asegúrate de que este namespace coincida con donde creaste tu DTO
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Manual de uso (Swagger UI):
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Base de datos simulada de Precios (en memoria) Mock
var priceDb = new List<PriceDto>
{
    new(1, 999.99m, "USD"), // Precio para el producto con ID 1
    new(2, 49.99m, "USD"),  // Precio para el producto con ID 2
    new(3, 199.99m, "USD")  // Precio para el producto con ID 3
};

// El Endpoint para consultar el precio de un producto (Ventanilla de Atención)

app.MapGet("/api/prices/{id}", (int id) =>
{
    // Buscamos el precio en la "base de datos" simulada
    var price = priceDb.FirstOrDefault(p => p.ProductId == id);
    // Retornamos 200 OK con el precio si existe, o 404 NotFound si no
    return price is not null ? Results.Ok(price) : Results.NotFound();
})
.WithName("GetPriceById") // Nombre claro y específico para la API de Precios
.WithOpenApi();

// Endpoint de salud en formato JSON compatible con HealthChecks UI
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
