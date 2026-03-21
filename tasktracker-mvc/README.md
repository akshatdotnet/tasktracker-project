# TaskTracker MVC — .NET Core 8 Web Application

> Clean Architecture · SOLID Principles · Server-side rendered Razor Views
> Same dark UI design as the Angular app · Connects to TaskTracker REST API

---

## Architecture

```
TaskTracker.MVC.Domain          <- Models, Enums (zero dependencies)
TaskTracker.MVC.Application     <- Interfaces (ISP), DTOs (depends on Domain only)
TaskTracker.MVC.Infrastructure  <- HTTP clients calling .NET API (implements Application interfaces)
TaskTracker.MVC.Web             <- MVC Controllers, Razor Views, Filters (depends on Application)
```

**Key design decisions:**
- **DIP**: Controllers depend on `IAuthService` and `IDashboardService` — never on concrete classes
- **ISP**: 3 small focused interfaces instead of one fat service interface
- **SRP**: `AuthService` only handles auth HTTP calls; `DashboardService` only dashboard calls
- **OCP**: Add new pages by adding new Controllers/Views — never modify existing auth logic
- **LSP**: `AuthenticatedFilter` can be extended or replaced without breaking controllers
- **Security**: JWT tokens stored in server-side ASP.NET Core session (HttpOnly cookie) — browser never sees raw tokens

---

## Quick Start

### Prerequisites

- .NET 8 SDK
- TaskTracker .NET Core 8 API running on `http://localhost:5000`

### Run

```cmd
cd tasktracker-mvc

dotnet run --project src/TaskTracker.MVC.Web

start http://localhost:5050
```

### Login Credentials (seeded by API)

| Email | Password | Role |
|---|---|---|
| admin@tasktracker.dev | Admin@1234 | Admin |
| arjun@tasktracker.dev | Dev@12345 | Developer |
| priya@tasktracker.dev | Dev@12345 | Developer |
| viewer@tasktracker.dev | Dev@12345 | Viewer |

---

## Pages

| URL | Page | Auth |
|---|---|---|
| `/login` | Sign in form | Public |
| `/forgot-password` | Request reset link | Public |
| `/reset-password?token=xxx` | Set new password | Public |
| `/dashboard/overview` | Metrics + team list | Protected |
| `/dashboard/team` | Team report table | Protected |
| `/dashboard/developer/{id}` | Developer + timeline | Protected |
| `/dashboard/velocity` | Charts + status | Protected |

---

## Configuration

Edit `src/TaskTracker.MVC.Web/appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5000/api/v1",
    "TimeoutSeconds": 30
  }
}
```

---

## Both projects running together

| Terminal | Command | Port |
|---|---|---|
| 1 | `dotnet run --project src/TaskTracker.API` (backend) | 5000 |
| 2 | `dotnet run --project src/TaskTracker.MVC.Web` (this) | 5050 |
| 3 | `ng serve` (Angular, optional) | 4200 |
