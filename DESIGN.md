# URL Shortener — Documento de Diseño

## Planteamiento del Problema

Sistema extraído del libro **"System Design Interview — An Insider's Guide" (Volumen 1), Capítulo 8: Design A URL Shortener**.

Un acortador de URLs crea un alias corto (ej. `https://short.domain/zn9edcu`) a partir de una URL larga. Cuando un usuario hace clic en la URL corta, es redirigido a la URL original larga.

### Requisitos

| Requisito | Detalle |
|---|---|
| Acortamiento | `POST /api/v1/data/shorten` → devuelve URL corta |
| Redirección | `GET /{shortCode}` (o `GET /api/v1/{shortCode}`) → redirect 301/302 a la URL larga |
| Caracteres permitidos | `[0-9, a-z, A-Z]` (62 caracteres posibles) |
| Longitud de URL corta | 7 caracteres (62⁷ ≈ 3.5 billones, suficiente para 365B registros) |
| Volumen de escritura | 100 millones de URLs/día ≈ 1.160 escrituras/s |
| Volumen de lectura | Proporción 10:1 lectura/escritura ≈ 11.600 lecturas/s |
| Almacenamiento (10 años) | 365 mil millones de registros ≈ 365 TB |
| URLs inmutables | No se pueden eliminar ni actualizar |
| Tipo de redirect configurable | 301 (permanente) o 302 (temporal) vía variable de entorno |

---

## Visión General de la Arquitectura

### Diagrama del Sistema

```mermaid
graph TD
    Client[Navegador] -->|http://localhost| Front[front Nginx]
    Front -->|/api/* /health /metrics| API[.NET API - UrlShortener.Api]
    Client -->|http://localhost:8080| API

    API --> MainDB[(mongodb-main<br/>WiredTiger Cache #1)]
    API --> AnalyticsDB[(mongodb-analytics<br/>WiredTiger Cache #2)]

    subgraph MainDB["mongodb-main (puerto 27017)"]
        UrlMappings[(url_mappings<br/>shortCode ↔ longUrl)]
        Counters[(counters<br/>secuencia atómica de IDs)]
    end

    subgraph AnalyticsDB["mongodb-analytics (puerto 27018)"]
        Clicks[(clicks<br/>Time Series Collection)]
    end

    API -->|/metrics| Prometheus[Prometheus Scraper]
```

### Preparado para División en Microservicios

La separación por responsabilidades (CQRS + bases de datos aisladas) deja el diseño listo para dividirse en 3 microservicios independientes:

| Microservicio | Responsabilidad | Persistencia | Interfaces |
|---|---|---|---|
| **🔗 URL Shortener** | Acortar URLs (escribir `url_mappings` + `counters`) | mongodb-main | `IUrlShortenerCommand.ShortenAsync` |
| **↪️ Redirect** | Resolver shortCode → longUrl (leer `url_mappings`) | mongodb-main | `IUrlMappingQuery.GetLongUrlAsync` |
| **📊 Analytics** | Registrar clicks + consultas analíticas (leer/escribir `clicks`) | mongodb-analytics | `IUrlShortenerCommand.RecordClickAsync` + `IUrlAnalyticsQuery` |

Cada microservicio sería un proceso independiente con su propio `WebApplication`, su propio pool de conexiones a MongoDB y su propio despliegue Docker. Las interfaces ya están definidas y aisladas — no hay dependencias circulares ni acoplamiento oculto. La comunicación entre ellos sería vía HTTP interno (o un message broker si se requiere mayor desacoplamiento).

Los beneficios de dividir serían:
- **Escalado independiente** — Redirect requiere más réplicas (10:1 lectura/escritura), Analytics puede escalar según volumen de clicks.
- **Aislamiento de fallos** — Una sobrecarga en Analytics no afecta los redirects.
- **Equipos independientes** — Cada servicio puede ser mantenido por un equipo distinto.

### Flujo de Acortamiento

```mermaid
sequenceDiagram
    participant C as Cliente
    participant API as API
    participant M as mongodb-main
    participant A as mongodb-analytics

    C->>API: POST /api/v1/data/shorten { longUrl }
    API->>M: Buscar por longUrl (verificación de idempotencia)
    M-->>API: shortCode existente o null

    alt URL ya existe
        API-->>C: 200 { shortUrl: "..." }
    else URL nueva
        API->>M: FindOneAndUpdate({ _id: "url_id" }, $inc: { seq: 1 })
        M-->>API: newId
        API->>API: Base62.Encode(newId) → shortCode (7 caracteres)
        API->>M: Insertar { Id, ShortCode, LongUrl, CreatedAt }
        API-->>C: 200 { shortUrl: "https://short.domain/{shortCode}" }
    end
```

