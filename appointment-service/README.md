# MediBook — Appointment Service

ASP.NET Core 8 Web API — the **core orchestration service** of the MediBook platform. Manages the full booking lifecycle and coordinates with the Schedule-Service and Payment-Service.

## Responsibilities

- **Book** — creates an appointment and marks the slot as booked in the Schedule-Service
- **Cancel** — transitions to Cancelled, releases the slot, triggers a refund via Payment-Service
- **Reschedule** — atomically swaps old slot → new slot and updates the appointment record
- **Complete / No-Show** — terminal status transitions driven by providers
- **Query** — by patient, provider, date, status, and upcoming

## Architecture

```
AppointmentController
        ↓
IAppointmentService (AppointmentService)
        ↓                         ↓
IAppointmentRepository      IScheduleClient ──HTTP──▶ Schedule-Service :5002
(AppointmentRepository)     IPaymentClient  ──HTTP──▶ Payment-Service  (stub)
        ↓
AppointmentDbContext (EF Core / PostgreSQL)
```

| Layer | Class | Role |
|---|---|---|
| Entity | `Appointment` | POCO with encapsulated state machine |
| Repository | `IAppointmentRepository` / `AppointmentRepository` | EF Core data access |
| HTTP Client | `IScheduleClient` / `ScheduleClient` | Typed client for schedule-service |
| HTTP Stub | `IPaymentClient` / `PaymentClientStub` | Replace when payment-service is live |
| Service | `IAppointmentService` / `AppointmentService` | Orchestration + business logic |
| Controller | `AppointmentController` | `/api/v1/appointments` endpoints |

## Status State Machine

```
                ┌──────────────┐
                │  Scheduled   │◀──────────────────────────┐
                └──────┬───────┘                           │
          ┌────────────┼─────────────┐                     │
          ▼            ▼             ▼                      │
    ┌──────────┐  ┌─────────┐  ┌─────────┐    (reschedule re-enters Scheduled
    │Cancelled │  │Completed│  │ No-Show │     on the new slot)
    └──────────┘  └─────────┘  └─────────┘
     (terminal)    (terminal)   (terminal)
```

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/v1/appointments` | Patient/Admin | Book a new appointment |
| GET | `/api/v1/appointments/{id}` | Any auth | Get by ID |
| GET | `/api/v1/appointments/patient/{patientId}` | Patient/Admin | All appointments for patient |
| GET | `/api/v1/appointments/provider/{providerId}` | Provider/Admin | All appointments for provider |
| GET | `/api/v1/appointments/provider/{providerId}/date/{date}` | Provider/Admin | By provider + date |
| GET | `/api/v1/appointments/patient/{patientId}/upcoming` | Patient/Admin | Upcoming (Scheduled, future) |
| PUT | `/api/v1/appointments/{id}/cancel` | Patient/Provider/Admin | Cancel appointment |
| PUT | `/api/v1/appointments/{id}/reschedule` | Patient/Admin | Reschedule to new slot |
| PUT | `/api/v1/appointments/{id}/complete` | Provider/Admin | Mark as completed |
| PUT | `/api/v1/appointments/{id}/status` | Admin | Generic status override |
| GET | `/api/v1/appointments/provider/{providerId}/count` | Provider/Admin | Total count for provider |
| GET | `/api/v1/appointments/health` | — | Health check |

## Running Locally

```bash
# 1. Start PostgreSQL
docker run -e POSTGRES_PASSWORD=password -e POSTGRES_DB=medibook_appointment -p 5435:5432 postgres:15-alpine

# 2. Make sure schedule-service is running on :5002

# 3. Run
cd src/MediBook.Appointment.API
dotnet run
```

Swagger UI: `http://localhost:5003`

## Docker

```bash
docker build -t medibook-appointment .
docker run -p 5003:8080 \
  -e ConnectionStrings__AppointmentDb="Host=host.docker.internal;Database=medibook_appointment;Username=postgres;Password=password" \
  -e JwtSettings__SecretKey="your-32-char-min-secret-key-here!!" \
  -e ServiceClients__ScheduleServiceBaseUrl="http://schedule-service:8080" \
  medibook-appointment
```

## Environment Variables

| Variable | Description |
|---|---|
| `ConnectionStrings__AppointmentDb` | PostgreSQL connection string |
| `JwtSettings__SecretKey` | Must match auth-service (≥ 32 chars) |
| `JwtSettings__Issuer` | Default: `MediBook.Auth` |
| `JwtSettings__Audience` | Default: `MediBook.Client` |
| `ServiceClients__ScheduleServiceBaseUrl` | Base URL of schedule-service |

## Cross-Service Communication

The service uses **`IHttpClientFactory`** (typed client pattern) to call the Schedule-Service:

| Action | HTTP Call |
|---|---|
| Book | `PUT /api/v1/slots/{slotId}/book` |
| Cancel / Reschedule (release old) | `PUT /api/v1/slots/{slotId}/unbook` |
| Validate slot before booking | `GET /api/v1/slots/{slotId}` |

### Payment-Service (stub)
`IPaymentClient` is currently a no-op stub that logs the refund intent. Replace `PaymentClientStub` with a real typed HTTP client once the payment-service is built.

## Migrations

EF Core migrations run automatically on startup. To add a new migration:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/MediBook.Appointment.API \
  --output-dir Migrations
```
