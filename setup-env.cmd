@echo off
REM ============================================================
REM  Script para configurar el entorno de desarrollo
REM  Ejecutar en CADA terminal nueva antes de los comandos dotnet
REM ============================================================

set "JAVA_HOME=C:\Users\watabares_solati\.jdk\jdk-17.0.15+6"
set "ANDROID_HOME=C:\Users\watabares_solati\AppData\Local\Android\Sdk"
set "ANDROID_SDK_ROOT=%ANDROID_HOME%"
set "DOTNET_ROOT=C:\Users\watabares_solati\.dotnet"
set "PATH=%DOTNET_ROOT%;%JAVA_HOME%\bin;%ANDROID_HOME%\platform-tools;%ANDROID_HOME%\emulator;%ANDROID_HOME%\cmdline-tools\latest\bin;%PATH%"

echo.
echo === Entorno configurado ===
echo JAVA_HOME=%JAVA_HOME%
echo ANDROID_HOME=%ANDROID_HOME%
echo DOTNET_ROOT=%DOTNET_ROOT%
echo.
echo Comandos disponibles:
echo   dotnet run --project Itm.Inventory.Api       (puerto 5293)
echo   dotnet run --project Itm.Price.Api            (puerto 5012)
echo   dotnet run --project Itm.Product.Api          (puerto 5298)
echo   dotnet run --project Order.Api                (puerto 5027)
echo   dotnet run --project Notification.Api         (puerto 5089)
echo   dotnet run --project Itm.Gateway.Api          (puerto 5110)
echo   emulator -avd Pixel_5_API_34                  (emulador Android)
echo   dotnet build Itm.Store.Mobile -t:Run -f net9.0-android  (app MAUI)
echo.