### Flujo de Redirección

```mermaid
sequenceDiagram
    participant C as Cliente
    participant API as API
    participant M as mongodb-main
    participant A as mongodb-analytics

    C->>API: GET /{shortCode} (o GET /api/v1/{shortCode})
    API->>API: Base62.TryDecode(shortCode)
    alt shortCode inválido
        API-->>C: 400 Bad Request
    else Formato válido
        API->>M: Buscar por shortCode
        alt No encontrado
            API-->>C: 404 Not Found
        else Encontrado
            API->>A: Insertar ClickEvent { shortCode, timestamp }
            API-->>C: 301/302 Redirect → Location: longUrl
        end
    end
```

---

## Decisiones de Diseño

| Decisión | Opción elegida | Justificación |
|---|---|---|
| **Función hash** | Conversión Base 62 | Se elige Base62 porque: (1) **Sin colisiones** — es biyectiva, cada ID numérico genera exactamente un shortCode. No necesita resolución de colisiones ni Bloom filters. (2) **URL-safe por construcción** — usa solo `[0-9a-zA-Z]`. A diferencia de Base64 (que tiene `/`, `+`, `=`), ningún caracter necesita percent-encoding. El `/` rompería el ruteo y `+` se interpreta como espacio en query strings. (3) **Longitud predecible** — con 7 caracteres (62⁷ ≈ 3.5 billones) se cubren los 365 mil millones de URLs estimados para 10 años. |
| **Motor de almacenamiento** | MongoDB (WiredTiger) | WiredTiger provee cache integrada (por defecto 50% de RAM - 1GB), eliminando la necesidad de una capa separada de Redis. Simplifica el stack manteniendo lecturas rápidas a esta escala (~11.6K req/s). |
| **Dos instancias mongod vs. una con dos colecciones** | Dos procesos `mongod` independientes | **Decisión arquitectónica clave.** WiredTiger asigna su cache por proceso `mongod`, no por base de datos. Si `clicks` (alto volumen de escritura) y `url_mappings` (alto volumen de lectura) compartieran la misma cache, los inserts desplazarían páginas de URL mappings, degradando la latencia de redirects. Dos instancias separadas garantizan aislamiento total de cache, cada una con su propio pool WiredTiger + filesystem cache del SO. Se ejecutan en la misma red Docker (latencia submilisegundo) y requieren recursos extra mínimos. Es el diseño correcto para producción — el costo de un contenedor extra es despreciable vs. el costo de contención de cache a escala. |
| **Generación de IDs** | Contador atómico MongoDB (`$inc`) | Para una configuración de un solo mongod, un contador atómico en una colección `counters` dedicada es la solución correcta más simple. Es un solo `FindOneAndUpdate` + `$inc` — sin dependencias externas, sin sincronización de reloj. Snowflake sería necesario solo en un entorno clusterizado/shardeado. Complejidad hacia abajo: quien llama no sabe ni le importa cómo se generan los IDs. |
| **Analytics de clicks** | Time Series Collection (mongod separado) | Un contador simple por URL es insuficiente para análisis de series temporales. Almacenar eventos individuales `{ shortCode, timestamp }` en una Time Series Collection de MongoDB permite consultas como clicks por hora/día, URLs con tendencia, etc. Las Time Series Collections usan compresión zstd (~70% de ahorro de espacio) y bucketing automático. El mongod separado evita que las escrituras de analytics contaminen la cache principal. |
| **Tipo de redirect** | Configurable (301/302) vía env var | 301 (permanente, cacheado por el navegador, menos carga en el servidor) vs. 302 (temporal, más amigable para analytics). Hacerlo configurable permite al operador elegir por despliegue sin cambios de código. |
| **Estilo de API** | Controllers (MVC) | Los Controllers proveen un enfoque más estructurado para equipos, con separación clara de definiciones de ruta, enlace de modelos y atributos de validación. Patrón familiar en el ecosistema .NET. |
| **Manejo de errores** | Estrategia de 3 capas (validación → nullable → middleware) | `null` es suficiente para "no encontrado" en este dominio. El prefijo `Try*` para validación de formato evita excepciones. Un middleware global de exception handler atrapa errores inesperados y devuelve ProblemDetails. `Result<T>` agrega ceremonia sin valor para un servicio con 2 métodos. *Define errors out of existence:* validar en el borde para que los datos inválidos nunca lleguen al núcleo. |
| **Métricas** | `System.Diagnostics.Metrics` + OpenTelemetry Prometheus exporter | Sin vendor lock-in, API estándar de .NET, expone `/metrics` en formato Prometheus. Funciona con cualquier stack de observabilidad. El exporter de Prometheus es liviano — no necesita agente separado. |
| **Docker** | Build multi-stage + docker-compose | Entorno reproducible. Docker Compose orquesta los cuatro servicios (API + 2x MongoDB + frontend Nginx). Healthchecks aseguran el orden de inicio correcto. El frontend usa Nginx para servir estáticos y hacer proxy reverso a la API, eliminando problemas de CORS en producción. |
| **CORS** | `AddCors()` con origen desde config | Se configuró CORS en la API con orígenes desde variables de entorno. En producción, el proxy reverso de Nginx evita la necesidad de CORS, pero tenerlo configurado no afecta. Es la opción más realista: el equipo de front puede desarrollar contra la API directamente sin depender del proxy. |

