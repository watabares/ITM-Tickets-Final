using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------
// 1. REGISTRO DE SERVICIOS (Inyección de Dependencias)
// ---------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// ARQUITECTURA: Registramos los HttpClientFactory
builder.Services.AddHttpClient("InventoryClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5293");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient("PriceClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5012");
});

// REGISTRO DE ARQUITECTURA: CACHÉ DISTRIBUIDA (NIVEL 5)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "ItmTickets_";
});

var app = builder.Build();

// ---------------------------------------------------------
// 2. PIPELINE HTTP
// ---------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();

// ---------------------------------------------------------
// 3. ENDPOINTS
// ---------------------------------------------------------

// --- ENDPOINT 1: Consulta simple (Solo inventario con propagación de Token) ---
app.MapGet("/api/products/{id}/check-stock", async (int id, IHttpClientFactory clientFactory, HttpContext httpContext) =>
{
    var client = clientFactory.CreateClient("InventoryClient");
    try
    {
        if (httpContext.Request.Headers.TryGetValue("Authorization", out var auth))
        {
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("Authorization", (IEnumerable<string>)auth);
        }

        var response = await client.GetAsync($"/api/inventory/{id}");
        if (response.IsSuccessStatusCode)
        {
            var inventoryData = await response.Content.ReadFromJsonAsync<InventoryResponse>();
            return Results.Ok(new { ProductId = id, StockInfo = inventoryData });
        }
        return Results.Problem($"El inventario respondió con error: {response.StatusCode}");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error de conexión: {ex.Message}");
    }
})
.WithName("CheckProductStock")
.WithOpenApi();


// --- ENDPOINT 2: EL CORAZÓN DE LA CLASE 10 (Paralelismo / BFF) ---
app.MapGet("/api/products/{id}/summary", async (int id, IHttpClientFactory factory) =>
{
    var invClient = factory.CreateClient("InventoryClient");
    var priceClient = factory.CreateClient("PriceClient");

    try
    {
        var inventoryTask = invClient.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{id}");
        var priceTask = priceClient.GetFromJsonAsync<PriceResponse>($"/api/prices/{id}");

        await Task.WhenAll(inventoryTask, priceTask);

        return Results.Ok(new
        {
            Id = id,
            Product = "Laptop Gamer Pro",
            StockDetails = inventoryTask.Result,
            FinancialDetails = priceTask.Result,
            CalculatedAt = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error en el ecosistema distribuido: {ex.Message}");
    }
})
.WithName("GetProductSummary")
.WithOpenApi();


// --- ENDPOINT 3: EL CORAZÓN DE LA CLASE 11 (PATRÓN CACHE-ASIDE) ---
app.MapGet("/api/products/{id}", async (int id, IHttpClientFactory factory, IDistributedCache cache, ILogger<Program> logger) =>
{
    string cacheKey = $"Product_summary_{id}";

    // 1. Intentar obtener de Redis (Cache Hit?)
    var cachedData = await cache.GetStringAsync(cacheKey);
    if (!string.IsNullOrEmpty(cachedData))
    {
        logger.LogInformation(" Cache hit: Devolviendo datos desde Redis.");
        var resultFromCache = JsonSerializer.Deserialize<object>(cachedData);
        return Results.Ok(resultFromCache);
    }

    logger.LogWarning(" Cache miss: Yendo a la base de datos (Price.Api)");

    // 2. Si no hay caché, vamos al microservicio
    var client = factory.CreateClient("PriceClient");

    try
    {
        var priceResponse = await client.GetFromJsonAsync<PriceResponse>($"/api/prices/{id}");

        var finalProduct = new
        {
            Id = id,
            Name = "Entrada VIP Concierto",
            PriceData = priceResponse,
        };

        // 3. Guardar en caché con TTL de 60 segundos
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        };

        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(finalProduct), cacheOptions);

        return Results.Ok(finalProduct);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error: {ex.Message}");
    }
})
.WithName("GetProductWithCache")
.WithOpenApi();


// --- MONITOREO Y EJECUCIÓN ---
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();


// ---------------------------------------------------------
// 4. MODELOS LOCALES (DTOS)
// ---------------------------------------------------------
internal record InventoryResponse(int ProductId, int Stock, string Sku);
internal record PriceResponse(int ProductId, decimal Amount, string Currency);