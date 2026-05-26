using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Itm.Store.Mobile.Services;

// DelegatingHandler: Es un "Peaje" por donde pasan TODAS las peticiones HTTP, y es el lugar ideal para agregar lógica de autenticación, manejo de tokens, logging, etc. Antes de que la petición llegue al servidor o después de que la respuesta regrese al cliente, dependiendo de dónde coloquemos nuestra lógica dentro del método SendAsync.
public class AuthHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        //1. Buscamos el token en la bóveda de seguridad del celulalr (Secure Storage)
        var token = await SecureStorage.GetAsync("jwt_token");

        //2. Si el  usuario está autenticado (hay token), se lo agregamos a la petición HTTP
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        //3. Continuamos el viaje de la petición HTTP hacia el Gateway, o el servidor, o el API, etc.
        return await base.SendAsync(request, cancellationToken);
    }
    }