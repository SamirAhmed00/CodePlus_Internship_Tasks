# OrderFlow — Monitoring & Observability

Enterprise-grade Monitoring and Observability stack implemented on top of the
**OrderFlow** CQRS backend (.NET 10, EF Core 10 / SQL Server, Redis, MediatR).

This project equips the system with the complete **three pillars of observability**
(Metrics, Traces, Structured Logs) along with health probes, an automated Docker
Compose monitoring stack, a 7-panel Grafana dashboard, and provisioned Grafana alert
rules.

---

## Observability Architecture

```
                       ┌────────────────────────────┐
                       │  OrderFlow API (.NET 10)   │
                       │     (host port 5222)       │
                       └──────┬───────┬───────┬─────┘
          HTTP /metrics       │       │       │ OTLP / gRPC :4317
        (Prometheus Exporter) │       │       │ (OpenTelemetry .NET SDK)
                              │       │       ▼
                              │       │  ┌───────────────────────┐
                              ▼       │  │ Jaeger (all-in-one)   │
           ┌──────────────────────┐   │  │   UI: http://:16686   │
           │  Prometheus v3.15.0  │   │  └───────────────────────┘
           │   UI: http://:9090   │   │
           └──────────┬───────────┘   │ HTTP /health (JSON Health Report)
                      │               ▼
                      │  ┌───────────────────────────────┐
                      │  │ Health Endpoint               │
                      │  │ - SQL Server (DbContext check)│
                      │  │ - Redis PING                  │
                      │  │ - App liveness                │
                      │  └───────────────────────────────┘
                      │
                      ▼
         ┌─────────────────────────┐
         │     Grafana 13.2.2      │
         │    UI: http://:3000     │
         │ - OrderFlow Dashboard   │
         │ - Provisioned Alerting  │
         └────────────┬────────────┘
                      │ Scrapes :9121
                      ▼
         ┌─────────────────────────┐
         │     redis_exporter      │
         │   (:9121) ──► Redis     │
         └─────────────────────────┘
```

---

## Monitoring Stack Services (`docker-compose.yml`)

Run `docker compose up -d` to start the full observability infrastructure:

| Container Name | Service | Host Port | Description |
|---|---|---|---|
| `orderflow-grafana` | **Grafana 13.2.2** | `3000` | Pre-provisioned dashboards & alerts (`admin` / `admin`) |
| `orderflow-prometheus` | **Prometheus 3.15.0** | `9090` | Time-series scraper (5s scrape interval) |
| `orderflow-jaeger` | **Jaeger 1.76.0** | `16686` (UI), `4317` (OTLP gRPC), `4318` (OTLP HTTP) | Distributed trace backend & visualizer |
| `orderflow-redis` | **Redis 8-Alpine** | `6379` | Order details cache-aside store |
| `orderflow-redis-exporter` | **Redis Exporter 1.92.1** | `9121` | Prometheus exporter for Redis metrics |

> **Host networking:** The OrderFlow API runs directly on the Windows host (using Windows Integrated Auth with local SQL Server). Prometheus scrapes the host API via Docker's `host.docker.internal:5222` gateway bridge.


---

## 1. Metrics & Instrumentation

Implemented with native `System.Diagnostics.Metrics` (.NET OpenTelemetry SDK) and exported via `OpenTelemetry.Exporter.Prometheus.AspNetCore` on `/metrics`.

### Custom Application Metrics (Meter: `OrderFlow`)

| Metric Name | Type | Unit | Description & Labels |
|---|---|---|---|
| `orderflow_http_requests_total` | Counter | `{request}` | Total HTTP requests handled. Labels: `http_request_method`, `http_route`, `http_response_status_code` |
| `orderflow_http_request_duration_seconds` | Histogram | `s` | Request latency buckets (`0.005` to `10.0`s). Labels: `http_request_method`, `http_route` |
| `orderflow_http_errors_total` | Counter | `{error}` | Responses with status `>= 400` (4xx client + 5xx server). Same labels as request counter |
| `orderflow_orders_created_total` | Counter | `{order}` | Total orders created via `CreateOrderCommand` |
| `orderflow_orders_pending` | Observable Gauge | `{order}` | Current count of pending orders queried periodically from SQL Server |
| `orderflow_worker_cycles_total` | Counter | `{cycle}` | Processing cycles run by `OrderProcessingWorker`. Label: `outcome` (`success` / `error`) |
| `orderflow_worker_orders_completed_total` | Counter | `{order}` | Total orders transitioned from `Pending` to `Completed` by the worker |