---

## Estrategia de Métricas

### Capa 1 — Métricas Operacionales (en memoria, vía OpenTelemetry)

Expuestas en `GET /metrics` en formato Prometheus. Para alertas de tasa, SLOs de latencia y planificación de capacidad.

| Métrica | Tipo | Qué mide |
|---|---|---|
| `urlshortener_shorten_duration_milliseconds` | Histograma | Latencia de acortamiento. `_count` da el total de URLs acortadas. |
| `urlshortener_redirect_duration_milliseconds` | Histograma | Latencia de redirects. `_count` da el total de redirects servidos. |

### Capa 2 — Datos de Analytics (persistidos, en MongoDB)

Cada redirect inserta un documento `ClickEvent` en la Time Series Collection:

```json
{
  "shortCode": "zn9edcu",
  "timestamp": "2026-06-12T14:30:00Z"
}
```

Esto permite consultas de series temporales: clicks por hora/día, URLs principales, análisis de tendencias.

---

## Modelo de Datos

### `urlshortener.url_mappings`

```json
{
  "_id": 2009215674938,
  "shortCode": "zn9edcu",
  "longUrl": "https://en.wikipedia.org/wiki/Systems_design",
  "createdAt": "2026-06-12T10:00:00Z"
}
```

Índices:
- `{ shortCode: 1 }` (único) — búsqueda rápida de redirect
- `{ longUrl: 1 }` (único) — verificación rápida de idempotencia

### `urlshortener.counters`

```json
{ "_id": "url_id", "seq": 2009215674938 }
```

### `urlshortener_analytics.clicks` (Time Series Collection)

Configuración Time Series:
- `timeField`: `"timestamp"`
- `metaField`: `"shortCode"`
- `granularity`: `"seconds"`

---

## Estrategia de Manejo de Errores

| Capa | Mecanismo | Escenario de ejemplo |
|---|---|---|
| **1. Validación en borde** (Controller) | Data annotations (`[Required]`, `[Url]`), `ModelState.IsValid`, `Base62Converter.TryDecode` | URL malformada → 400 antes de tocar el servicio |
| **2. Tipos seguros** (Service) | `TryDecode` en vez de lanzar excepción, retornos nullable en vez de excepciones | shortCode inválido → `false`, no un `FormatException` |
| **3. Middleware global** (Pipeline) | `UseExceptionHandler` + `ProblemDetails` | MongoDB inalcanzable → 500 con detalle estructurado (verbose en development) |

Sin `Result<T>`, sin `OneOf`, sin tipos de excepción personalizados. *Define errors out of existence:* el controller atrapa errores de formato antes de que se propaguen; el servicio usa tipos que hacen imposibles los estados inválidos; el middleware es la red de seguridad para fallos realmente inesperados.

---

## Referencia de API

### POST /api/v1/data/shorten

Crea una URL corta para la URL larga dada. Idempotente — llamar con la misma `longUrl` devuelve la misma `shortUrl`.

