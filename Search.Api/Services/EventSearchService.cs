using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Search.Api.Services;

/// <summary>
/// Servicio de búsqueda inteligente que combina:
/// - Elasticsearch: búsqueda por texto (fuzzy, full-text)
/// - Qdrant: búsqueda semántica por vectores (IA, "vibe")
/// </summary>
public class EventSearchService
{
    private readonly ElasticsearchClient _esClient;
    private readonly QdrantClient _qdrantClient;
    private readonly ILogger<EventSearchService> _logger;
    private const string IndexName = "events";
    private const string CollectionName = "events_vectors";
    private const int VectorSize = 64; // Dimensión del embedding simplificado

    // Catálogo de eventos del "Festival de los Dos Mundos"
    private static readonly List<EventDocument> _events = new()
    {
        new("1", "Festival de los Dos Mundos - VIP Medellín", "Concierto exclusivo con artistas internacionales en la sede de Medellín. Experiencia premium con acceso backstage.", "Medellín", "concierto", 150000m, new[] { "música", "vip", "premium", "backstage", "exclusivo" }),
        new("2", "Festival de los Dos Mundos - General Medellín", "Entrada general al festival en Medellín. Disfruta de todos los escenarios principales.", "Medellín", "concierto", 80000m, new[] { "música", "general", "festival", "escenarios" }),
        new("3", "Festival de los Dos Mundos - VIP Madrid", "Experiencia VIP en Madrid con cena gourmet y meet & greet con artistas.", "Madrid", "concierto", 85m, new[] { "música", "vip", "gourmet", "meet&greet", "europa" }),
        new("4", "Festival de los Dos Mundos - General Madrid", "Entrada general al festival en Madrid. Zona libre con food trucks y actividades.", "Madrid", "concierto", 45m, new[] { "música", "general", "food trucks", "actividades" }),
        new("5", "Combo Dos Mundos - Medellín + Madrid", "Paquete especial: asiste a ambas sedes del festival. Incluye boleta VIP en ambas ciudades.", "Global", "paquete", 200000m, new[] { "combo", "ambas sedes", "viaje", "experiencia completa", "descuento" }),
        new("6", "After Party Electrónica - Medellín", "Fiesta electrónica post-festival con DJs internacionales. Solo mayores de 18.", "Medellín", "fiesta", 60000m, new[] { "electrónica", "dj", "fiesta", "noche", "after" }),
        new("7", "Taller de Producción Musical", "Workshop de 4 horas con productores del festival. Aprende técnicas de mezcla y mastering.", "Medellín", "taller", 120000m, new[] { "educación", "producción", "música", "workshop", "aprender" }),
        new("8", "Experiencia Gastronómica Fusión", "Cena maridaje que fusiona sabores colombianos y españoles. Edición limitada.", "Madrid", "gastronomía", 95m, new[] { "comida", "fusión", "gourmet", "colombia", "españa", "cena" }),
        new("9", "Zona Familiar - Medellín", "Actividades para toda la familia: juegos, música infantil y talleres creativos.", "Medellín", "familiar", 40000m, new[] { "niños", "familia", "juegos", "infantil", "diversión" }),
        new("10", "Noche de Salsa y Flamenco", "Espectáculo único que fusiona la salsa colombiana con el flamenco español.", "Madrid", "espectáculo", 55m, new[] { "salsa", "flamenco", "baile", "fusión cultural", "espectáculo" })
    };

    public EventSearchService(ElasticsearchClient esClient, QdrantClient qdrantClient, ILogger<EventSearchService> logger)
    {
        _esClient = esClient;
        _qdrantClient = qdrantClient;
        _logger = logger;
    }

