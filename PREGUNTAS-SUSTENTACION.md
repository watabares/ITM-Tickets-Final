# Posibles Preguntas del Docente — ITM-Tickets

---

## SAGA y Resiliencia

### P: ¿Qué pasa si el pago falla después de reservar el stock?
**R:** Se ejecuta la compensación de la SAGA. Order.Api llama a `ReleaseStock` via gRPC para devolver las unidades reservadas. El usuario recibe un mensaje de error pero el inventario queda consistente. Esto se puede ver en la demo cuando dice "Fondos Insuficientes — Stock compensado via gRPC en 2ms".

### P: ¿Qué pasa si Notification.Api está caído cuando se hace una compra?
**R:** La compra se completa normalmente. El evento `OrderCreatedEvent` queda retenido en la cola de RabbitMQ (CloudAMQP). Cuando Notification.Api se levante, consume los mensajes pendientes y envía las notificaciones. Esto es desacoplamiento temporal — el productor no depende del consumidor.

### P: ¿Qué pasa si dos usuarios intentan comprar la última boleta al mismo tiempo?
**R:** gRPC es síncrono — el primero que llegue a `ReduceStock` reserva la boleta. El segundo recibe "Stock insuficiente" y la SAGA no avanza. En producción se usaría un lock optimista o pesimista en la base de datos para garantizar atomicidad.

### P: ¿Por qué SAGA orquestada y no coreografiada?
**R:** Porque el flujo es lineal (reservar → pagar → confirmar) y necesitamos control explícito de la compensación. En una SAGA coreografiada cada servicio emite eventos y reacciona, lo cual es más complejo de debuggear. La orquestada centraliza la lógica en Order.Api y facilita el rollback.

### P: ¿Qué es MassTransit?
**R:** Es una librería .NET que abstrae el message broker. Nos permite publicar y consumir eventos sin acoplarnos directamente a RabbitMQ. Si mañana migramos a Azure Service Bus o Amazon SQS, solo cambiamos la configuración, no el código.

---

## gRPC y Rendimiento

### P: ¿Por qué gRPC en vez de REST para Order↔Inventory?
**R:** gRPC usa Protocol Buffers (binario) en vez de JSON (texto). Es más rápido porque: serialización binaria más eficiente, HTTP/2 con multiplexing, y contratos tipados (.proto). En la demo se ve latencia de 2-11ms vs los ~50-100ms que tomaría REST con JSON.

### P: ¿Qué es un archivo .proto?
**R:** Es el contrato del servicio gRPC. Define los mensajes (como DTOs) y los métodos disponibles (como endpoints). Tanto el servidor (Inventory) como el cliente (Order) generan código a partir de este archivo, garantizando compatibilidad en compilación.

### P: ¿Se puede usar gRPC desde el navegador?
**R:** No directamente — los navegadores no soportan HTTP/2 trailers que gRPC necesita. Por eso el Gateway expone REST hacia afuera y internamente los microservicios se comunican por gRPC. Existe gRPC-Web como alternativa pero agrega complejidad.

### P: ¿Qué pasa si Inventory.Api está caído y Order intenta llamar por gRPC?
**R:** El cliente gRPC lanza una excepción `RpcException` con status `Unavailable`. Order.Api la captura y retorna un error 503 al usuario. En producción se agregaría un circuit breaker (Polly) para no saturar un servicio caído.

---

## Redis y Cache

### P: ¿Qué patrón de cache usan?
**R:** Cache-Aside (Lazy Loading). El servicio primero consulta Redis; si hay dato (hit), lo retorna. Si no (miss), va a la fuente (Price.Api), guarda en Redis con TTL de 60 segundos, y retorna. El 90% de consultas de precios se resuelven sin tocar la base de datos.

### P: ¿Qué pasa si el precio cambia y Redis tiene el dato viejo?
**R:** El TTL de 60 segundos garantiza que eventualmente se actualice. Para cambios críticos se puede invalidar la cache manualmente (`cache.Remove(key)`) o usar un patrón Write-Through donde al actualizar el precio también se actualiza Redis.

### P: ¿Por qué Redis y no cache en memoria?
**R:** Porque tenemos múltiples réplicas del servicio (HPA escala a 8 pods). Cache en memoria no se comparte entre pods — cada uno tendría su propia copia. Redis es distribuido: todos los pods leen del mismo cache, garantizando consistencia.

### P: ¿Qué pasa si Redis se cae?
**R:** El servicio sigue funcionando — simplemente todas las llamadas son cache miss y van directo a Price.Api. La latencia sube pero no hay error. Redis es una optimización, no una dependencia crítica.

---

## Búsqueda Semántica (IA)

### P: ¿Cómo funciona la búsqueda semántica?
**R:** Cada evento se convierte en un vector numérico (embedding) que representa su significado. Cuando el usuario busca "algo divertido para la familia", esa frase también se convierte en vector y Qdrant busca los vectores más cercanos por similitud coseno. No compara palabras — compara significados.

### P: ¿Qué es Qdrant?
**R:** Es una base de datos vectorial. Almacena embeddings (vectores de alta dimensión) y permite búsqueda por similitud. Es el motor detrás de búsquedas semánticas, recomendaciones y RAG (Retrieval Augmented Generation) en sistemas de IA.

### P: ¿Qué diferencia hay entre Elasticsearch y Qdrant?
**R:** Elasticsearch busca por **texto** (palabras exactas, fuzzy matching, sinónimos). Qdrant busca por **significado** (vectores, similitud semántica). Elasticsearch encuentra "concierto madrid" si esas palabras están en el documento. Qdrant encuentra "Zona Familiar" cuando buscas "algo divertido para niños" aunque no comparta palabras.

