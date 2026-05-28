using System.Text;
using System.Text.Json;
using Itm.Store.Mobile.Services;

namespace Itm.Store.Mobile;

public partial class MainPage : ContentPage
{
    private HttpClient? _httpClient;
    private HttpClient? _productClient;
    private NotificationService? _notificationService;

    public MainPage()
    {
        InitializeComponent();
    }

    private void EnsureClients()
    {
        if (_httpClient != null) return;
        var handler = new AuthHandler { InnerHandler = new HttpClientHandler() };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5110/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var handler2 = new AuthHandler { InnerHandler = new HttpClientHandler() };
        _productClient = new HttpClient(handler2)
        {
            BaseAddress = new Uri("http://localhost:5110/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        try
        {
            string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJJdG1JZGVudGl0eVNlcnZlciIsImF1ZCI6Ikl0bVN0b3JlQXBpcyIsImVtYWlsIjoiYWRtaW5AaXRtLmVkdS5jbyIsInJvbGUiOiJBZG1pbmlzdHJhZG9yIiwiZXhwIjoxODExMzgxMjU4fQ.IlyaPSYpWmm6BOGTy5OPdROMcaowOo7rImd8A2HUcX8";
            await SecureStorage.Default.SetAsync("jwt_token", token);
            LoginStatusLabel.Text = "JWT guardado en SecureStorage (cifrado DPAPI)";
            LoginStatusLabel.TextColor = Colors.Green;
        }
        catch (Exception ex)
        {
            LoginStatusLabel.Text = ex.Message;
            LoginStatusLabel.TextColor = Colors.Red;
        }
    }

    private async void OnConsultarProductoClicked(object sender, EventArgs e)
    {
        try
        {
            EnsureClients();
            ProductResultLabel.Text = "Consultando...";
            var response = await _productClient!.GetAsync("api/products/1");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var product = JsonSerializer.Deserialize<JsonElement>(json);
                ProductResultLabel.Text = "Stock: " + product.GetProperty("stock") + " | SKU: " + product.GetProperty("sku");
                ProductResultLabel.TextColor = Colors.Green;
            }
            else
            {
                ProductResultLabel.Text = response.StatusCode.ToString();
                ProductResultLabel.TextColor = Colors.Orange;
            }
        }
        catch (Exception ex)
        {
            ProductResultLabel.Text = ex.Message;
            ProductResultLabel.TextColor = Colors.Red;
        }
    }

    private async void OnComprarBoletaClicked(object sender, EventArgs e)
    {
        try
        {
            EnsureClients();
            BtnComprar.IsEnabled = false;
            CompraLoading.IsRunning = true;
            CompraLoading.IsVisible = true;
            CompraResultFrame.IsVisible = false;

            string sede = SedePicker.SelectedItem?.ToString() ?? "Medellin";
            int cantidad = int.Parse(CantidadPicker.SelectedItem?.ToString() ?? "1");

            var orderRequest = new { productId = 1, quantity = cantidad, sede = sede, userEmail = "usuario@itm.edu.co" };
            var json = JsonSerializer.Serialize(orderRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient!.PostAsync("api/orders", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

            CompraResultFrame.IsVisible = true;

            if (response.IsSuccessStatusCode)
            {
                var orderId = result.GetProperty("orderId").GetString() ?? "";
                var latency = result.GetProperty("inventoryLatencyMs").GetInt64();
                var newStock = result.GetProperty("newStock").GetInt32();

                CompraResultFrame.BackgroundColor = Color.FromArgb("#E8F5E9");
                CompraStatusLabel.Text = "Boleta reservada exitosamente!";
                CompraStatusLabel.TextColor = Colors.Green;
                CompraDetailLabel.Text = "Orden: " + orderId + "\nSede: " + sede + "\ngRPC: " + latency + "ms\nStock restante: " + newStock;
            }
            else
            {
                CompraResultFrame.BackgroundColor = Color.FromArgb("#FFF3E0");
                CompraStatusLabel.Text = "SAGA Compensacion - Stock devuelto";
                CompraStatusLabel.TextColor = Colors.Orange;
                CompraDetailLabel.Text = responseBody;
            }
        }
        catch (Exception ex)
        {
            CompraResultFrame.IsVisible = true;
            CompraResultFrame.BackgroundColor = Color.FromArgb("#FFEBEE");
            CompraStatusLabel.Text = "Error de conexion";
            CompraStatusLabel.TextColor = Colors.Red;
            CompraDetailLabel.Text = ex.Message;
        }
        finally
        {
            BtnComprar.IsEnabled = true;
            CompraLoading.IsRunning = false;
            CompraLoading.IsVisible = false;
        }
    }

    private async void OnConnectSignalRClicked(object sender, EventArgs e)
    {
        try
        {
            BtnSignalR.IsEnabled = false;
            SignalRStatusLabel.Text = "Conectando al Hub...";

            _notificationService ??= new NotificationService();
            _notificationService.OnTicketReady += (s, message) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    NotificationFrame.IsVisible = true;
                    NotificationLabel.Text = DateTime.Now.ToString("HH:mm:ss") + " - " + message;
                });
            };

            await _notificationService.ConnectAsync();
            SignalRStatusLabel.Text = "Conectado al Hub de Notificaciones";
            SignalRStatusLabel.TextColor = Colors.Green;
            BtnSignalR.Text = "SignalR Conectado";
            BtnSignalR.BackgroundColor = Colors.Gray;
        }
        catch (Exception ex)
        {
            SignalRStatusLabel.Text = ex.Message;
            SignalRStatusLabel.TextColor = Colors.Red;
            BtnSignalR.IsEnabled = true;
        }
    }
}
