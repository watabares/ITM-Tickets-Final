@echo off
REM ============================================================
REM  Detener todos los servicios y emulador
REM ============================================================

echo Deteniendo servicios...

for /f "tokens=5" %%a in ('netstat -ano ^| findstr "LISTENING" ^| findstr ":5110 :5293 :5298 :5012 :5027 :5089"') do (
    taskkill /PID %%a /F >nul 2>&1
)

echo Deteniendo emulador...
taskkill /IM emulator.exe /F >nul 2>&1
taskkill /IM qemu-system-x86_64.exe /F >nul 2>&1
taskkill /IM adb.exe /F >nul 2>&1

echo.
echo === Todo detenido ===
pause
