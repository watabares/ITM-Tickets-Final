using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Itm.Order.Api.Handlers
{
    // Interceptor que propaga el X-Correlation-ID en las llamadas HTTP salientes
    public class CorrelationIdDelegatingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string CorrelationIdHeader = "X-Correlation-ID";

        public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // 1. Extraemos el pasaporte del contexto HTTP actual (el que viene desde el Gateway)
            var correlationId = _httpContextAccessor.HttpContext?.Request.Headers[CorrelationIdHeader].FirstOrDefault();

            // 2. Si existe, lo inyectamos en la petición HTTP saliente hacia el otro microservicio
            if (!string.IsNullOrEmpty(correlationId) && !request.Headers.Contains(CorrelationIdHeader))
            {
                request.Headers.Add(CorrelationIdHeader, correlationId);
            }

            // 3. Dejamos que la petición continúe su viaje
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