**Solicitud:**
```json
{ "longUrl": "https://en.wikipedia.org/wiki/Systems_design" }
```

**Éxito (200):**
```json
{ "shortUrl": "http://localhost:8080/zn9edcu" }
```

**Error de validación (400):**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "longUrl": ["The longUrl field is not a valid fully-qualified URL."] }
}
```

### GET /{shortCode} (y /api/v1/{shortCode})

Redirige a la URL larga original. Ambas rutas funcionan:
- `GET http://localhost:8080/zn9edcu` — ruta canónica
- `GET http://localhost:8080/api/v1/zn9edcu` — ruta versionada (compatible con otros endpoints)

| Estado | Cuándo | Respuesta |
|---|---|---|
| **301 o 302** | shortCode existe | `Location: <longUrl>` (sin cuerpo) |
| **400** | Formato de shortCode inválido | ProblemDetails |
| **404** | Formato válido pero no encontrado | ProblemDetails |

### GET /health

**200 OK** — Usado por el healthcheck de Docker y sondas del balanceador de carga.

### GET /api/v1/urls

Lista todas las URLs acortadas, ordenadas de más reciente a más antigua.

**Éxito (200):**
```json
[
  { "shortCode": "zn9edcu", "longUrl": "https://...", "createdAt": "2026-06-12T10:00:00Z" },
  ...
]
```

### GET /api/v1/{shortCode}/clicks

Devuelve el conteo de clicks de una URL agrupado por unidad de tiempo. Ideal para gráficos.

**Parámetros query:**

| Parámetro | Tipo | Default | Descripción |
|---|---|---|---|
| `from` | `DateTime` | `now - 30d` | Inicio del rango |
| `to` | `DateTime` | `now` | Fin del rango |
| `bucket` | `string` | `"hour"` | Unidad de agrupación: `minute`, `hour` o `day` |

**Éxito (200):**
```json
{
  "shortCode": "zn9edcu",
  "from": "2026-06-01T00:00:00Z",
  "to": "2026-06-12T00:00:00Z",
  "bucket": "day",
  "data": [
    { "timestamp": "2026-06-01T00:00:00Z", "count": 15 },
    { "timestamp": "2026-06-02T00:00:00Z", "count": 42 }
  ]
}
```

**Error (400):** shortCode inválido o `bucket` no es `minute`, `hour` o `day`.

### GET /api/v1/analytics/top

Devuelve las URLs más clickeadas en un rango de tiempo.

**Parámetros query:**

| Parámetro | Tipo | Default | Descripción |
|---|---|---|---|
| `limit` | `int` | `10` | Cantidad de resultados (máx. 100) |
| `from` | `DateTime` | `now - 7d` | Inicio del rango |
| `to` | `DateTime` | `now` | Fin del rango |

**Éxito (200):**
```json
{
  "from": "2026-06-05T00:00:00Z",
  "to": "2026-06-12T00:00:00Z",
  "limit": 5,
  "data": [
    { "shortCode": "zn9edcu", "longUrl": "https://...", "clickCount": 150 },
    ...
  ]
}
```

---

## Desarrollo Local

```bash
# Compilar
cd back && dotnet build

# Ejecutar tests
cd back && dotnet test

# Iniciar todos los servicios
docker-compose up -d

# Probar acortamiento
curl -X POST http://localhost:8080/api/v1/data/shorten \
  -H 'Content-Type: application/json' \
  -d '{"longUrl":"https://example.com/a-very-long-url"}'

# Probar redirect
curl -v http://localhost:8080/api/v1/zn9edcu

# Ver métricas
curl http://localhost:8080/metrics

# Ver eventos de click en la BD de analytics
mongosh mongodb://localhost:27018/urlshortener_analytics \
  --eval 'db.clicks.countDocuments({shortCode:"zn9edcu"})'
```

---

## Referencias

1. Alex Xu — *System Design Interview — An Insider's Guide* (Volumen 1), Capítulo 8: Design A URL Shortener
2. John Ousterhout — *A Philosophy of Software Design* (2ª Edición)
3. MongoDB — [Time Series Collections](https://www.mongodb.com/docs/manual/core/timeseries-collections/)
4. MongoDB — [WiredTiger Storage Engine](https://www.mongodb.com/docs/manual/core/wiredtiger/)
5. OpenTelemetry .NET — [Metrics API](https://opentelemetry.io/docs/languages/net/instrumentation/)
