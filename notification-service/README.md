# MediBook — Notification Service

> **Stack:** .NET 8 · ASP.NET Core Web API · Entity Framework Core 8 · PostgreSQL · MailKit (SMTP) · SignalR

---

## Overview

The Notification Service is one of the nine independently deployable microservices in the MediBook platform. It is responsible for:

| Channel | Technology | Notes |
|---------|-----------|-------|
| **In-App (real-time)** | ASP.NET Core SignalR | WebSocket push to connected browser/mobile clients |
| **Email** | MailKit + MimeKit (SMTP) | The .NET equivalent of Node.js Nodemailer |
| ~~SMS~~ | *(not implemented)* | Excluded per project requirements |

Every notification is also **persisted** to PostgreSQL so users can view their notification history and mark items as read.

---

## Architecture

```
NotificationController  (REST API — /api/v1/notifications/*)
        │
        ▼
NotificationService     (orchestrates all three outputs below)
   ├── INotificationRepository  → PostgreSQL via EF Core
   ├── IEmailService            → MailKit SMTP (Nodemailer equivalent)
   └── IHubContext<NotificationHub> → SignalR real-time push
```

### Layers (same pattern as all MediBook services)

| Layer | Class | Role |
|-------|-------|------|
| Entity / POCO | `Notification` | Domain model, no framework deps |
| Repository Interface | `INotificationRepository` | Data-access contract |
| Repository Impl | `NotificationRepository` | EF Core + PostgreSQL |
| Service Interface | `INotificationService` | Business contract |
| Service Impl | `NotificationService` | Orchestrates DB + SignalR + Email |
| API Controller | `NotificationController` | `[ApiController]` REST surface |

---

## Email: MailKit (Nodemailer Equivalent)

MailKit is the de-facto SMTP library for .NET, mirroring what Nodemailer does in Node.js:

| Node.js (Nodemailer) | .NET (MailKit) |
|---------------------|---------------|
| `nodemailer.createTransport(smtpOptions)` | `new SmtpClient()` + `ConnectAsync()` |
| `transporter.sendMail(mailOptions)` | `client.SendAsync(message)` |
| `{ from, to, subject, html }` | `MimeMessage` with `BodyBuilder` |
| Gmail / Mailtrap / SendGrid | Same providers — same SMTP credentials |

### Supported SMTP Providers

Configure `EmailSettings` in `appsettings.json`:

```json
{
  "EmailSettings": {
    "SmtpHost":  "smtp.gmail.com",
    "SmtpPort":  587,
    "UseSsl":    false,
    "Username":  "your-email@gmail.com",
    "Password":  "your-app-password",
    "FromEmail": "no-reply@medibook.com",
    "FromName":  "MediBook"
  }
}
```

| Provider | Host | Port | UseSsl |
|----------|------|------|--------|
| Gmail | `smtp.gmail.com` | 587 | false (STARTTLS) |
| Outlook / Hotmail | `smtp.office365.com` | 587 | false |
| Mailtrap (dev) | `sandbox.smtp.mailtrap.io` | 587 | false |
| SendGrid | `smtp.sendgrid.net` | 587 | false |
| Amazon SES | `email-smtp.us-east-1.amazonaws.com` | 587 | false |

> **Gmail note:** Use an [App Password](https://support.google.com/accounts/answer/185833), not your Google account password.

---

## SignalR — Real-Time In-App Notifications

The `NotificationHub` pushes events to connected clients. Each user is added to a **group named after their UserId**, so notifications are targeted precisely.

### JavaScript / TypeScript Client

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5006/hubs/notifications", {
    accessTokenFactory: () => localStorage.getItem("token") ?? ""
  })
  .withAutomaticReconnect()
  .build();

connection.on("ReceiveNotification", (notification) => {
  console.log("New notification:", notification);
  // { id, recipientId, type, title, message, channel, isRead, sentAt, ... }
});

