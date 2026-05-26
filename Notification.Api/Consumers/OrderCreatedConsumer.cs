using MassTransit; // <-- NUEVO IMPORT
using Itm.Shared.Events; // <-- NUEVO IMPORT
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Itm.Notification.Api.Hubs; // <-- NUEVO IMPORT: Referencia a la carpeta donde está nuestro Hub de SignalR

namespace Itm.Notification.Api.Consumers;

// Heredar de IConsumer le dice al motor : "Despiértame cuando llegue este tipo de evento" 
public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IHubContext<NotificationHub> _hubContext; // Inyectamos el Hub <-- NUEVO CAMPO: Contexto de SignalR para enviar notificaciones en tiempo real

    public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger, IHubContext<NotificationHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        _logger.LogInformation("Procesando evento de RabbitMQ para orden: {OrderId}", context.Message.OrderId);

        // Simulamos tiempo de procesamiento pesado (generación de PDF, pago , etc.)
        await Task.Delay(3000);

        // Magia en tiempo real
        // Le decimos al HUB: "Mandale a todos los clientes conectados un evento llamdo ´TickerReady´ "

        var message = $"¡Tu boleta para el producto {context.Message.ProductId} ha sido confrimada!";

        await _hubContext.Clients.All.SendAsync("TickerReady", message);

        _logger.LogInformation("Notificación push enviada via SignalR");
}
}

