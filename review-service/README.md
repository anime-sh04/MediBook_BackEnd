# MediBook — Review / Rating Service

ASP.NET Core 8 Web API — manages **patient reviews and star ratings** for healthcare providers on the MediBook platform.

## Responsibilities

- **Add** — a patient submits a 1–5 star rating and comment after a completed appointment
- **One-per-appointment** — EF Core unique constraint on `AppointmentId` prevents duplicates
- **Update** — patient edits their review's rating and comment
- **Delete** — admin moderation removes inappropriate/fraudulent reviews; patient may retract
- **Average rating** — computes `AvgRating` per provider and pushes it to the Provider-Service via typed `IHttpClientFactory` client
- **Query** — by provider, patient, appointment, or all (admin)
- **Anonymous reviews** — `IsAnonymous = true` hides `PatientId` in API responses

## Architecture

```
ReviewController
      ↓
IReviewService (ReviewService)
      ↓                        ↓
IReviewRepository         IProviderClient ──HTTP──▶ Provider-Service :5001
(ReviewRepository)
      ↓
ReviewDbContext (EF Core / PostgreSQL)
```

| Layer | Class | Role |
|---|---|---|
| Entity | `Review` | POCO with factory, `Update()`, `Verify()`, domain guards |
| Repository | `IReviewRepository` / `ReviewRepository` | EF Core data access |
| HTTP Client | `IProviderClient` / `ProviderClient` | Pushes updated `AvgRating` to provider-service |
| Service | `IReviewService` / `ReviewService` | Business logic, orchestration |
| Controller | `ReviewController` | REST API — `[ApiController]` |

## API Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/api/v1/reviews` | Patient, Admin | Submit a new review |
| `GET` | `/api/v1/reviews/provider/{providerId}` | Anonymous | All reviews for a provider |
| `GET` | `/api/v1/reviews/patient/{patientId}` | Patient, Admin | All reviews by a patient |
| `GET` | `/api/v1/reviews/appointment/{appointmentId}` | Authenticated | Review for an appointment |
| `GET` | `/api/v1/reviews` | Admin | All reviews platform-wide |
| `PUT` | `/api/v1/reviews/{reviewId}` | Patient, Admin | Update rating and comment |
| `DELETE` | `/api/v1/reviews/{reviewId}` | Patient, Admin | Delete / moderate a review |
| `GET` | `/api/v1/reviews/provider/{providerId}/avg-rating` | Anonymous | Average rating + count |
| `GET` | `/api/v1/reviews/provider/{providerId}/count` | Anonymous | Review count for a provider |

## Database Schema

```sql
CREATE TABLE reviews (
    review_id      INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    appointment_id INTEGER NOT NULL,          -- UNIQUE (one review per appointment)
    patient_id     UUID    NOT NULL,
    provider_id    UUID    NOT NULL,
    rating         INTEGER NOT NULL,          -- CHECK (rating BETWEEN 1 AND 5)
    comment        VARCHAR(2000) NOT NULL,
    review_date    DATE    NOT NULL,
    is_verified    BOOLEAN NOT NULL DEFAULT false,
    is_anonymous   BOOLEAN NOT NULL DEFAULT false,

    CONSTRAINT ix_reviews_appointment_id_unique UNIQUE (appointment_id),
    CONSTRAINT ck_reviews_rating CHECK (rating BETWEEN 1 AND 5)
);
```

## Cross-Service Integration

After any write operation (add / update / delete), the service recomputes the average rating for the affected provider and calls:

```
PUT /api/v1/providers/{providerId}/rating
Body: { "avgRating": 4.67 }
```

on the **Provider-Service** to keep `AvgRating` in sync. This call is **best-effort** — a provider-service outage will be logged but will not fail the review operation.

## Running Locally

```bash
# 1. Set your connection string in appsettings.Development.json
# 2. Restore and run
dotnet restore
dotnet run --project src/MediBook.Review.API

# Swagger UI available at: http://localhost:5006
```

## Configuration

| Key | Description |
|---|---|
| `ConnectionStrings:ReviewDb` | PostgreSQL connection string |
| `JwtSettings:SecretKey` | Must match auth-service (min 32 chars) |
| `JwtSettings:Issuer` | `MediBook.Auth` |
| `JwtSettings:Audience` | `MediBook.Client` |
| `ServiceClients:ProviderServiceBaseUrl` | Base URL of the provider-service |

## EF Core Migrations

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> \
  --project src/MediBook.Review.API \
  --output-dir Migrations

# Apply migrations manually (auto-applied on startup in development)
dotnet ef database update --project src/MediBook.Review.API
```
