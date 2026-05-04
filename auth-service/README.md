# MediBook — Auth Service

JWT-based authentication service for MediBook with **Google OAuth2** and **GitHub OAuth2** support.

---

## Endpoint

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/v1/auth/register` | — | Register a Patient account |
| POST | `/api/v1/auth/register-provider` | — | Register a Provider account |
| POST | `/api/v1/auth/login` | — | Email + password login |
| GET  | `/api/v1/auth/me` | Bearer | Get current user profile |
| POST | `/api/v1/auth/refresh` | — | Rotate refresh token |
| POST | `/api/v1/auth/logout` | Bearer | Revoke refresh token |
| PUT  | `/api/v1/auth/profile` | Bearer | Update name / phone / picture |
| PUT  | `/api/v1/auth/password` | Bearer | Change password (local accounts) |
| DELETE | `/api/v1/auth/deactivate` | Bearer | Soft-deactivate account |
| **GET** | **`/api/v1/auth/oauth/google/login`** | — | **Start Google OAuth flow** |
| **GET** | **`/api/v1/auth/oauth/google/callback`** | — | **Google OAuth callback** |
| **GET** | **`/api/v1/auth/oauth/github/login`** | — | **Start GitHub OAuth flow** |
| **GET** | **`/api/v1/auth/oauth/github/callback`** | — | **GitHub OAuth callback** |
| PUT  | `/api/v1/auth/oauth/set-password` | Bearer | Set local password (OAuth users) |
| GET  | `/api/v1/auth/health` | — | Health check |

---

## OAuth2 Setup

### Google

1. Go to [Google Cloud Console](https://console.cloud.google.com/) → **APIs & Services** → **Credentials**.
2. Create an **OAuth 2.0 Client ID** (type: **Web application**).
3. Add authorised redirect URI:
   ```
   http://localhost:5000/api/v1/auth/oauth/google/callback   ← development
   https://your-host/api/v1/auth/oauth/google/callback       ← production
   ```
4. Copy **Client ID** and **Client Secret** into `appsettings.json`:
   ```json
   "OAuthSettings": {
     "Google": {
       "ClientId": "YOUR_GOOGLE_CLIENT_ID",
       "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
     }
   }
   ```

### GitHub

1. Go to [GitHub Developer Settings](https://github.com/settings/applications/new) → **OAuth Apps** → **New OAuth App**.
2. Set **Authorization callback URL**:
   ```
   http://localhost:5000/api/v1/auth/oauth/github/callback   ← development
   https://your-host/api/v1/auth/oauth/github/callback       ← production
   ```
3. Copy **Client ID** and **Client Secret** into `appsettings.json`:
   ```json
   "OAuthSettings": {
     "GitHub": {
       "ClientId": "YOUR_GITHUB_CLIENT_ID",
       "ClientSecret": "YOUR_GITHUB_CLIENT_SECRET"
     }
   }
   ```

### CallbackBaseUrl

Set `OAuthSettings:CallbackBaseUrl` to the public base URL of this service:

```json
"OAuthSettings": {
  "CallbackBaseUrl": "https://auth.medibook.io"
}
```

---

## OAuth2 Flow

```
Client browser                Auth Service              Google / GitHub
     │                             │                           │
     │  GET /oauth/google/login    │                           │
     │────────────────────────────▶│                           │
     │  302 → accounts.google.com  │                           │
     │◀────────────────────────────│                           │
     │                             │                           │
     │──── user consents ─────────────────────────────────────▶│
     │◀─── redirect with code & state ────────────────────────│
     │                             │                           │
     │  GET /oauth/google/callback │                           │
     │   ?code=...&state=...       │                           │
     │────────────────────────────▶│                           │
     │                             │── exchange code ─────────▶│
     │                             │◀─ access_token ───────────│
     │                             │── GET userinfo ──────────▶│
     │                             │◀─ { sub, email, name } ───│
     │                             │                           │
     │                             │  upsert user in DB        │
     │                             │  issue MediBook JWT       │
     │                             │                           │
     │  { accessToken, refreshToken, user, isNewUser }         │
     │◀────────────────────────────│                           │
```

**Account linking**: if the OAuth email matches an existing local account, the OAuth provider is linked to it — no duplicate account is created.

---

## Configuration — `appsettings.json`

```json
{
  "JwtSettings": {
    "SecretKey": "at-least-32-character-secret",
    "Issuer": "MediBook.Auth",
    "Audience": "MediBook.Client",
    "AccessTokenExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  },
  "OAuthSettings": {
    "CallbackBaseUrl": "http://localhost:5000",
    "Google": {
      "ClientId": "...",
      "ClientSecret": "..."
    },
    "GitHub": {
      "ClientId": "...",
      "ClientSecret": "..."
    }
  }
}
```

---

## Running Locally

```bash
cd auth-service
dotnet restore
dotnet run --project src/MediBook.Auth.API
# Swagger UI: http://localhost:5000
```