### P: ¿En producción usarían embeddings propios o de OpenAI?
**R:** En producción usaríamos un modelo como `sentence-transformers` o la API de OpenAI Embeddings para generar vectores de 768-1536 dimensiones. En este demo usamos un embedding simplificado de 64 dimensiones basado en bag-of-words con pesos semánticos para demostrar el concepto.

---

## Kubernetes y DevOps

### P: ¿Qué es HPA y cómo funciona?
**R:** Horizontal Pod Autoscaler. Monitorea métricas (CPU en nuestro caso) y escala el número de réplicas automáticamente. Si Order.Api supera 60% de CPU, K8s crea más pods (hasta 15). Si baja, los elimina (mínimo 2). Así soportamos picos de 50,000 usuarios sin provisionar recursos fijos.

### P: ¿Qué pasó cuando mataste el pod?
**R:** El Deployment de K8s tiene `replicas: 2`. Cuando eliminé los pods, el controlador detectó que hay 0 de 2 réplicas deseadas y creó 2 nuevos pods automáticamente. Esto es self-healing — el sistema se recupera solo sin intervención humana.

### P: ¿Por qué Kubernetes y no solo Docker Compose?
**R:** Docker Compose es para desarrollo local. Kubernetes agrega: auto-escalado (HPA), self-healing, rolling updates (zero downtime), service discovery, load balancing, y gestión declarativa. Es lo que se usa en producción para sistemas que no pueden caerse.

### P: ¿Qué hace el GitHub Actions pipeline?
**R:** Al hacer push a `main`: compila la solución, corre tests, construye 6 imágenes Docker (multi-stage build), y las sube a Docker Hub con tags `latest` y el SHA del commit. Los pods de K8s pueden hacer pull de esas imágenes para actualizar.

### P: ¿Qué es Terraform y por qué no crearon la infra manualmente?
**R:** Terraform es Infraestructura como Código. El archivo `main.tf` describe toda la infra (VPC, subnets, EKS, nodos) de forma declarativa. Ventajas: reproducible (otro equipo puede recrear el mismo ambiente), versionada (git), auditable, y destruible con un comando.

### P: ¿Qué es un Dockerfile multi-stage?
**R:** Tiene múltiples etapas: una con el SDK completo (para compilar) y otra solo con el runtime (para ejecutar). La imagen final pesa ~100MB en vez de ~700MB porque no incluye el SDK. Menos superficie de ataque y deploys más rápidos.

---

## Gateway y Seguridad

### P: ¿Qué es YARP?
**R:** Yet Another Reverse Proxy — es el reverse proxy de Microsoft para .NET. Actúa como punto de entrada único: recibe todas las peticiones, valida JWT, aplica rate limiting, agrega Correlation ID, y enruta al microservicio correcto. Los clientes solo conocen una URL.

### P: ¿Cómo funciona el Rate Limiting?
**R:** Dos políticas: global (100 requests/minuto por IP) y específica para compras (5 compras/minuto por IP). Si un atacante intenta fuerza bruta o un bot compra masivamente, recibe HTTP 429 Too Many Requests. Protege contra DDoS y acaparamiento de boletas.

### P: ¿Cómo funciona la autenticación JWT?
**R:** La app MAUI guarda un JWT en SecureStorage (cifrado del dispositivo). Cada request al Gateway incluye el header `Authorization: Bearer <token>`. El Gateway valida firma, issuer, audience y expiración. Si es inválido → 401 Unauthorized antes de llegar a los microservicios.

### P: ¿Por qué validar JWT en el Gateway Y en Inventory?
**R:** Defensa en profundidad. Si alguien bypasea el Gateway (acceso directo a la red interna), Inventory.Api también rechaza requests sin token válido. Nunca confiar en una sola capa de seguridad.

---

## SignalR y Tiempo Real

### P: ¿Cómo llega la notificación a la app móvil?
**R:** Flujo: Order.Api publica evento → RabbitMQ → Notification.Api consume → SignalR Hub envía push a todos los clientes conectados via WebSocket. La app MAUI mantiene una conexión persistente al Hub y recibe el mensaje "TicketReady" en tiempo real.

### P: ¿Qué pasa si el usuario cierra la app antes de recibir la notificación?
**R:** La conexión WebSocket se cierra. Cuando vuelva a abrir la app, se reconecta automáticamente (`WithAutomaticReconnect`). Para notificaciones offline se necesitaría push notifications nativas (Firebase/APNs), que es una mejora futura.

---

## Arquitectura General

### P: ¿Por qué microservicios y no un monolito?
**R:** Escalabilidad independiente (solo escalar Order.Api en picos de venta), despliegue independiente (actualizar Search sin tocar Order), resiliencia (si Search cae, las compras siguen funcionando), y equipos autónomos (cada servicio puede usar tecnologías diferentes).

### P: ¿Cuál es el punto único de falla del sistema?
**R:** El Gateway. Si cae, nada entra. Por eso tiene 2 réplicas mínimo con HPA hasta 10. En producción se pondría un Load Balancer de AWS (ALB) delante del Gateway para redundancia adicional.

### P: ¿Cómo garantizan que una boleta no se venda dos veces?
**R:** Inventory.Api es el único dueño del stock (Single Source of Truth). La operación `ReduceStock` es atómica — en producción sería una transacción de base de datos con lock. No hay dos servicios que puedan modificar el stock simultáneamente.

### P: ¿Qué mejorarían si tuvieran más tiempo?
**R:** 1) Base de datos real (PostgreSQL) en vez de memoria. 2) Circuit breaker con Polly para resiliencia. 3) Observabilidad con OpenTelemetry + Jaeger para tracing distribuido. 4) Push notifications nativas para la app. 5) Tests de carga con k6 para validar los 50,000 concurrentes.
