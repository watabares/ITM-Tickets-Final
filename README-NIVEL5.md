# ITM-Tickets Global "The World Tour 2026" — Nivel 5

## Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────────────────────┐
│                        FRONTERA Y MOVILIDAD                          │
│  ┌──────────────┐     HTTPS      ┌──────────────────────────────┐   │
│  │  .NET MAUI   │ ──────────────▶│  API Gateway (YARP)          │   │
│  │  App Móvil   │                │  • JWT Validation            │   │
│  │  SecureStorage│◀──── SignalR ──│  • Rate Limiting (100/min)   │   │
│  └──────────────┘                │  • Correlation ID            │   │
│                                  └──────────────┬───────────────┘   │
└─────────────────────────────────────────────────┼───────────────────┘
                                                  │
┌─────────────────────────────────────────────────┼───────────────────┐
│                    NÚCLEO DE MICROSERVICIOS      │                    │
│                                                  ▼                    │
│  ┌────────────┐  gRPC (binario)  ┌──────────────────┐               │
│  │ Order.Api  │ ◀═══════════════▶│ Inventory.Api    │               │
│  │ (SAGA)     │                  │ (gRPC Server)    │               │
│  └─────┬──────┘                  └──────────────────┘               │
│        │                                                             │
│        │ Publish                 ┌──────────────────┐               │
│        ▼                         │ Product.Api      │               │
│  ┌────────────┐                  │ (BFF + Redis)    │               │
│  │ RabbitMQ   │                  └──────────────────┘               │
│  │ (CloudAMQP)│                                                      │
│  └─────┬──────┘                  ┌──────────────────┐               │
│        │ Consume                 │ Price.Api        │               │
│        ▼                         └──────────────────┘               │
│  ┌────────────────┐                                                  │
│  │Notification.Api│              ┌──────────────────┐               │
│  │(SignalR Push)  │              │ Search.Api       │               │
│  └────────────────┘              │ • Elasticsearch  │               │
│                                  │ • Qdrant (IA)    │               │
│                                  └──────────────────┘               │
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│                    INFRAESTRUCTURA Y NUBE                             │
│                                                                       │
│  Kubernetes (EKS)          GitHub Actions          Terraform          │
│  • HPA (CPU 60-70%)       • Build + Test          • VPC + Subnets    │
│  • Self-healing            • Push Docker Hub      • EKS Cluster      │
│  • Ingress HTTPS           • Multi-service       • Node Group (ASG)  │
│  • 2-15 réplicas                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

## Componentes vs. Rúbrica

| Criterio | Peso | Implementación | Evidencia |
|----------|------|----------------|-----------|
| **Integración Funcional** | 1.5 | MAUI → Gateway → Order → gRPC → Inventory → RabbitMQ → SignalR → MAUI | Flujo completo de compra con confirmación real-time |
| **Resiliencia y SAGA** | 1.0 | Order.Api: reduce stock (gRPC) → pago → si falla → release stock (gRPC). RabbitMQ retiene mensajes si Notification cae | Logs con Correlation ID en 3 servicios |
| **Rendimiento (Redis/gRPC)** | 1.0 | gRPC binario Order↔Inventory (<10ms). Redis cache-aside en Product.Api (TTL 60s) | Stopwatch en logs muestra latencia |
| **DevOps y Cloud** | 1.0 | Dockerfiles multi-stage. GitHub Actions CI/CD. K8s con HPA. Terraform para EKS | `kubectl get hpa`, pipeline verde |
| **IA Semántica** | 0.5 | Search.Api: Elasticsearch (texto fuzzy) + Qdrant (vectores, búsqueda por "vibe") | Buscar "algo divertido para niños" → Zona Familiar |

## Estructura del Proyecto

```
├── .github/workflows/deploy.yml     ← CI/CD Pipeline
├── k8s/                             ← Manifiestos Kubernetes
│   ├── namespace.yaml
│   ├── gateway-deployment.yaml      ← + HPA
│   ├── order-deployment.yaml        ← + HPA
│   ├── inventory-deployment.yaml    ← + HPA + gRPC port
│   ├── product-deployment.yaml      ← + HPA
│   ├── notification-deployment.yaml
│   ├── redis-deployment.yaml
│   ├── search-deployment.yaml       ← + Elasticsearch + Qdrant
│   └── ingress.yaml                 ← HTTPS Ingress
├── terraform/main.tf                ← IaC para EKS
├── Protos/inventory.proto           ← Contrato gRPC
├── Itm.Gateway.Api/                 ← YARP + JWT + Rate Limiting
├── Order.Api/                       ← SAGA + gRPC Client + MassTransit
├── Itm.Inventory.Api/               ← gRPC Server + REST + JWT
├── Itm.Product.Api/                 ← BFF + Redis Cache-Aside
├── Itm.Price.Api/                   ← Precios
├── Notification.Api/                ← Consumer + SignalR Hub
├── Search.Api/                      ← Elasticsearch + Qdrant
├── Itm.Shared.Events/              ← Eventos inmutables
├── Itm.Store.Mobile/               ← .NET MAUI
└── docker-compose.yml              ← Orquestación local
```

## Demo en 15 minutos (Guía)

### 1. Compra exitosa (3 min)
```bash
# Desde MAUI o curl:
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"productId": 1, "quantity": 1, "sede": "Medellin"}'

# Respuesta muestra: Protocol=gRPC, InventoryLatencyMs=<10ms
# SignalR push llega a la app MAUI: "¡Tu boleta ha sido confirmada!"
```

### 2. Correlation ID en 3 servicios (3 min)
```bash
# Los logs muestran el mismo CorrelationId en:
# - Gateway (middleware)
# - Order.Api (scope)
# - Inventory.Api (gRPC context)
# - Notification.Api (consumer)
```

### 3. Self-healing en Kubernetes (4 min)
```bash
# Matar un pod
kubectl delete pod -l app=order-api -n itm-tickets

# Ver cómo K8s lo recrea automáticamente
kubectl get pods -n itm-tickets -w

# Ver HPA escalando
kubectl get hpa -n itm-tickets
```

### 4. Terraform (2 min)
```bash
cd terraform
terraform init
terraform plan
# Mostrar el plan: VPC, Subnets, EKS, Node Group con auto-scaling
```

### 5. Búsqueda semántica (3 min)
```bash
# Texto exacto (Elasticsearch)
curl "http://localhost:5000/api/search/text?q=concierto+madrid"

# Búsqueda por "vibe" (Qdrant - IA)
curl "http://localhost:5000/api/search/semantic?q=algo+divertido+para+la+familia"
# → Retorna "Zona Familiar" sin usar esas palabras exactas
```

## Comandos rápidos

```bash
# Levantar todo local
docker-compose up -d

# Verificar salud
curl http://localhost:5000/monitor

# Aplicar K8s
kubectl apply -f k8s/

# Ver auto-escalado
kubectl get hpa -n itm-tickets

# Seed de búsqueda
curl -X POST http://localhost:5100/api/search/seed
```

## Sedes del evento

| Sede | Boletas | Moneda |
|------|---------|--------|
| Medellín (Colombia) | VIP, General, After Party, Taller, Familiar | COP |
| Madrid (España) | VIP, General, Gastronomía, Salsa y Flamenco | EUR |
| Global | Combo Dos Mundos (ambas sedes) | COP |