### Redis Metrics (scraped via `redis-exporter`)

- `redis_up`: `1` if Redis is reachable, `0` when down.
- Redis memory, connected clients, commands processed, hit/miss ratios.

---

## 2. Distributed Tracing

Exported via OTLP (`http://localhost:4317`) into **Jaeger** using `OpenTelemetry.Exporter.OpenTelemetryProtocol`.

- **ActivitySource:** `OrderFlow`
- **Tracing Pipeline Behavior:** MediatR `TracingBehavior<TRequest, TResponse>` wraps every Command and Query in an Activity:
  - `Create Order` (tag: `mediatr.request.type`)
  - `Get Order By ID` (tags: `mediatr.request.type`, `order.id`)
  - `Get Orders`
  - `Get Dashboard Orders`
- **Cache Spans:** Hand-crafted Activities inside `RedisOrderCache`:
  - `cache get` (tags: `cache.key`, `cache.hit: true|false`)
  - `cache set` (tag: `cache.key`)
  - `cache remove` (tag: `cache.key`)
- **Worker Cycle Activity:** `OrderProcessingWorker` creates a root `Worker Cycle` activity with tags:
  - `worker.completed_orders`
  - `worker.dashboard_rows`
- **Automated Instrumentations:**
  - `AddAspNetCoreInstrumentation`: filters out `/metrics` and `/health` to keep traces clean.
  - `AddSqlClientInstrumentation`: records raw EF Core / SQL queries (with duration, status, and connection metadata) as child spans under their respective MediatR activity.

Trace visualization is accessible at `http://localhost:16686` (Search Service: `orderflow-api`).


---

## 3. Health Checks (`/health`)

Standard ASP.NET Core Health Checks mapped to a custom JSON response writer at `/health`:

```http
GET /health
```

### Response Example (Healthy)

```json
{
  "status": "Healthy",
  "totalDurationMs": 4.2,
  "checks": [
    { "name": "application", "status": "Healthy", "durationMs": 0.0, "error": null },
    { "name": "sql-server", "status": "Healthy", "durationMs": 2.1, "error": null },
    { "name": "redis", "status": "Healthy", "durationMs": 2.1, "error": null }
  ]
}
```

When Redis is stopped:
- Endpoint returns HTTP `503 Service Unavailable`.
- `"status": "Unhealthy"`.
- `"checks": [ ..., { "name": "redis", "status": "Unhealthy", "error": "The message timed out..." } ]`.
- **OrderFlow API keeps serving queries gracefully** via SQL fallback (graceful degradation verified).

---

## 4. Grafana Dashboard

A production dashboard is auto-provisioned into Grafana from:
`monitoring/grafana/dashboards/orderflow.json`

UID: `orderflow-monitoring` | Folder: `OrderFlow`

### Panels (7 total)

1. **HTTP Request Count** (`timeseries`): aggregate rate and volume of incoming HTTP traffic.
2. **HTTP Latency Percentiles (p50 & p95)** (`timeseries`): computed using Prometheus `histogram_quantile` over `orderflow_http_request_duration_seconds_bucket`.
3. **HTTP Error Rate** (`timeseries`): separates `4xx (Client Error)` and `5xx (Server Error)` rate curves.
4. **Orders Created** (`stat`): aggregate total orders created through the system.
5. **Pending Orders** (`stat`): real-time gauge of orders currently awaiting worker completion.
6. **Background Worker Activity** (`timeseries`): worker cycles completed vs. pending orders transitioned.
7. **Redis Availability** (`stat`): displays `UP` (green) or `DOWN` (red) based on `min(redis_up)`.

---

## 5. Grafana Alert Rules

Pre-provisioned into Grafana Alerting from:
`monitoring/grafana/provisioning/alerting/alert-rules.yml`

Folder: `OrderFlow` | Evaluation Group: `orderflow-alerts` (Interval: `10s`)

