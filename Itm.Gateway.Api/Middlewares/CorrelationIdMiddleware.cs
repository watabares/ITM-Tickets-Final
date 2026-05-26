using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Itm.Gateway.Api.Middlewares
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private const string CorrelationIdHeader = "X-Correlation-ID";

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Si la petición no trae un ID desde el cliente, generamos uno nuevo en el Gateway
            if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
                context.Request.Headers.Append(CorrelationIdHeader, correlationId);
            }

            // Siempre devolvemos el ID en la respuesta para que el cliente pueda rastrear su transacción
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.Append(CorrelationIdHeader, correlationId);
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
