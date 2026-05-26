# Itm.Inventory.Api

Servicio de Inventario para el ecosistema distribuido de ejemplo.

Expone el stock disponible de cada producto y está protegido con JWT.

## Endpoints principales

- `GET /api/inventory/{id}`
  - Devuelve el inventario de un producto por `id`.
  - Requiere autenticación JWT (`Bearer` token).

Ejemplo de respuesta:

```json
{
  "productId": 1,
  "stock": 50,
  "sku": "LAPTOP-DELL"
}
```

## Seguridad (JWT)

El servicio usa autenticación `Bearer` con firma simétrica.

Configuración en `appsettings.json`:

```json
"JwtSettings": {
  "Issuer": "ItmIdentityServer",
  "Audience": "ItmStoreApis",
  "SecretKey": "ITM-Super-Secret-Key-For-JWT-Class-2026-Nivel5"
}
```

En `Program.cs` se configura:

- `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)`
- `AddJwtBearer(...)` con validación de emisor, audiencia, expiración y firma.
- `app.UseAuthentication();`
- `app.UseAuthorization();`
- El endpoint se protege con `.RequireAuthorization()`.

## Cómo probar rápido

1. Ejecutar `Itm.Inventory.Api`.
2. Llamar sin token:
   - `GET https://localhost:<puerto>/api/inventory/1` → `401 Unauthorized`.
3. Generar un JWT en jwt.io con:
   - `iss = ItmIdentityServer`
   - `aud = ItmStoreApis`
   - `secret = ITM-Super-Secret-Key-For-JWT-Class-2026-Nivel5`
4. Enviar la petición con header:
   - `Authorization: Bearer <token>` → `200 OK` si existe el producto.
