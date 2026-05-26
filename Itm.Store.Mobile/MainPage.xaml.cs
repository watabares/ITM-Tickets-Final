using System.Net.Http;
using Microsoft.AspNetCore.SignalR.Client; // Cliente de SignalR para conectarnos al Hub desde la App Móvil
using Microsoft.Maui.ApplicationModel; // MainThread
using Microsoft.Maui.Storage; // SecureStorage
using Microsoft.Maui.Graphics; // Colors
using Microsoft.Maui.Controls; // ContentPage, UI controls

namespace Itm.Store.Mobile;

public partial class MainPage : ContentPage
{
    private readonly IHttpClientFactory _httpClientFactory;
    private HubConnection? _hubConnection; // Conexión al Hub de SignalR (nullable hasta que InitializeSignalR la cree)

    // Inyectamos la fábrica, tal como lo hacemos en el Backend
    public MainPage(IHttpClientFactory httpClientFactory)
    {
        InitializeComponent();
        _httpClientFactory = httpClientFactory;

        // Inicializamos la conexión al Hub de SignalR
        InitializeSignalR();
    }

    private async void InitializeSignalR()
    {
        // 1. Configuramos el tubo hacia el Gateway (El Gateway lo pasará al Notification.Api)
        // OJO: Recuerden usar usar 10.0.2.2:5000 en Android y localhost:5000 en iOS, porque el emulador de Android no puede usar localhost para referirse a la máquina host, debe usar esta IP especial.
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://10.0.2.2:5000/hubs/notifications") // URL del Hub de SignalR
            .WithAutomaticReconnect() // Resilencia: si se cael Wifi, el va a tratar de reconectar solo. Habilitamos la reconexión automática en caso de pérdida de conexión
            .Build();

        // 2. Le decimos al Hub: "Cuando llegue un mensaje del servidor con el nombre 'TicketReady', haz esto"

        _hubConnection.On<string>("TicketReady", (mensajeDelServidor) =>
        {
            // Esta función se ejecuta cada vez que el servidor envía un mensaje "TickerReady"
            // El mensaje es el string que el servidor envió (Ej: "¡Tu boleta para el producto X ha sido confrimada!")
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Actualizamos la UI con el mensaje recibido del servidor
                ResultLabel.Text = $" ALERTA EN VIVO: {mensajeDelServidor}";
                ResultLabel.TextColor = Colors.Purple;
            });
        });

        // 3.  Encendemos el radio para empezar a escuchar mensajes del servidor

        try

        {
            await _hubConnection.StartAsync();

        }
        catch (Exception ex)

        {
            Console.WriteLine($"Error al conectar con el Hub de SignalR: {ex.Message}");
        }
    }

         
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        // Simulamos que fuimos a un servidor de Identidad (IdentityServer / Auth0)
        // y nos devolvió este JWT. En un caso real, haríamos un POST /api/login.
        // Debe coincidir con Issuer = ItmIdentityServer, Audience = ItmStoreApis y SecretKey = ITM-Super-Secret-Key-For-JWT-Class-2026-Nivel5
        // configurados en Itm.Inventory.Api/appsettings.json.
        string simulatedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJJdG1JZGVudGl0eVNlcnZlciIsImF1ZCI6Ikl0bVN0b3JlQXBpcyIsImVtYWlsIjoiYWRtaW5AaXRtLmVkdS5jbyIsInJvbGUiOiJBZG1pbmlzdHJhZG9yIn0.PaSdxe8NkHzbkrTA40janIgKn4gnVp63yWh_cenvUDw"; //"PASTE_AQUI_UN_JWT_VALIDO_PARA_ItmIdentityServer_ItmStoreApis" Validar en la pagina https://www.jwt.io/

        //  SEGURIDAD NIVEL 5: Lo guardamos en la bóveda criptográfica del celular
        await SecureStorage.Default.SetAsync("jwt_token", simulatedToken);

        ResultLabel.Text = " ¡Token JWT guardado seguro en el dispositivo!";
        ResultLabel.TextColor = Colors.Green;
    }

    private async void OnGetDataClicked(object sender, EventArgs e)
    {
        try
        {
            ResultLabel.Text = "Consultando Gateway...";
            ResultLabel.TextColor = Colors.Orange;

            // 1. Pedimos el cliente configurado (Él ya sabe que debe ir al Gateway y usar el AuthHandler)
            var client = _httpClientFactory.CreateClient("GatewayClient");

            // 2. Hacemos la petición HTTP al Gateway (Ruta que creamos en clases pasadas)
            var response = await client.GetAsync("/api/products/1/check-stock");

            if (response.IsSuccessStatusCode)
            {
                // Si el Gateway y los microservicios responden 200 OK
                var data = await response.Content.ReadAsStringAsync();
                ResultLabel.Text = $" ÉXITO:\n{data}";
                ResultLabel.TextColor = Colors.Green;
            }
            else
            {
                ResultLabel.Text = $" ERROR {response.StatusCode}:\n{await response.Content.ReadAsStringAsync()}";
                ResultLabel.TextColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            // Atrapamos errores de red (Ej: El Gateway está apagado)
            ResultLabel.Text = $" ERROR DE RED:\n{ex.Message}";
            ResultLabel.TextColor = Colors.Red;
        }
    }
}