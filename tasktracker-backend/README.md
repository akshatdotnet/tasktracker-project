# TaskTracker API — .NET Core 8 RESTful Service

> Clean Architecture · CQRS · MediatR · EF Core 8 · JWT Auth · SignalR · Swagger

---

## Quick Start (3 commands)

```powershell
# IMPORTANT: All commands must be run from the solution root folder:
# C:\...\tasktracker-backend>

# 1 — Restore packages
dotnet restore

# 2 — Apply database migrations
dotnet ef database update --project src/TaskTracker.Infrastructure --startup-project src/TaskTracker.API

# 3 — Run the API  (always use --project flag from solution root)
dotnet run --project src/TaskTracker.API

# 4 — Open Swagger UI
start https://localhost:5001/swagger
```

> **Why --project?** Running `dotnet run` from the solution root fails because
> there is no `.csproj` at that level. Always use `--project src/TaskTracker.API`.

---

## Prerequisites

| Tool | Version | Check | Install |
|------|---------|-------|---------|
| .NET SDK | 8.x | `dotnet --version` | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| EF Core CLI | 8.x | `dotnet ef --version` | `dotnet tool install -g dotnet-ef` |
| SQL Server LocalDB | 2019+ | `sqllocaldb info` | Bundled with Visual Studio 2022 |
| Visual Studio 2022 | 17.8+ | — | Community edition free |

---

## Step-by-Step Local Setup

### Step 1 — Extract the project

```powershell
cd C:\Projects
# Extract tasktracker-backend.zip here
cd tasktracker-backend
```

### Step 2 — Restore NuGet packages

```powershell
dotnet restore
```

### Step 3 — Apply database migrations

```powershell
dotnet ef database update `
  --project src/TaskTracker.Infrastructure `
  --startup-project src/TaskTracker.API
```

> This creates `TaskTrackerDb` on `(localdb)\MSSQLLocalDB` and seeds all test data automatically.

### Step 4 — Run the API

```powershell
# Standard run
dotnet run --project src/TaskTracker.API

# Watch mode (auto-restart on code changes)
dotnet watch --project src/TaskTracker.API run
```

### Step 5 — Test in Swagger

Open **https://localhost:5001/swagger** in your browser.

1. Use **POST /api/v1/auth/login** to get a JWT token
2. Click **Authorize** (top right) and paste `Bearer <your-token>`
3. All protected endpoints are now accessible

---

## Seeded Users

| Email | Password | Role |
|-------|----------|------|
| admin@tasktracker.dev | Admin@1234 | Admin |
| arjun@tasktracker.dev | Dev@12345 | Developer |
| priya@tasktracker.dev | Dev@12345 | Developer |
| rohan@tasktracker.dev | Dev@12345 | Developer |
| sneha@tasktracker.dev | Dev@12345 | Developer |
| viewer@tasktracker.dev | Dev@12345 | Viewer |

---

## API Endpoints

### Auth — `/api/v1/auth`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/login` | Public | Login → JWT + refresh token |
| POST | `/refresh-token` | Public | Rotate refresh token |
| POST | `/forgot-password` | Public | Request reset email (always 200) |
| POST | `/validate-token` | Public | Check reset token validity |
| POST | `/reset-password` | Public | Set new password |
| POST | `/register` | Admin | Create new user |
| GET | `/me` | Bearer | Get current user profile |

### Dashboard — `/api/v1/dashboard`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/overview` | Bearer | Full dashboard snapshot |
| GET | `/team` | Bearer | All developers + today's stats |
| GET | `/developer/{id}` | Bearer | Single developer summary |
| GET | `/developer/{id}/today` | Bearer | Today's task timeline |
| GET | `/tasks/by-status` | Bearer | Status count breakdown |
| GET | `/velocity?days=7` | Bearer | N-day velocity trend |

---

## Angular Integration

### 1. Set the environment

```typescript
// src/environments/environment.ts
export const environment = {
  production:  false,
  apiBase:     'https://localhost:5001/api/v1',
  useMockData: false,   // ← Set false to use this API
  signalRHub:  'https://localhost:5001/hubs/dashboard'
};
```

### 2. Login and store tokens

```typescript
this.authSvc.login({ email, password }).subscribe(res => {
  localStorage.setItem('tt_access',  res.accessToken);
  localStorage.setItem('tt_refresh', res.refreshToken);
  localStorage.setItem('tt_expiry',  res.expiresAt);
});
```

### 3. Bearer token interceptor (already in Angular project)

The `auth.interceptor.ts` in your Angular project automatically:
- Attaches `Authorization: Bearer <token>` to every request
- Catches 401 responses and silently refreshes the token
- Retries the original request with the new token

### 4. CORS is pre-configured

The API allows `http://localhost:4200` by default. No extra setup needed.

---

## Running Unit Tests

```powershell
dotnet test tests/TaskTracker.UnitTests

# With coverage report
dotnet test tests/TaskTracker.UnitTests --collect:"XPlat Code Coverage"
```

---

## Project Architecture

```
tasktracker-backend/
  src/
    TaskTracker.Domain/            ← Entities, Enums, Interfaces, Result pattern
    TaskTracker.Application/       ← CQRS Commands/Queries, Validators, DTOs
    TaskTracker.Infrastructure/    ← EF Core, Repositories, JWT, bcrypt, Cache
    TaskTracker.API/               ← Controllers, Middleware, SignalR Hub
  tests/
    TaskTracker.UnitTests/         ← xUnit + Moq + FluentAssertions
  TaskTracker.sln
  TaskTracker.postman_collection.json
```

### Dependency flow (Clean Architecture)

```
API ──────────────────────────┐
Infrastructure ───────────────┤──→  Application ──→  Domain
                              ┘
```

Domain has zero external dependencies. Application depends only on Domain.
Infrastructure and API depend on Application.

---

## Common Issues & Fixes

| Problem | Fix |
|---------|-----|
| `dotnet ef: command not found` | `dotnet tool install -g dotnet-ef` then restart terminal |
| `SSL certificate error` | `dotnet dev-certs https --trust` then restart browser |
| `LocalDB not found` | Install via Visual Studio Installer → Individual components → LocalDB |
| `401 on every dashboard request` | Check token in Swagger Authorize or Angular localStorage |
| `Port 5001 already in use` | Edit `applicationUrl` in `Properties/launchSettings.json` |
| `Seed data missing` | Delete the database: `dotnet ef database drop --force` then re-run migrations |

---

## Reset Database (Clean Slate)

```powershell
dotnet ef database drop --force `
  --project src/TaskTracker.Infrastructure `
  --startup-project src/TaskTracker.API

dotnet ef database update `
  --project src/TaskTracker.Infrastructure `
  --startup-project src/TaskTracker.API
```

Then restart the API — seed data runs automatically on startup.

---

## Postman Collection

Import `TaskTracker.postman_collection.json` into Postman:
1. Open Postman → Import → select the JSON file
2. Run "Login (Admin)" first — token is saved automatically
3. All other requests use the saved Bearer token

---

*TaskTracker API · .NET Core 8 · Clean Architecture · SOLID · 2025*
