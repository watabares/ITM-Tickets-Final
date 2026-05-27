using Order.Api;
using System.Net.Http.Json;
using System.Diagnostics;
using MassTransit;
using Itm.Shared.Events;
using Itm.Inventory.Grpc;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using Itm.Order.Api.Handlers;

var builder = WebApplication.CreateBuilder(args);

// Servicios básicos y Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

// CONFIGURACIÓN DEL PRODUCTOR (MassTransit + RabbitMQ)
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("amqps://miqffttk:1pscfTN1wGyzJHwe8BTEFMyocp9U-bEp@moose.rmq.cloudamqp.com/miqffttk");
    });
});

// Cliente gRPC hacia Inventory.Api (comunicación binaria - Nivel 5)
builder.Services.AddGrpcClient<InventoryGrpc.InventoryGrpcClient>(options =>
{
    options.Address = new Uri(builder.Configuration["InventoryGrpcUrl"] ?? "http://localhost:5294");
});

// Cliente HTTP legacy (fallback)
builder.Services.AddHttpClient("InventoryClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5293");
})
.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<CloudAmqpHealthCheck>("CloudAMQP-Broker");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ============================================================
// ENDPOINT PRINCIPAL: Crear orden con SAGA + gRPC (Nivel 5)
// ============================================================
app.MapPost(
    "/api/orders",
    async (CreateOrderDto order, InventoryGrpc.InventoryGrpcClient grpcClient,
           IPublishEndpoint publisher, HttpContext httpContext, ILogger<Program> logger) =>
    {
        var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N")[..12];
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        logger.LogInformation("=== INICIO ORDEN === ProductId={ProductId} Qty={Qty} Sede={Sede}",
            order.ProductId, order.Quantity, order.Sede);

        // Medir latencia gRPC
        var sw = Stopwatch.StartNew();

        // Paso 1: Reservar stock via gRPC (comunicación binaria, <10ms latencia local)
        var reduceReply = await grpcClient.ReduceStockAsync(new ReduceStockRequest
        {
            ProductId = order.ProductId,
            Quantity = order.Quantity
        });

        sw.Stop();
        logger.LogInformation("[gRPC] ReduceStock respondió en {ElapsedMs}ms - Success={Success}",
            sw.ElapsedMilliseconds, reduceReply.Success);

        if (!reduceReply.Success)
        {
            return Results.BadRequest(new
            {
                Error = reduceReply.Message,
                CorrelationId = correlationId,
                Protocol = "gRPC",
                LatencyMs = sw.ElapsedMilliseconds
            });
        }

        try
        {
            // Paso 2: Simular procesamiento de pago
            bool paymentSuccess = new Random().Next(0, 10) > 2; // 70% éxito

            if (!paymentSuccess)
            {
                throw new InvalidOperationException("Fondos Insuficientes en la Tarjeta");
            }

            // Paso 3: Orden exitosa - publicar evento a RabbitMQ
            var newOrderId = Guid.NewGuid();
            decimal finalTotal = order.Sede == "Madrid" ? 85.00m : 150000m; // EUR vs COP

            var orderEvent = new OrderCreatedEvent(newOrderId, order.ProductId, order.UserEmail ?? "usuario@itm.edu.co", finalTotal);
            await publisher.Publish(orderEvent);

            logger.LogInformation("=== ORDEN COMPLETADA === OrderId={OrderId} Total={Total} gRPC_Latency={LatencyMs}ms",
                newOrderId, finalTotal, sw.ElapsedMilliseconds);

            return Results.Ok(new
            {
                Status = "Boleta reservada exitosamente",
                OrderId = newOrderId,
                Sede = order.Sede,
                CorrelationId = correlationId,
                Protocol = "gRPC",
                InventoryLatencyMs = sw.ElapsedMilliseconds,
                NewStock = reduceReply.NewStock
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el pago. Iniciando compensación SAGA via gRPC...");

            // COMPENSACIÓN SAGA via gRPC
            var compensateSw = Stopwatch.StartNew();
            var releaseReply = await grpcClient.ReleaseStockAsync(new ReduceStockRequest
            {
                ProductId = order.ProductId,
                Quantity = order.Quantity
            });
            compensateSw.Stop();

            logger.LogWarning("SAGA Compensación completada en {ElapsedMs}ms - Success={Success}",
                compensateSw.ElapsedMilliseconds, releaseReply.Success);

            if (releaseReply.Success)
            {
                return Results.Problem(new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Title = "Pago fallido - Stock devuelto",
                    Detail = $"El pago falló ({ex.Message}). Stock compensado via gRPC en {compensateSw.ElapsedMilliseconds}ms.",
                    Extensions = { ["correlationId"] = correlationId, ["protocol"] = "gRPC" }
                });
            }

            logger.LogCritical("FALLO CRÍTICO: Compensación SAGA falló. Datos inconsistentes.");
            return Results.Problem("Error crítico del sistema. Contacte soporte.");
        }
    });

// Endpoint de salud
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();

// Health check CloudAMQP
internal sealed class CloudAmqpHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private const string AmqpUrl = "amqps://miqffttk:1pscfTN1wGyzJHwe8BTEFMyocp9U-bEp@moose.rmq.cloudamqp.com/miqffttk";

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = new Uri(AmqpUrl);
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : 5671, cancellationToken);
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("CloudAMQP reachable");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("CloudAMQP unreachable", ex);
        }
    }
}





