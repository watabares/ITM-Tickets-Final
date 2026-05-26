using Microsoft.AspNetCore.SignalR;

namespace Itm.Notification.Api.Hubs;

// Esta clase hereda de Hub. Representa  la conexión en tiempo real entre el servidor y los clientes. Es el punto central para enviar notificaciones a los clientes conectados.
//Por ahora no necesitamos métodos adentro, porque el servidor será quien hable hacia el cliente, no al revés. Sin embargo, si en el futuro queremos que los clientes puedan enviar mensajes al servidor, podríamos agregar métodos aquí para manejar esas interacciones.

public class NotificationHub : Hub
{
    // Opcional: Podriamos registrar cuando un usuario se conecta o desconecta, para llevar un control de los clientes activos. Esto se puede hacer sobrescribiendo los métodos OnConnectedAsync y OnDisconnectedAsync.

    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"[SinalR] Cliente conectado: {Context.ConnectionId}");
        return base.OnConnectedAsync();
    }
}
