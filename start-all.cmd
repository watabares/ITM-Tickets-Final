@echo off
REM ============================================================
REM ITM-Tickets - Iniciar todos los servicios (desarrollo local)
REM ============================================================
REM Prerequisitos:
REM   - Docker Desktop corriendo (para Redis, Elasticsearch, Qdrant)
REM   - .NET 8 SDK instalado
REM   - Puertos 5293, 5294, 5027, 5012, 5298, 5089, 5100, 5110 libres
REM ============================================================

echo === PASO 1: Levantar infraestructura con Docker ===
docker-compose up -d rabbitmq redis elasticsearch qdrant
echo Esperando 10 segundos para que los servicios arranquen...
timeout /t 10 /nobreak

echo.
echo === PASO 2: Iniciar microservicios .NET ===
echo.

echo [1/6] Iniciando Inventory.Api (REST:5293 + gRPC:5294)...
start "Inventory.Api" cmd /c "dotnet run --project Itm.Inventory.Api --launch-profile http"

timeout /t 3 /nobreak

echo [2/6] Iniciando Price.Api (5012)...
start "Price.Api" cmd /c "dotnet run --project Itm.Price.Api --launch-profile http"

echo [3/6] Iniciando Order.Api (5027)...
start "Order.Api" cmd /c "dotnet run --project Order.Api --launch-profile http"

echo [4/6] Iniciando Product.Api (5298)...
start "Product.Api" cmd /c "dotnet run --project Itm.Product.Api --launch-profile http"

echo [5/6] Iniciando Notification.Api (5089)...
start "Notification.Api" cmd /c "dotnet run --project Notification.Api --launch-profile http"

echo [6/6] Iniciando Search.Api (5100)...
start "Search.Api" cmd /c "dotnet run --project Search.Api --launch-profile http"

timeout /t 5 /nobreak

echo.
echo === PASO 3: Iniciar Gateway (5110) ===
start "Gateway.Api" cmd /c "dotnet run --project Itm.Gateway.Api --launch-profile http"

echo.
echo ============================================================
echo   TODOS LOS SERVICIOS INICIADOS
echo ============================================================
echo.
echo   Gateway:        http://localhost:5110
echo   Health Monitor: http://localhost:5110/monitor
echo   Inventory:      http://localhost:5293/swagger
echo   Order:          http://localhost:5027/swagger
echo   Price:          http://localhost:5012/swagger
echo   Product:        http://localhost:5298/swagger
echo   Notification:   http://localhost:5089
echo   Search:         http://localhost:5100/swagger
echo.
echo   Elasticsearch:  http://localhost:9200
echo   Qdrant:         http://localhost:6333
echo   RabbitMQ:       http://localhost:15672 (guest/guest)
echo   Redis:          localhost:6379
echo.
echo === Para probar: ===
echo   curl -X POST http://localhost:5100/api/search/seed
echo   curl "http://localhost:5100/api/search/text?q=madrid"
echo   curl "http://localhost:5100/api/search/semantic?q=algo+divertido+para+familia"
echo   curl -X POST http://localhost:5027/api/orders -H "Content-Type: application/json" -d "{\"productId\":1,\"quantity\":1,\"sede\":\"Medellin\"}"
echo.
pause
