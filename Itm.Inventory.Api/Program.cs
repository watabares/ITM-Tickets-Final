using Itm.Inventory.Api.Dtos;
using Itm.Inventory.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using HealthChecks.UI.Client;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Servicios básicos (Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. gRPC Server - Comunicación binaria de alta velocidad
builder.Services.AddGrpc();

// 3. Bloque de seguridad JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// 4. Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// 5. Pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// 6. Mapear servicio gRPC
app.MapGrpcService<InventoryGrpcService>();

// 7. "Base de datos" en memoria (para endpoints REST legacy)
var inventoryDb = new List<InventoryItemDto>
{
    new(1, 50, "BOLETA-MEDELLIN-VIP"),
    new(2, 100, "BOLETA-MEDELLIN-GENERAL"),
    new(3, 50, "BOLETA-MADRID-VIP"),
    new(4, 100, "BOLETA-MADRID-GENERAL"),
    new(5, 200, "BOLETA-COMBO-2MUNDOS")
};

// 8. Endpoints REST (mantienen compatibilidad con Product.Api y MAUI)
app.MapGet("/api/inventory/{id}", (int id, HttpContext httpContext, ILogger<Program> logger) =>
{
    var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "SIN-ID";
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        logger.LogInformation("Consultando inventario para producto {ProductId}", id);
        var item = inventoryDb.FirstOrDefault(p => p.ProductId == id);
        return item is not null ? Results.Ok(item) : Results.NotFound();
    }
})
.WithName("GetInventory")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/api/inventory/reduce", (ReduceStockDto request, HttpContext httpContext, ILogger<Program> logger) =>
{
    var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "SIN-ID";
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        var item = inventoryDb.FirstOrDefault(p => p.ProductId == request.ProductId);
        if (item is null)
            return Results.NotFound(new { Error = "Producto no existe en bodega" });

        if (item.Stock < request.Quantity)
            return Results.BadRequest(new { Error = "Stock insuficiente", CurrentStock = item.Stock });

        var index = inventoryDb.IndexOf(item);
        inventoryDb[index] = item with { Stock = item.Stock - request.Quantity };

        logger.LogInformation("[REST] Stock reducido ProductId={ProductId} NewStock={NewStock}", request.ProductId, inventoryDb[index].Stock);
        return Results.Ok(new { Message = "Stock reservado", NewStock = inventoryDb[index].Stock });
    }
})
.WithName("ReduceStock")
.WithOpenApi();

app.MapPost("/api/inventory/release", (ReduceStockDto request, HttpContext httpContext, ILogger<Program> logger) =>
{
    var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "SIN-ID";
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        var item = inventoryDb.FirstOrDefault(p => p.ProductId == request.ProductId);
        if (item is null) return Results.NotFound();

        var index = inventoryDb.IndexOf(item);
        inventoryDb[index] = item with { Stock = item.Stock + request.Quantity };

        logger.LogWarning("[REST] SAGA Compensación: Stock liberado ProductId={ProductId} NewStock={NewStock}", request.ProductId, inventoryDb[index].Stock);
        return Results.Ok(new { Message = "Stock liberado (compensación SAGA)", CurrentStock = inventoryDb[index].Stock });
    }
})
.WithName("ReleaseStock")
.WithOpenApi();

// 9. Health Check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();



