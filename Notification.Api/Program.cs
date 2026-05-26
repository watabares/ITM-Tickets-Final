using MassTransit; // <-- NUEVO IMPORT
using Itm.Notification.Api.Consumers; // <-- NUEVO IMPORT
using Itm.Notification.Api.Hubs; // <-- NUEVO IMPORT: Referencia a la carpeta donde está nuestro Hub de SignalR
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;

var builder = WebApplication.CreateBuilder(args);
// 1. REGISTRO DE SERVICIOS (DEPENDENCY INJECTION)

// Signal R: Registramos el servicio de SignalR para habilitar la comunicación en tiempo real entre el servidor y los clientes
// Preparamos el motor para manejar conexxiones persistentes con los clientes, lo que nos permitirá enviar notificaciones en tiempo real.
builder.Services.AddSignalR();

// MassTransit:  Configuración de RabbitMQ para recibir eventos de la nube. MassTransit es una librería que facilita la integración con sistemas de mensajería como RabbitMQ, Azure Service Bus, etc. En este caso, usaremos RabbitMQ para recibir eventos de la nube (como OrderCreatedEvent) y procesarlos en esta aplicación.
builder.Services.AddMassTransit(x =>
{
    // Registramos nuestro obrero de eventos (consumidor) para que MassTransit sepa a qué eventos debe "despertar" esta aplicación
    x.AddConsumer<OrderCreatedConsumer>(); // <-- REGISTRAMOS EL CONSUMIDOR
    x.UsingRabbitMq((context, cfg) =>
    {
        // En un trabajo real, esta URL debe venir de configuración segura (KeyVault / env vars)
        cfg.Host("amqps://miqffttk:1pscfTN1wGyzJHwe8BTEFMyocp9U-bEp@moose.rmq.cloudamqp.com/miqffttk");

        // Configuramos el nombre de la "fila" donde el obrero va a escuchar
        cfg.ReceiveEndpoint("notificaciones-cola", e =>
        {
            // Le decimos a esta fila que cuando llegue un evento del tipo OrderCreatedEvent, despierte al OrderCreatedConsumer
            e.ConfigureConsumer<OrderCreatedConsumer>(context);
        });
    });
});

// Registro básico de health checks para que el Gateway pueda monitorear este servicio
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => "Notification.Api  activa y con soporte de Websockets (Signal R)...");

// Signal R Hub: Exponemos la ruta TCP por donde se conecta la App Móvil para recibir las notificaciones en tiempo real. Esta ruta es la que los clientes usarán para establecer la conexión persistente con el servidor.
// El Gateway (YARP) deberá apuntar a esta ruta para que las notificaciones puedan llegar a los clientes móviles a través del Gateway.
app.MapHub<NotificationHub>("/hubs/notifications");

// Endpoint de salud en formato JSON compatible con HealthChecks UI
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
