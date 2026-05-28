using Microsoft.AspNetCore.SignalR.Client;

namespace Itm.Store.Mobile.Services;

/// <summary>
/// Servicio SignalR que se conecta al Hub de Notificaciones.
/// Recibe push en tiempo real cuando una orden se procesa exitosamente.
/// Flujo: Order.Api → RabbitMQ → Notification.Api (Consumer) → SignalR Hub → MAUI
/// </summary>
public class NotificationService
{
    private HubConnection? _hubConnection;

    public event EventHandler<string>? OnTicketReady;

    public async Task ConnectAsync()
    {
        // Conectar al Hub de Notificaciones
        // Local: puerto 5089 (Notification.Api directo)
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5089/hubs/notifications")
            .WithAutomaticReconnect()
            .Build();

        // Escuchar el evento "TicketReady" que publica Notification.Api
        _hubConnection.On<string>("TicketReady", (message) =>
        {
            OnTicketReady?.Invoke(this, message);
        });

        // Escuchar evento genérico "OrderConfirmed"
        _hubConnection.On<string>("OrderConfirmed", (message) =>
        {
            OnTicketReady?.Invoke(this, $"🎫 Orden confirmada: {message}");
        });

        await _hubConnection.StartAsync();
    }

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task DisconnectAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }
    }
}
