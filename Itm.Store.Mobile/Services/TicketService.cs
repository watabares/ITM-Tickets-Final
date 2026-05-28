using System.Text;
using System.Text.Json;

namespace Itm.Store.Mobile.Services;

/// <summary>
/// Servicio que encapsula la lógica de compra de boletas.
/// Llama al Order.Api via Gateway (YARP) que ejecuta:
///   1. gRPC → Inventory.Api (reservar stock)
///   2. Simula pago
///   3. MassTransit → RabbitMQ → Notification.Api (evento)
///   4. Si falla → SAGA compensación via gRPC (devolver stock)
/// </summary>
public class TicketService
{
    private readonly HttpClient _httpClient;

    public TicketService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<OrderResult> ComprarBoletaAsync(string sede, int cantidad, string email)
    {
        // Adjuntar JWT
        var token = await SecureStorage.Default.GetAsync("jwt_token");
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            productId = 1,
            quantity = cantidad,
            sede = sede,
            userEmail = email
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("api/orders", content);
        var body = await response.Content.ReadAsStringAsync();

        return new OrderResult
        {
            Success = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            ResponseBody = body
        };
    }
}

public class OrderResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
}
