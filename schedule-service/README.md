# MediBook — Schedule Service

ASP.NET Core 8 Web API that manages provider availability slots for the MediBook platform.

## Responsibilities

- **Slot CRUD** — add, read, update, delete individual slots
- **Bulk creation** — create up to 500 slots in a single request
- **Slot state machine** — Available → Booked → Released | Blocked → Unblocked
- **Recurring generation** — daily or weekly slot patterns between a date range
- **Patient-facing query** — exposes only unbooked, unblocked slots

## Architecture

```
ScheduleController  →  IScheduleService (ScheduleService)
                              ↓
                     ISlotRepository (SlotRepository)
                              ↓
                       ScheduleDbContext (EF Core / PostgreSQL)
```

| Layer | Class | Role |
|---|---|---|
| Entity | `AvailabilitySlot` | POCO with encapsulated state transitions |
| Repository | `ISlotRepository` / `SlotRepository` | Data access |
| Service | `IScheduleService` / `ScheduleService` | Business logic |
| Controller | `ScheduleController` | HTTP surface, `/api/v1/slots` |

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/v1/slots` | Provider/Admin | Add a single slot |
| POST | `/api/v1/slots/bulk` | Provider/Admin | Bulk-create slots |
| POST | `/api/v1/slots/generate-recurring` | Provider/Admin | Generate recurring slots |
| GET | `/api/v1/slots/provider/{providerId}` | — | All slots for a provider |
| GET | `/api/v1/slots/available?providerId=&date=` | — | Available slots (patient-facing) |
| GET | `/api/v1/slots/{id}` | — | Single slot by ID |
| PUT | `/api/v1/slots/{id}` | Provider/Admin | Update slot time |
| PUT | `/api/v1/slots/{id}/block` | Provider/Admin | Block a slot |
| PUT | `/api/v1/slots/{id}/unblock` | Provider/Admin | Unblock a slot |
| PUT | `/api/v1/slots/{id}/book` | Patient/Admin | Book a slot |
| PUT | `/api/v1/slots/{id}/unbook` | Patient/Admin | Release a booked slot |
| DELETE | `/api/v1/slots/{id}` | Provider/Admin | Delete a slot |
| GET | `/api/v1/slots/health` | — | Health check |

## Slot State Machine

```
[Available] ──book──▶ [Booked] ──unbook──▶ [Available]
[Available] ──block──▶ [Blocked] ──unblock──▶ [Available]
[Booked]   ──block──▶ [Blocked]   (auto-releases booking)
```

## Running Locally

```bash
# 1. Start PostgreSQL
docker run -e POSTGRES_PASSWORD=password -e POSTGRES_DB=medibook_schedule -p 5434:5432 postgres:15-alpine

# 2. Run the service
cd src/MediBook.Schedule.API
dotnet run
```

Swagger UI is available at `http://localhost:5002` (root path).

## Docker

```bash
docker build -t medibook-schedule .
docker run -p 5002:8080 \
  -e ConnectionStrings__ScheduleDb="Host=host.docker.internal;Database=medibook_schedule;Username=postgres;Password=password" \
  -e JwtSettings__SecretKey="your-32-char-min-secret-key-here!!" \
  medibook-schedule
```

## Environment Variables

| Variable | Description |
|---|---|
| `ConnectionStrings__ScheduleDb` | PostgreSQL connection string |
| `JwtSettings__SecretKey` | Must match the auth-service secret (≥ 32 chars) |
| `JwtSettings__Issuer` | Default: `MediBook.Auth` |
| `JwtSettings__Audience` | Default: `MediBook.Client` |

## Migrations

EF Core migrations run automatically on startup. To add a new migration manually:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/MediBook.Schedule.API \
  --output-dir Migrations
```