    /// <summary>
    /// Búsqueda por texto usando Elasticsearch (fuzzy matching)
    /// </summary>
    public async Task<List<SearchResult>> SearchByTextAsync(string query)
    {
        try
        {
            var response = await _esClient.SearchAsync<EventDocument>(s => s
                .Index(IndexName)
                .Query(q => q
                    .MultiMatch(mm => mm
                        .Query(query)
                        .Fields(new[] { "name^3", "description^2", "category", "city", "tags" })
                        .Fuzziness(new Fuzziness("AUTO"))
                        .Type(TextQueryType.BestFields)
                    )
                )
                .Size(10)
            );

            if (!response.IsValidResponse || response.Documents == null)
            {
                _logger.LogWarning("Elasticsearch no retornó resultados válidos para: {Query}", query);
                return FallbackTextSearch(query);
            }

            return response.Documents.Select(d => new SearchResult(
                d.Id, d.Name, d.Description, d.City, d.Category, d.Price, 0.9f
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Elasticsearch, usando fallback local");
            return FallbackTextSearch(query);
        }
    }

    /// <summary>
    /// Búsqueda semántica usando Qdrant (por "vibe" / intención del usuario)
    /// El usuario puede buscar "algo divertido para la familia" y encontrar eventos relevantes
    /// sin necesidad de usar las palabras exactas del título.
    /// </summary>
    public async Task<List<SearchResult>> SearchByVibeAsync(string query)
    {
        try
        {
            // Generar embedding del query (en producción usaríamos OpenAI/Sentence-Transformers)
            var queryVector = GenerateSimpleEmbedding(query);

            var searchResult = await _qdrantClient.SearchAsync(
                CollectionName,
                queryVector,
                limit: 5
            );

            return searchResult.Select(r =>
            {
                var payload = r.Payload;
                return new SearchResult(
                    payload.GetValueOrDefault("id")?.StringValue ?? "",
                    payload.GetValueOrDefault("name")?.StringValue ?? "",
                    payload.GetValueOrDefault("description")?.StringValue ?? "",
                    payload.GetValueOrDefault("city")?.StringValue ?? "",
                    payload.GetValueOrDefault("category")?.StringValue ?? "",
                    decimal.TryParse(payload.GetValueOrDefault("price")?.StringValue, out var p) ? p : 0,
                    r.Score
                );
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Qdrant, usando fallback semántico local");
            return FallbackSemanticSearch(query);
        }
    }

    /// <summary>
    /// Indexa los eventos en Elasticsearch y Qdrant
    /// </summary>
    public async Task SeedDataAsync()
    {
        // Seed Elasticsearch
        try
        {
            foreach (var evt in _events)
            {
                await _esClient.IndexAsync(evt, IndexName);
            }
            _logger.LogInformation("Elasticsearch: {Count} eventos indexados", _events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexando en Elasticsearch");
        }

        // Seed Qdrant
        try
        {
            // Crear colección si no existe
            try
            {
                await _qdrantClient.CreateCollectionAsync(CollectionName, new VectorParams
                {
                    Size = VectorSize,
                    Distance = Distance.Cosine
                });
            }
            catch { /* Collection may already exist */ }

            // Insertar puntos con vectores
            var points = _events.Select((evt, idx) => new PointStruct
            {
                Id = new PointId { Num = (ulong)(idx + 1) },
                Vectors = GenerateSimpleEmbedding($"{evt.Name} {evt.Description} {string.Join(" ", evt.Tags)}"),
                Payload =
                {
                    ["id"] = evt.Id,
                    ["name"] = evt.Name,
                    ["description"] = evt.Description,
                    ["city"] = evt.City,
                    ["category"] = evt.Category,
                    ["price"] = evt.Price.ToString()
                }
            }).ToList();

            await _qdrantClient.UpsertAsync(CollectionName, points);
            _logger.LogInformation("Qdrant: {Count} vectores insertados", points.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error insertando en Qdrant");
        }
    }

    /// <summary>
    /// Genera un embedding simplificado basado en bag-of-words.
    /// En producción se usaría un modelo como sentence-transformers o OpenAI embeddings.
    /// </summary>
    private float[] GenerateSimpleEmbedding(string text)
    {
        var vector = new float[VectorSize];
        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Vocabulario semántico agrupado por conceptos
        var concepts = new Dictionary<int, string[]>
        {
            [0] = new[] { "música", "concierto", "festival", "dj", "artista", "canción" },
            [1] = new[] { "vip", "premium", "exclusivo", "backstage", "lujo" },
            [2] = new[] { "general", "libre", "acceso", "entrada" },
            [3] = new[] { "medellín", "colombia", "colombiano", "salsa" },
            [4] = new[] { "madrid", "españa", "español", "flamenco", "europa" },
            [5] = new[] { "familia", "niños", "infantil", "juegos", "diversión" },
            [6] = new[] { "fiesta", "noche", "electrónica", "after", "party" },
            [7] = new[] { "comida", "gastronomía", "gourmet", "cena", "fusión" },
            [8] = new[] { "educación", "taller", "workshop", "aprender", "producción" },
            [9] = new[] { "baile", "espectáculo", "show", "cultural" },
            [10] = new[] { "combo", "paquete", "descuento", "ambas", "viaje" },
            [11] = new[] { "económico", "barato", "accesible", "precio" },
            [12] = new[] { "caro", "costoso", "premium", "alto" },
            [13] = new[] { "divertido", "emocionante", "increíble", "genial" },
            [14] = new[] { "tranquilo", "relajado", "chill", "suave" },
            [15] = new[] { "intenso", "energía", "adrenalina", "fuerte" }
        };

        foreach (var word in words)
        {
            // Hash simple para distribuir en el vector
            var hash = Math.Abs(word.GetHashCode()) % VectorSize;
            vector[hash] += 1.0f;

            // Activar dimensiones semánticas
            foreach (var (dim, keywords) in concepts)
            {
                if (keywords.Any(k => word.Contains(k) || k.Contains(word)))
                {
                    vector[dim] += 2.0f; // Peso mayor para matches semánticos
                }
            }
        }

        // Normalizar L2
        var magnitude = (float)Math.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
                vector[i] /= magnitude;
        }

        return vector;
    }

    // Fallback cuando Elasticsearch no está disponible
    private List<SearchResult> FallbackTextSearch(string query)
    {
        var q = query.ToLowerInvariant();
        return _events
            .Where(e => e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                       e.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                       e.City.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                       e.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Select(e => new SearchResult(e.Id, e.Name, e.Description, e.City, e.Category, e.Price, 0.7f))
            .ToList();
    }

    // Fallback semántico local
    private List<SearchResult> FallbackSemanticSearch(string query)
    {
        var queryEmbedding = GenerateSimpleEmbedding(query);

        return _events
            .Select(e =>
            {
                var eventEmbedding = GenerateSimpleEmbedding($"{e.Name} {e.Description} {string.Join(" ", e.Tags)}");
                var similarity = CosineSimilarity(queryEmbedding, eventEmbedding);
                return new SearchResult(e.Id, e.Name, e.Description, e.City, e.Category, e.Price, similarity);
            })
            .OrderByDescending(r => r.Score)
            .Take(5)
            .ToList();
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        var denom = (float)(Math.Sqrt(magA) * Math.Sqrt(magB));
        return denom > 0 ? dot / denom : 0;
    }
}

// Modelos
public record EventDocument(string Id, string Name, string Description, string City, string Category, decimal Price, string[] Tags);
public record SearchResult(string Id, string Name, string Description, string City, string Category, decimal Price, float Score);


