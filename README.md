# ITM-Tickets Global "The World Tour 2026"

## Sistema de Boletería Distribuida — Nivel 5

---

## Tabla de Contenido

1. [Prerequisitos](#prerequisitos)
2. [Estructura del Proyecto](#estructura-del-proyecto)
3. [Paso 1: Levantar Infraestructura (Docker)](#paso-1-levantar-infraestructura-docker)
4. [Paso 2: Ejecutar Microservicios](#paso-2-ejecutar-microservicios)
5. [Paso 3: Probar el Sistema](#paso-3-probar-el-sistema)
6. [Paso 4: Subir a GitHub](#paso-4-subir-a-github)
7. [Paso 5: Kubernetes (Demo)](#paso-5-kubernetes-demo)
8. [Arquitectura](#arquitectura)
9. [Guía de Sustentación (15 min)](#guía-de-sustentación-15-min)
10. [Troubleshooting](#troubleshooting)

---

## Prerequisitos

| Herramienta | Versión | Verificar | Instalar |
|---|---|---|---|
| .NET SDK | 8.0+ | `dotnet --version` | `winget install Microsoft.DotNet.SDK.8` |
| Docker Desktop | 4.x | Abrir Docker Desktop | [docker.com/desktop](https://docker.com/products/docker-desktop) |
| Git | 2.x | `git --version` | `winget install Git.Git` |

### Verificar que Docker esté corriendo

Abrir Docker Desktop y esperar a que el ícono en la barra de tareas diga "Docker Desktop is running".

---

## Estructura del Proyecto

```
Final/
├── .github/workflows/deploy.yml     ← CI/CD (GitHub Actions)
├── k8s/                             ← Manifiestos Kubernetes + HPA
│   ├── namespace.yaml
│   ├── gateway-deployment.yaml
│   ├── order-deployment.yaml
│   ├── inventory-deployment.yaml
│   ├── product-deployment.yaml
│   ├── notification-deployment.yaml
│   ├── redis-deployment.yaml
│   ├── search-deployment.yaml
│   └── ingress.yaml
├── terraform/main.tf                ← IaC (EKS en AWS)
├── Protos/inventory.proto           ← Contrato gRPC
├── Itm.Gateway.Api/                 ← YARP + JWT + Rate Limiting
├── Itm.Inventory.Api/               ← gRPC Server + REST + JWT
├── Order.Api/                       ← SAGA + gRPC Client + MassTransit
├── Itm.Product.Api/                 ← BFF + Redis Cache-Aside
├── Itm.Price.Api/                   ← Precios
├── Notification.Api/                ← Consumer RabbitMQ + SignalR Hub
├── Search.Api/                      ← Elasticsearch + Qdrant (IA)
├── Itm.Shared.Events/              ← Eventos inmutables
├── Itm.Store.Mobile/               ← .NET MAUI (App Móvil)
├── docker-compose.yml              ← Orquestación local
├── start-all.cmd                   ← Script inicio rápido
└── stop-all-services.cmd           ← Script para detener todo
```

---

## Paso 1: Levantar Infraestructura (Docker)

Abrir una terminal (CMD o PowerShell) en la carpeta `Final/`:

```cmd
cd C:\Users\watabares_solati\ITM\ProgramacionDistribuida\Entregables\Final
```

### Opción A: Si `docker` está en el PATH

```cmd
docker compose up -d rabbitmq redis elasticsearch qdrant
```

### Opción B: Si `docker` NO está en el PATH (usar ruta completa)

```cmd
"C:\Program Files\Docker\Docker\resources\bin\docker.exe" compose up -d rabbitmq redis elasticsearch qdrant
```

### Verificar que los 4 contenedores estén corriendo

```cmd
docker ps
```

Debes ver:
- `itm-rabbitmq` (puertos 5672, 15672)
- `itm-redis` (puerto 6379)
- `itm-elasticsearch` (puerto 9200)
- `itm-qdrant` (puertos 6333, 6334)

### Si hay error "container name already in use"

```cmd
docker rm -f itm-elasticsearch itm-redis itm-qdrant itm-rabbitmq
docker compose up -d rabbitmq redis elasticsearch qdrant
```

**Esperar 10-15 segundos** para que Elasticsearch termine de arrancar.

---

## Paso 2: Ejecutar Microservicios

### Opción Rápida: Script automático

```cmd
start-all.cmd
```

Esto abre 7 ventanas de terminal, una por servicio.

### Opción Manual: Abrir 7 terminales

Cada terminal en la carpeta `Final/`:

```cmd
REM Terminal 1 - Inventory (REST + gRPC)
dotnet run --project Itm.Inventory.Api --launch-profile http

REM Terminal 2 - Price
dotnet run --project Itm.Price.Api --launch-profile http

REM Terminal 3 - Order (SAGA + gRPC client)
dotnet run --project Order.Api --launch-profile http

REM Terminal 4 - Product (Redis cache)
dotnet run --project Itm.Product.Api --launch-profile http

REM Terminal 5 - Notification (SignalR + RabbitMQ consumer)
dotnet run --project Notification.Api --launch-profile http

REM Terminal 6 - Search (Elasticsearch + Qdrant)
dotnet run --project Search.Api --launch-profile http

REM Terminal 7 - Gateway (YARP, iniciar DE ÚLTIMO)
dotnet run --project Itm.Gateway.Api --launch-profile http
```

### Puertos asignados

| Servicio | Puerto | Swagger |
|---|---|---|
| Gateway (YARP) | 5110 | — |
| Inventory.Api | 5293 (REST) + 5294 (gRPC) | http://localhost:5293/swagger |
| Order.Api | 5027 | http://localhost:5027/swagger |
| Price.Api | 5012 | http://localhost:5012/swagger |
| Product.Api | 5298 | http://localhost:5298/swagger |
| Notification.Api | 5089 | — |
| Search.Api | 5100 | http://localhost:5100/swagger |

### URLs de infraestructura

| Servicio | URL | Credenciales |
|---|---|---|
| RabbitMQ Management | http://localhost:15672 | guest / guest |
| Elasticsearch | http://localhost:9200 | — |
| Qdrant Dashboard | http://localhost:6333/dashboard | — |
| Health Monitor | http://localhost:5110/monitor | — |

---

## Paso 3: Probar el Sistema

### 3.1 Seed de datos de búsqueda (hacer UNA sola vez)

```cmd
curl -X POST http://localhost:5100/api/search/seed
```

### 3.2 Búsqueda por texto (Elasticsearch)

```cmd
curl "http://localhost:5100/api/search/text?q=madrid"
```

### 3.3 Búsqueda semántica (Qdrant - IA)

```cmd
curl "http://localhost:5100/api/search/semantic?q=algo+divertido+para+la+familia"
```

Resultado esperado: **"Zona Familiar - Medellín"** como primer resultado (sin usar esas palabras exactas).

### 3.4 Comprar boleta (SAGA + gRPC)

```cmd
curl -X POST http://localhost:5027/api/orders -H "Content-Type: application/json" -d "{\"productId\":1,\"quantity\":1,\"sede\":\"Medellin\"}"
```

Resultado exitoso:
```json
{
  "status": "Boleta reservada exitosamente",
  "orderId": "...",
  "sede": "Medellin",
  "protocol": "gRPC",
  "inventoryLatencyMs": 2,
  "newStock": 49
}
```

> Si dice "Fondos Insuficientes" es la simulación de pago fallido (SAGA compensó el stock). Intentar de nuevo.

### 3.5 Redis Cache (demostrar latencia)

```cmd
REM Primera llamada (cache miss - va a Price.Api)
curl http://localhost:5298/api/products/1

REM Segunda llamada (cache hit - viene de Redis)
curl http://localhost:5298/api/products/1
```

Ver en la terminal de Product.Api: "Cache miss" luego "Cache hit".

### 3.6 Verificar SignalR + RabbitMQ

Después de una compra exitosa, ver en la terminal de Notification.Api:
```
Procesando evento de RabbitMQ para orden: ...
Notificación push enviada via SignalR
```

---

## Paso 4: Subir a GitHub

### 4.1 Crear repositorio en GitHub

Ir a https://github.com/new → crear repo (ej: `ITM-Tickets-Final`). NO inicializar con README.

### 4.2 Crear .gitignore

```cmd
cd C:\Users\watabares_solati\ITM\ProgramacionDistribuida\Entregables\Final
```

Crear archivo `.gitignore` con este contenido:
```
bin/
obj/
.vs/
*.user
*.suo
.idea/
```

### 4.3 Inicializar y subir

```cmd
git init
git add .
git commit -m "feat: ITM-Tickets Nivel 5 completo"
git branch -M main
git remote add origin https://github.com/TU_USUARIO/ITM-Tickets-Final.git
git push -u origin main
```

### 4.4 Si pide autenticación (usar token)

```cmd
git remote set-url origin https://TU_TOKEN@github.com/TU_USUARIO/ITM-Tickets-Final.git
git push -u origin main
```

### 4.5 Configurar secretos para GitHub Actions (CI/CD)

En GitHub → tu repo → Settings → Secrets and variables → Actions → New repository secret:

1. `DOCKER_USERNAME` = tu usuario de Docker Hub
2. `DOCKER_PASSWORD` = tu token/password de Docker Hub

Al hacer el próximo push a `main`, el pipeline compilará y subirá las imágenes automáticamente.

---

## Paso 5: Kubernetes (Demo)

### Prerequisito: Habilitar Kubernetes en Docker Desktop

Docker Desktop → Settings → Kubernetes → ✅ Enable Kubernetes → Apply & Restart (tarda ~2 min)

### Aplicar manifiestos

```cmd
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/
```

### Ver pods corriendo

```cmd
kubectl get pods -n itm-tickets
```

### Ver auto-escalado (HPA)

```cmd
kubectl get hpa -n itm-tickets
```

### Demostrar self-healing (matar un pod)

```cmd
kubectl delete pod -l app=order-api -n itm-tickets
kubectl get pods -n itm-tickets -w
```

K8s recrea el pod automáticamente en segundos.

### Terraform (solo mostrar el plan, NO ejecutar)

```cmd
cd terraform
terraform init
terraform plan
```

Explicar: "Este archivo recrea toda la infraestructura en AWS: VPC, subnets, EKS cluster con auto-scaling de 2 a 6 nodos."

---

## Arquitectura

```
┌─────────────────────────────────────────────────────────────────┐
│                    FRONTERA Y MOVILIDAD                           │
│  ┌──────────────┐    HTTPS     ┌────────────────────────────┐   │
│  │  .NET MAUI   │ ───────────▶ │  API Gateway (YARP)        │   │
│  │  App Móvil   │              │  • JWT Validation          │   │
│  │  SecureStorage│◀── SignalR ──│  • Rate Limiting (100/min) │   │
│  └──────────────┘              │  • Correlation ID          │   │
│                                └─────────────┬──────────────┘   │
└──────────────────────────────────────────────┼──────────────────┘
                                               │
┌──────────────────────────────────────────────┼──────────────────┐
│                 MICROSERVICIOS               │                    │
│                                              ▼                    │
│  ┌────────────┐  gRPC (2ms)  ┌────────────────────┐             │
│  │ Order.Api  │ ════════════▶│ Inventory.Api      │             │
│  │ (SAGA)     │              │ (gRPC Server)      │             │
│  └─────┬──────┘              └────────────────────┘             │
│        │ Publish                                                 │
│        ▼                     ┌────────────────────┐             │
│  ┌────────────┐              │ Product.Api        │             │
│  │ RabbitMQ   │              │ (Redis Cache)      │             │
│  │ (CloudAMQP)│              └────────────────────┘             │
│  └─────┬──────┘                                                  │
│        │ Consume             ┌────────────────────┐             │
│        ▼                     │ Search.Api         │             │
│  ┌────────────────┐          │ • Elasticsearch    │             │
│  │Notification.Api│          │ • Qdrant (IA)      │             │
│  │(SignalR Push)  │          └────────────────────┘             │
│  └────────────────┘                                              │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                    DEVOPS Y CLOUD                                  │
│  GitHub Actions → Docker Hub → Kubernetes (HPA) → Terraform      │
└──────────────────────────────────────────────────────────────────┘
```

---

## Guía de Sustentación (15 min)

### Minuto 0-3: Compra exitosa

1. Abrir Swagger de Order.Api → http://localhost:5027/swagger
2. POST /api/orders: `{"productId":1,"quantity":1,"sede":"Medellin"}`
3. Mostrar: `protocol: gRPC`, `inventoryLatencyMs: 2ms`
4. Mostrar terminal Notification.Api: evento consumido + SignalR push

### Minuto 3-6: Correlation ID en 3 servicios

1. Copiar el `correlationId` de la respuesta
2. Terminal Order.Api: log con ese ID
3. Terminal Inventory.Api: log con ese ID (gRPC)
4. Terminal Notification.Api: log con ese ID (consumer)

### Minuto 6-9: Self-healing en Kubernetes

```cmd
kubectl get pods -n itm-tickets
kubectl delete pod -l app=order-api -n itm-tickets
kubectl get pods -n itm-tickets -w
```

### Minuto 9-12: Terraform

```cmd
cd terraform
terraform plan
```

"Este main.tf recrea toda la infra: VPC, subnets, EKS con auto-scaling 2-6 nodos."

### Minuto 12-15: Búsqueda semántica + Redis

```cmd
curl "http://localhost:5100/api/search/semantic?q=algo+divertido+para+la+familia"
```

"El buscador entiende la intención sin palabras exactas → Zona Familiar."

Mostrar cache hit/miss en Product.Api.

---

## Troubleshooting

### "docker" no se reconoce como comando

```cmd
"C:\Program Files\Docker\Docker\resources\bin\docker.exe" compose up -d
```

### "container name already in use"

```cmd
docker rm -f itm-elasticsearch itm-redis itm-qdrant itm-rabbitmq
```

### Puerto ya en uso

```cmd
netstat -ano | findstr :5293
taskkill /PID <numero> /F
```

### Order.Api dice "Fondos Insuficientes"

Normal — pago simulado con 70% éxito. Demuestra que SAGA funciona (stock devuelto). Intentar de nuevo.

### Elasticsearch no arranca

Esperar 30 segundos. Verificar: `curl http://localhost:9200`

### Search.Api no encuentra resultados

Ejecutar seed: `curl -X POST http://localhost:5100/api/search/seed`

### Para detener todo

```cmd
stop-all-services.cmd
```

O manualmente: cerrar las 7 terminales + `docker compose down`

---

## Rúbrica vs. Implementación

| Criterio | Peso | Qué demostrar | Evidencia |
|---|---|---|---|
| **Integración Funcional** | 1.5 | Compra: MAUI→Gateway→Order→gRPC→Inventory→RabbitMQ→SignalR→MAUI | POST /api/orders |
| **Resiliencia y SAGA** | 1.0 | Pago falla → stock devuelto. Notification caído → mensajes retenidos | Repetir compra |
| **Rendimiento (Redis/gRPC)** | 1.0 | gRPC <10ms. Redis cache hit vs miss | Logs latencia |
| **DevOps y Cloud** | 1.0 | GitHub Actions + K8s HPA + Terraform | .github/, k8s/, terraform/ |
| **IA Semántica** | 0.5 | "algo divertido para familia" → Zona Familiar | /api/search/semantic |

---

*Última actualización: 26 de mayo de 2026*

---

## Ejecutar en otro computador (desde cero)

### Prerequisitos

| Software | Versión | Verificar | Instalar |
|---|---|---|---|
| .NET SDK | 8.0+ | `dotnet --version` | `winget install Microsoft.DotNet.SDK.8` |
| Docker Desktop | 4.x+ | Abrir Docker Desktop | [docker.com/desktop](https://docker.com/products/docker-desktop) |
| Kubernetes | Incluido en Docker Desktop | `kubectl version` | Docker Desktop → Settings → Kubernetes → Enable |
| Git | 2.x+ | `git --version` | `winget install Git.Git` |
| GitHub CLI (opcional) | 2.x+ | `gh --version` | `winget install GitHub.cli` |

### Paso 1: Clonar el repositorio

```
git clone https://github.com/watabares/ITM-Tickets-Final.git
cd ITM-Tickets-Final
```

### Paso 2: Levantar infraestructura (Docker)

```
docker compose up -d rabbitmq redis elasticsearch qdrant
```

Esperar 15 segundos. Verificar: `docker ps` (4 contenedores corriendo).

### Paso 3: Opción A — Kubernetes (producción)

```
kubectl apply -f k8s/
kubectl get pods -n itm-tickets -w
```

Kubernetes descarga las 6 imágenes de Docker Hub (`watabares/itm-*:latest`) automáticamente. Esperar hasta que todos los pods estén en `Running`.

### Paso 3: Opción B — Servicios locales (desarrollo + demo con logs)

```
start-all.cmd
```

Abre 7 terminales (una por servicio). Puertos: Gateway:5110, Order:5027, Inventory:5293, Product:5298, Price:5012, Notification:5089, Search:5100.

### Paso 4: Seed de búsqueda (una sola vez)

```
curl -X POST http://localhost:5100/api/search/seed
```

### Paso 5: App MAUI (Windows)

```
dotnet run --project Itm.Store.Mobile -f net8.0-windows10.0.19041.0
```

### Paso 6: Verificar

```
curl -X POST http://localhost:5027/api/orders -H "Content-Type: application/json" -d "{\"productId\":1,\"quantity\":1,\"sede\":\"Medellin\"}"
```

Respuesta esperada: `{"status":"Boleta reservada exitosamente","orderId":"...","protocol":"gRPC","inventoryLatencyMs":2}`

### URLs importantes

| Recurso | URL |
|---|---|
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| Elasticsearch | http://localhost:9200 |
| Qdrant Dashboard | http://localhost:6333/dashboard |
| Health Check (Gateway) | http://localhost:5110/monitor |
| Panel de Demo | Abrir `demo-panel.html` en navegador |

### Notas

- **No se necesita compilar los microservicios** si se usa Kubernetes (Opción A) — las imágenes ya están en Docker Hub.
- **MAUI sí requiere compilación** local (.NET 8 SDK + Windows).
- **RabbitMQ es local** (Docker), no CloudAMQP.
- **Kubernetes requiere** Docker Desktop con Kubernetes habilitado (Settings → Kubernetes → Enable).
- **El archivo `terraform/main.tf`** es para desplegar en AWS EKS (no se ejecuta en la demo, solo se explica).

