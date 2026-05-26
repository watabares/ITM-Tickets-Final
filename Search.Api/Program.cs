using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using Search.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Servicios básicos
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// 2. Elasticsearch Client (búsqueda por texto)
var esUrl = builder.Configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
var esSettings = new ElasticsearchClientSettings(new Uri(esUrl))
    .DefaultIndex("events");
builder.Services.AddSingleton(new ElasticsearchClient(esSettings));

// 3. Qdrant Client (búsqueda semántica por vectores)
var qdrantUrl = builder.Configuration["Qdrant:Url"] ?? "http://localhost:6333";
var qdrantUri = new Uri(qdrantUrl);
builder.Services.AddSingleton(new QdrantClient(qdrantUri.Host, qdrantUri.Port));

// 4. Servicio de búsqueda
builder.Services.AddSingleton<EventSearchService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ============================================================
// ENDPOINTS DE BÚSQUEDA
// ============================================================

// Búsqueda por texto (Elasticsearch) - búsqueda exacta y fuzzy
app.MapGet("/api/search/text", async (string q, EventSearchService searchService) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { Error = "El parámetro 'q' es requerido" });

    var results = await searchService.SearchByTextAsync(q);
    return Results.Ok(new
    {
        Query = q,
        Engine = "Elasticsearch",
        Count = results.Count,
        Results = results
    });
})
.WithName("SearchByText")
.WithOpenApi();

// Búsqueda semántica (Qdrant) - búsqueda por "vibe" / intención
app.MapGet("/api/search/semantic", async (string q, EventSearchService searchService) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { Error = "El parámetro 'q' es requerido" });

    var results = await searchService.SearchByVibeAsync(q);
    return Results.Ok(new
    {
        Query = q,
        Engine = "Qdrant (Semantic/AI)",
        Count = results.Count,
        Results = results
    });
})
.WithName("SearchBySemantic")
.WithOpenApi();

// Búsqueda híbrida (combina ambos motores)
app.MapGet("/api/search/hybrid", async (string q, EventSearchService searchService) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { Error = "El parámetro 'q' es requerido" });

    var textResults = await searchService.SearchByTextAsync(q);
    var semanticResults = await searchService.SearchByVibeAsync(q);

    return Results.Ok(new
    {
        Query = q,
        TextResults = new { Engine = "Elasticsearch", Count = textResults.Count, Items = textResults },
        SemanticResults = new { Engine = "Qdrant", Count = semanticResults.Count, Items = semanticResults }
    });
})
.WithName("HybridSearch")
.WithOpenApi();

// Seed: Indexar eventos de ejemplo
app.MapPost("/api/search/seed", async (EventSearchService searchService) =>
{
    await searchService.SeedDataAsync();
    return Results.Ok(new { Message = "Datos de eventos indexados en Elasticsearch y Qdrant" });
})
.WithName("SeedSearchData")
.WithOpenApi();

// Health Check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