await connection.start();
```

---

## Notification Types & Channels

### Types
| Value | Trigger |
|-------|---------|
| `BOOKING` | Appointment booked |
| `REMINDER` | 24h / 1h pre-appointment reminder |
| `CANCELLATION` | Appointment cancelled |
| `PAYMENT` | Payment processed / refunded |
| `FOLLOWUP` | Follow-up date from medical record |

### Channels
| Value | Dispatched |
|-------|-----------|
| `APP` | SignalR push only |
| `EMAIL` | Persisted + SignalR push + email via MailKit |
| `SMS` | Schema value — not dispatched |

---

## REST API

Base URL: `http://localhost:5006/api/v1/notifications`

Swagger UI: `http://localhost:5006` (dev only)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/send` | Any | Send a single notification |
| `POST` | `/bulk` | Admin | Broadcast to many recipients |
| `POST` | `/email` | Any | Send a raw HTML email (no record) |
| `GET` | `/recipient/{id}?page=1&pageSize=20` | Any | Get notifications for a user |
| `GET` | `/unread/{id}` | Any | Get unread count for a user |
| `GET` | `/all?page=1&pageSize=50` | Admin | Get all notifications |
| `PUT` | `/{id}/read` | Any | Mark single notification as read |
| `PUT` | `/recipient/{id}/read-all` | Any | Mark all as read for a user |
| `DELETE` | `/{id}` | Any | Delete a notification |

### Send Notification — Request Body

```json
{
  "recipientId":    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "recipientEmail": "patient@example.com",
  "recipientName":  "Rahul Sharma",
  "type":           "BOOKING",
  "title":          "Appointment Confirmed",
  "message":        "Your appointment with Dr. Mehta on 25 Apr at 10:00 AM is confirmed.",
  "channel":        "EMAIL",
  "relatedId":      "abc12345-...",
  "relatedType":    "Appointment"
}
```

---

## Running Locally

### Option A — Docker Compose (recommended)

```bash
cd notification-service

# Set your email credentials first (edit docker-compose.yml)
docker compose up --build
```

Service → `http://localhost:5006`
Swagger → `http://localhost:5006`

### Option B — dotnet run

```bash
# Requires PostgreSQL running on localhost:5432
cd notification-service/src/MediBook.Notification.API
dotnet run
```

---

## Database

- **Engine:** PostgreSQL 16
- **ORM:** Entity Framework Core 8 with Code-First migrations
- **Migration:** Applied automatically on startup via `db.Database.Migrate()`
- **Table:** `notifications`

```
notifications
├── id             uuid  PK
├── recipient_id   uuid  (indexed)
├── type           varchar(30)
├── title          varchar(200)
├── message        varchar(2000)
├── channel        varchar(10)
├── related_id     uuid  nullable (indexed)
├── related_type   varchar(50) nullable
├── is_read        bool  default false (indexed with recipient_id)
├── sent_at        timestamptz
└── created_at     timestamptz
```

---

## Integration with Other MediBook Services

Other services call this service via `IHttpClientFactory` to dispatch notifications:

```csharp
// In AppointmentService, after booking:
await _httpClient.PostAsJsonAsync("http://notification-service/api/v1/notifications/send", new
{
    RecipientId    = appointment.PatientId,
    RecipientEmail = patientEmail,
    RecipientName  = patientName,
    Type           = "BOOKING",
    Title          = "Appointment Confirmed",
    Message        = $"Your appointment with {providerName} is confirmed.",
    Channel        = "EMAIL",
    RelatedId      = appointment.Id,
    RelatedType    = "Appointment"
});
```

---

## Environment Variables (Docker / Production)

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__NotificationDb` | PostgreSQL connection string |
| `JwtSettings__SecretKey` | Must match the auth-service secret (min 32 chars) |
| `JwtSettings__Issuer` | Must match `MediBook.Auth` |
| `JwtSettings__Audience` | Must match `MediBook.Client` |
| `EmailSettings__SmtpHost` | SMTP server hostname |
| `EmailSettings__SmtpPort` | SMTP port (587 for STARTTLS) |
| `EmailSettings__Username` | SMTP username |
| `EmailSettings__Password` | SMTP password / app password |
| `EmailSettings__FromEmail` | Sender email address |
| `EmailSettings__FromName` | Sender display name |