| Alert Name | Severity | Condition | For Duration | Actionable Summary |
|---|---|---|---|---|
| **High Error Rate** | `critical` | 5xx error rate > 5% over 5m: `sum(rate(orderflow_http_errors_total{http_response_status_code=~"5.."}[5m])) / clamp_min(sum(rate(orderflow_http_requests_total[5m])), 0.001) > 0.05` | 2m | Triggers when unhandled 5xx exceptions surge. Points the operator to Jaeger traces and logs. |
| **Redis Unavailable** | `warning` | `min(redis_up) < 1` (or `NoData` state) | 1m | Triggers when Redis container stops or exporter fails. Signals cache miss storm on SQL Server. |

Both alert rules were end-to-end verified by producing intentional 500 error bursts and by executing `docker stop orderflow-redis`. Both alerts transition cleanly to `Alerting` and recover to `Normal` once healthy.


---

## Quick Start & Verification

### 1. Prerequisites
- .NET 10 SDK
- Docker Desktop (with Docker Compose v2)
- SQL Server running locally with `OrderFlow` database (or let EF apply migrations)

### 2. Start Monitoring Stack
```powershell
docker compose up -d
```

Verify all 5 containers are up:
```powershell
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

### 3. Run the OrderFlow API
```powershell
dotnet run --project src/OrderFlow.Api
```

The API starts on `http://localhost:5222`.

### 4. Verify Endpoints
- **Health:** `curl http://localhost:5222/health`
- **Prometheus Metrics:** `curl http://localhost:5222/metrics`
- **Prometheus UI:** [http://localhost:9090](http://localhost:9090)
- **Jaeger UI:** [http://localhost:16686](http://localhost:16686)
- **Grafana UI:** [http://localhost:3000](http://localhost:3000) (`admin` / `admin`)
  - Go to **Dashboards** → **OrderFlow** → **OrderFlow - Monitoring & Observability**
  - Go to **Alerts & Incidents** → **Alert rules** → **orderflow-alerts**

### 5. Generate Test Traffic
```powershell
# Create an order
curl -X POST http://localhost:5222/api/orders `
  -H "Content-Type: application/json" `
  -d '{"customerName":"Alice","items":[{"productName":"Keyboard","quantity":1,"unitPrice":75.00}]}'

# Read back (populates Redis cache)
curl http://localhost:5222/api/orders

# Read dashboard materialized table
curl http://localhost:5222/api/dashboard/orders
```

---

## Project Structure

```
├── docker-compose.yml                              # Redis, Prometheus, Jaeger, Grafana, redis-exporter
├── monitoring/
│   ├── prometheus/
│   │   └── prometheus.yml                          # Scrape configs for OrderFlow API & Redis
│   └── grafana/
│       ├── dashboards/
│       │   └── orderflow.json                      # 7-panel production Grafana dashboard
│       └── provisioning/
│           ├── alerting/
│           │   └── alert-rules.yml                 # Provisioned alert rules (High Error Rate, Redis Down)
│           ├── dashboards/
│           │   └── dashboards.yml                  # Dashboard provisioning provider
│           └── datasources/
│               └── datasources.yml                 # Prometheus & Jaeger datasource definitions
└── src/
    ├── OrderFlow.Domain/                           # Domain entities, rules, exceptions
    ├── OrderFlow.Application/
    │   ├── Observability/
    │   │   ├── OrderFlowDiagnostics.cs             # ActivitySource & Meter definitions
    │   │   ├── OrderFlowMetrics.cs                 # Custom metrics implementation
    │   │   └── TracingBehavior.cs                  # MediatR pipeline tracing behavior
    │   └── ...                                     # CQRS commands and queries
    ├── OrderFlow.Infrastructure/
    │   ├── BackgroundServices/
    │   │   └── OrderProcessingWorker.cs            # Traced worker + cycle metrics
    │   ├── Caching/
    │   │   └── RedisOrderCache.cs                  # Traced cache get/set/remove + SQL fallback
    │   └── Persistence/
    └── OrderFlow.Api/
        ├── Observability/
        │   ├── HttpMetricsMiddleware.cs            # Request rate, route, status, duration metrics
        │   └── ObservabilityExtensions.cs          # OpenTelemetry, health checks, & exporter wiring
        └── ...
```

