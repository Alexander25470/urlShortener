# URL Shortener

Solución al capítulo 8 del libro **"System Design Interview — An Insider's Guide"** implementada con .NET 10 + MongoDB.

---

## Requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (para modo completo con MongoDB)
- MongoDB 8+ (si se ejecuta sin Docker)

---

## Ejecución con Docker (recomendado)

Levanta la API + dos instancias de MongoDB (main + analytics) con un solo comando:

```bash
docker-compose up -d --build
```

Esto inicia tres servicios:
| Servicio | Puerto | Propósito |
|---|---|---|
| `api` | `8080` | API REST |
| `mongodb-main` | `27017` | URL mappings + contador |
| `mongodb-analytics` | `27018` | Click events (Time Series) |

Verificar que todo esté healthy:

```bash
docker-compose ps
```

---

## Ejecución sin Docker (solo API)

Requiere tener MongoDB corriendo en los puertos `27017` y `27018`.

```bash
# Restaurar paquetes
dotnet restore

# Compilar
dotnet build

# Ejecutar
dotnet run --project src/UrlShortener.Api --urls "http://localhost:8080"
```

---

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/data/shorten` | Acorta una URL |
| `GET` | `/api/v1/{shortCode}` | Redirige a la URL original |
| `GET` | `/health` | Health check |
| `GET` | `/metrics` | Métricas en formato Prometheus |

### Documentación interactiva

- **Scalar UI**: `http://localhost:8080/scalar/v1`
- **OpenAPI JSON**: `http://localhost:8080/openapi/v1.json`

---

## Ejemplos de uso

### Acortar una URL

```bash
curl -X POST http://localhost:8080/api/v1/data/shorten \
  -H "Content-Type: application/json" \
  -d '{"longUrl":"https://example.com/muy-larga-url"}'
```

Respuesta: `{ "shortUrl": "http://localhost:8080/0000001" }`

### Redirigir

```bash
curl -v http://localhost:8080/api/v1/0000001
```

Respuesta: `301 Moved Permanently → Location: https://example.com/muy-larga-url`

### Ver métricas

```bash
curl http://localhost:8080/metrics
```

---

## Tests

```bash
dotnet test
```

---

## Configuración vía variables de entorno

| Variable | Default | Descripción |
|---|---|---|
| `UrlShortener__BaseUrl` | `http://localhost:8080` | URL base para los short URLs |
| `UrlShortener__RedirectType` | `301` | Tipo de redirect: `301` (permanente) o `302` (temporal) |
| `MongoDB__MainConnectionString` | `mongodb://localhost:27017` | Conexión a MongoDB principal |
| `MongoDB__AnalyticsConnectionString` | `mongodb://localhost:27018` | Conexión a MongoDB de analytics |

---

## Arquitectura

Ver [DESIGN.md](DESIGN.md) para la documentación completa de decisiones de diseño.
