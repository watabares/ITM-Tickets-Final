@echo off
REM ============================================================
REM ITM-Tickets - Detener todos los servicios
REM ============================================================

echo === Deteniendo microservicios .NET ===
taskkill /FI "WINDOWTITLE eq Inventory.Api*" /F 2>nul
taskkill /FI "WINDOWTITLE eq Price.Api*" /F 2>nul
taskkill /FI "WINDOWTITLE eq Order.Api*" /F 2>nul
taskkill /FI "WINDOWTITLE eq Product.Api*" /F 2>nul
taskkill /FI "WINDOWTITLE eq Notification.Api*" /F 2>nul
taskkill /FI "WINDOWTITLE eq Search.Api*" /F 2>nul
taskkill /FI "WINDOWTITLE eq Gateway.Api*" /F 2>nul

echo === Deteniendo contenedores Docker ===
docker-compose down

echo === Todo detenido ===
pause
