using Itm.Store.Mobile.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Itm.Store.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // REGISTRO DE ARQUITECTURA

            // 1. Registrar nuestro peaje de seguridad (AuthHandler) para que se ejecute en cada petición HTTP
            builder.Services.AddTransient<AuthHandler>();

            // 2. Registrar el cliente HTTP apuntando UNICAMENTE a nuestro API Gateway,
            //    y agregar el peaje de seguridad (AuthHandler) a su pipeline de ejecución
            builder.Services.AddHttpClient("GatewayClient", client =>
                {
                    // El emulador 10.0.2.2 es la IP que usa Android para hablar con el localhost del PC.
                    // En dispositivo físico, cambiar por la IP local del PC en la red Wi‑Fi.
                    // El Gateway corre por defecto en http://localhost:5110, así que desde el emulador usamos el mismo puerto.
                    client.BaseAddress = new Uri("http://10.0.2.2:5110/");
                })
                .AddHttpMessageHandler<AuthHandler>();

            // 3. Registramos la vista principal de la aplicación (MainPage)
            builder.Services.AddTransient<MainPage>();

            return builder.Build();
        }
    }
}
