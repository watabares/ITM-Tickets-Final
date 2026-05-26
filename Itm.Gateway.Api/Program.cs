using Itm.Gateway.Api.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 2. JWT Authentication (Seguridad Perimetral - Nivel 5)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? "ITM-Super-Secret-Key-For-JWT-Class-2026-Nivel5");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "ItmIdentityServer",
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"] ?? "ItmStoreApis",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
    });

builder.Services.AddAuthorization();

// 3. Rate Limiting (Control de Multitudes - Nivel 5)
// Protege contra ataques de fuerza bruta y controla 50,000 usuarios concurrentes
builder.Services.AddRateLimiter(options =>
{
    // Política global: Fixed Window - 100 requests por minuto por IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    // Política específica para compras: Sliding Window - 5 compras por minuto por IP
    options.AddSlidingWindowLimiter("purchase", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 4;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    // Respuesta cuando se excede el límite
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":\"Demasiadas solicitudes. Intente de nuevo en un momento.\",\"retryAfter\":60}",
            cancellationToken: token);
    };
});

// 4. Health Checks UI
builder.Services.AddHealthChecksUI(setup =>
{
    setup.AddHealthCheckEndpoint("Inventory API", "http://localhost:5293/health");
    setup.AddHealthCheckEndpoint("Orders API", "http://localhost:5027/health");
    setup.AddHealthCheckEndpoint("Prices API", "http://localhost:5012/health");
    setup.AddHealthCheckEndpoint("Notifications API", "http://localhost:5089/health");
    setup.AddHealthCheckEndpoint("Product API", "http://localhost:5298/health");
    setup.AddHealthCheckEndpoint("Search API", "http://localhost:5100/health");
})
.AddInMemoryStorage();

var app = builder.Build();

// 5. Pipeline de middlewares (orden importa)
app.UseRateLimiter();                          // Rate Limiting primero
app.UseMiddleware<CorrelationIdMiddleware>();   // Correlation ID
app.UseAuthentication();                       // JWT Validation
app.UseAuthorization();                        // Authorization

// 6. YARP Reverse Proxy
app.MapReverseProxy();

// 7. Health Checks UI
app.MapHealthChecksUI(options =>
{
    options.UIPath = "/monitor";
});

app.Run();
