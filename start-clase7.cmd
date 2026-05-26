@echo off
REM ============================================================
REM  Arranque rapido - Clase 7: Prueba Movil Completa
REM  Ejecutar desde la carpeta PD174-3
REM ============================================================

set "JAVA_HOME=C:\Users\watabares_solati\.jdk\jdk-17.0.15+6"
set "ANDROID_HOME=C:\Users\watabares_solati\AppData\Local\Android\Sdk"
set "DOTNET_ROOT=C:\Users\watabares_solati\.dotnet"
set "PATH=%DOTNET_ROOT%;%JAVA_HOME%\bin;%ANDROID_HOME%\platform-tools;%ANDROID_HOME%\emulator;%PATH%"

echo.
echo ========================================
echo  ITM Distributed System - Clase 7
echo ========================================
echo.

echo [1/5] Arrancando Inventory.Api (puerto 5293)...
start "Inventory.Api" cmd /k "cd /d %~dp0 && dotnet run --project Itm.Inventory.Api"

echo [2/5] Arrancando Product.Api (puerto 5298)...
start "Product.Api" cmd /k "cd /d %~dp0 && dotnet run --project Itm.Product.Api"

echo [3/5] Arrancando Gateway.Api (puerto 5110)...
start "Gateway.Api" cmd /k "cd /d %~dp0 && dotnet run --project Itm.Gateway.Api"

echo [4/5] Arrancando emulador Android (Pixel_5_API_34)...
start "" "%ANDROID_HOME%\emulator\emulator.exe" -avd Pixel_5_API_34

echo [5/5] Esperando 40 segundos a que el emulador arranque...
ping -n 41 127.0.0.1 >nul

echo Desplegando app MAUI en el emulador...
"%DOTNET_ROOT%\dotnet.exe" build "%~dp0Itm.Store.Mobile\Itm.Store.Mobile.csproj" -t:Run -f net9.0-android

echo.
echo ========================================
echo  Todo listo! Prueba en el emulador:
echo  1. Consultar sin sesion = 401
echo  2. Iniciar sesion = Token guardado
echo  3. Consultar con sesion = JSON stock
echo ========================================
pause
