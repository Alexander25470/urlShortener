# URL Shortener

Solución al capítulo 8 del libro **"System Design Interview — An Insider's Guide"** implementada con .NET 10 + MongoDB + React.

---

## Requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (recomendado)
- [Node.js 22+](https://nodejs.org/) (para frontend sin Docker)
- MongoDB 8+ (para back sin Docker)

---

## Stack completo (Docker)

```bash
docker-compose up -d --build
```

| Servicio | Puerto | Propósito |
|---|---|---|
| `front` | `80` | Frontend React (Vite + Nginx) |
| `api` | `8080` | API REST .NET |
| `mongodb-main` | `27017` | URL mappings + contador |
| `mongodb-analytics` | `27018` | Click events (Time Series) |

Verificar:

```bash
docker-compose ps
# http://localhost        → frontend
# http://localhost:8080   → API directa
```

---

## Solo back (API + MongoDB)

Requiere MongoDB corriendo en `27017` y `27018`.

```bash
cd back
dotnet restore
dotnet build
dotnet run --project src/UrlShortener.Api --urls "http://localhost:8080"
```

Variables de entorno relevantes:

| Variable | Default |
|---|---|
| `MongoDB__MainConnectionString` | `mongodb://localhost:27017` |
| `MongoDB__AnalyticsConnectionString` | `mongodb://localhost:27018` |
| `UrlShortener__BaseUrl` | `http://localhost:8080` |
| `Cors__Origins__0` | `http://localhost` |

---

## Solo front

Requiere el backend corriendo en `http://localhost:8080`.

```bash
cd front
npm install
npm run dev        # http://localhost:5173
```

La URL del back se configura en `front/.env`:

```env
VITE_API_URL=http://localhost:8080
```

---

## Tests

```bash
# Back (.NET)
cd back && dotnet test

# Front (Vite + Vitest)
cd front && npm test
```

---

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/data/shorten` | Acorta una URL |
| `GET` | `/api/v1/{shortCode}` | Redirige a la URL original |
| `GET` | `/api/v1/urls` | Lista todas las URLs acortadas |
| `GET` | `/api/v1/{shortCode}/clicks` | Clicks por URL (con bucket) |
| `GET` | `/api/v1/analytics/top` | Top URLs más visitadas |
| `GET` | `/health` | Health check |
| `GET` | `/metrics` | Métricas Prometheus |

### Documentación interactiva

- **Scalar UI**: `http://localhost:8080/scalar/v1`
- **OpenAPI JSON**: `http://localhost:8080/openapi/v1.json`

---

## Ejemplos de uso

```bash
# Acortar
curl -X POST http://localhost:8080/api/v1/data/shorten \
  -H "Content-Type: application/json" \
  -d '{"longUrl":"https://example.com/muy-larga-url"}'

# Redirigir
curl -v http://localhost:8080/api/v1/0000001

# Clicks (buckets diarios)
curl "http://localhost:8080/api/v1/0000001/clicks?bucket=day"

# Top URLs
curl "http://localhost:8080/api/v1/analytics/top?limit=10"

# Métricas
curl http://localhost:8080/metrics
```

---

## Arquitectura

Ver [DESIGN.md](DESIGN.md) para documentación completa de decisiones de diseño.

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
