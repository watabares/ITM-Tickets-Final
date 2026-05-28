using System.Net.Http.Headers;

namespace Itm.Store.Mobile.Services;

/// <summary>
/// Interceptor HTTP que adjunta automáticamente el JWT de SecureStorage
/// a cada petición saliente como Bearer token.
/// Se ejecuta en CADA request HTTP sin intervención del usuario.
/// </summary>
public class AuthHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. Leer el token de la bóveda criptográfica del dispositivo
        var token = await SecureStorage.Default.GetAsync("jwt_token");

        // 2. Adjuntarlo como Bearer token en el header Authorization
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        // 3. Continuar con la petición (ahora lleva el JWT)
        return await base.SendAsync(request, cancellationToken);
    }
}
