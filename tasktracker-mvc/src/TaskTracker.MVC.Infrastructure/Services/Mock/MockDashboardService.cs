using TaskTracker.MVC.Application.DTOs.Dashboard;
using TaskTracker.MVC.Application.Interfaces;

namespace TaskTracker.MVC.Infrastructure.Services.Mock;

/// <summary>
/// In-memory mock dashboard service — mirrors Angular DashboardMockService data exactly.
/// All developer IDs, names, and stats match the Angular mock so switching between
/// Angular and MVC gives the same visual output.
/// Activated when ApiSettings.UseMockData = true in appsettings.json.
/// </summary>
public sealed class MockDashboardService : IDashboardService
{
    // ── Same mock developers as Angular DEVS array ─────────────────────────────
    private static readonly List<DeveloperSummaryDto> MockDevs = new()
    {
        new() {
            DeveloperId         = "dev-001",
            Name                = "Arjun Mehta",
            Email               = "arjun@tasktracker.dev",
            TotalTasks          = 12,
            CompletedToday      = 5,
            InProgress          = 3,
            Pending             = 4,
            TotalHoursToday     = 6.5,
            AverageTaskDuration = 78,
            LastActivityAt      = DateTime.UtcNow.AddMinutes(-12)
        },
        new() {
            DeveloperId         = "dev-002",
            Name                = "Priya Sharma",
            Email               = "priya@tasktracker.dev",
            TotalTasks          = 9,
            CompletedToday      = 7,
            InProgress          = 1,
            Pending             = 1,
            TotalHoursToday     = 7.0,
            AverageTaskDuration = 60,
            LastActivityAt      = DateTime.UtcNow.AddMinutes(-3)
        },
        new() {
            DeveloperId         = "dev-003",
            Name                = "Rohan Desai",
            Email               = "rohan@tasktracker.dev",
            TotalTasks          = 11,
            CompletedToday      = 4,
            InProgress          = 5,
            Pending             = 2,
            TotalHoursToday     = 5.5,
            AverageTaskDuration = 82,
            LastActivityAt      = DateTime.UtcNow.AddMinutes(-30)
        },
        new() {
            DeveloperId         = "dev-004",
            Name                = "Sneha Kulkarni",
            Email               = "sneha@tasktracker.dev",
            TotalTasks          = 8,
            CompletedToday      = 2,
            InProgress          = 4,
            Pending             = 2,
            TotalHoursToday     = 4.0,
            AverageTaskDuration = 120,
            LastActivityAt      = DateTime.UtcNow.AddHours(-1)
        },
    };

    // ── Timeline entries per developer ─────────────────────────────────────────
    private static readonly Dictionary<string, List<TimelineEntryDto>> MockTimelines = new()
    {
        ["dev-001"] = new()
        {
            new() { TaskId = "t-101", Title = "Login API integration",        Status = "Completed",  Priority = "High",   StartedAt = DateTime.UtcNow.AddHours(-5),   EndedAt = DateTime.UtcNow.AddHours(-3.75), DurationMinutes = 75  },
            new() { TaskId = "t-102", Title = "Fix refresh token rotation",   Status = "Completed",  Priority = "High",   StartedAt = DateTime.UtcNow.AddHours(-3.5), EndedAt = DateTime.UtcNow.AddHours(-2.8),  DurationMinutes = 40  },
            new() { TaskId = "t-103", Title = "Dashboard overview component", Status = "Completed",  Priority = "Medium", StartedAt = DateTime.UtcNow.AddHours(-2.5), EndedAt = DateTime.UtcNow.AddHours(-1.25), DurationMinutes = 75  },
            new() { TaskId = "t-104", Title = "Unit tests for AuthService",   Status = "InProgress", Priority = "Medium", StartedAt = DateTime.UtcNow.AddHours(-0.5), EndedAt = null,                             DurationMinutes = 0   },
            new() { TaskId = "t-105", Title = "Code review: reset-pw PR",    Status = "Pending",    Priority = "Low",    StartedAt = null,                            EndedAt = null,                             DurationMinutes = 0   },
        },
        ["dev-002"] = new()
        {
            new() { TaskId = "t-201", Title = "Design forgot-password flow",  Status = "Completed",  Priority = "High",   StartedAt = DateTime.UtcNow.AddHours(-7),   EndedAt = DateTime.UtcNow.AddHours(-6.25), DurationMinutes = 45  },
            new() { TaskId = "t-202", Title = "ForgotPasswordHandler impl",   Status = "Completed",  Priority = "High",   StartedAt = DateTime.UtcNow.AddHours(-6),   EndedAt = DateTime.UtcNow.AddHours(-4.5),  DurationMinutes = 90  },
            new() { TaskId = "t-203", Title = "RabbitMQ event publishing",    Status = "Completed",  Priority = "Medium", StartedAt = DateTime.UtcNow.AddHours(-4),   EndedAt = DateTime.UtcNow.AddHours(-2.75), DurationMinutes = 75  },
            new() { TaskId = "t-204", Title = "Email service consumer",       Status = "Completed",  Priority = "Medium", StartedAt = DateTime.UtcNow.AddHours(-2.5), EndedAt = DateTime.UtcNow.AddHours(-1),    DurationMinutes = 90  },
            new() { TaskId = "t-205", Title = "Integration tests",            Status = "InProgress", Priority = "Low",    StartedAt = DateTime.UtcNow.AddMinutes(-45), EndedAt = null,                            DurationMinutes = 0   },
        },
        ["dev-003"] = new()
        {
            new() { TaskId = "t-301", Title = "SignalR hub setup",            Status = "Completed",  Priority = "High",   StartedAt = DateTime.UtcNow.AddHours(-6),   EndedAt = DateTime.UtcNow.AddHours(-4.5),  DurationMinutes = 90  },
            new() { TaskId = "t-302", Title = "DashboardHub OnTaskUpdated",   Status = "Completed",  Priority = "Medium", StartedAt = DateTime.UtcNow.AddHours(-4),   EndedAt = DateTime.UtcNow.AddHours(-2.75), DurationMinutes = 75  },
            new() { TaskId = "t-303", Title = "Cache invalidation logic",     Status = "InProgress", Priority = "High",   StartedAt = DateTime.UtcNow.AddHours(-2),   EndedAt = null,                             DurationMinutes = 0   },
            new() { TaskId = "t-304", Title = "Background refresh service",   Status = "InProgress", Priority = "Medium", StartedAt = DateTime.UtcNow.AddHours(-1),   EndedAt = null,                             DurationMinutes = 0   },
            new() { TaskId = "t-305", Title = "Redis integration",            Status = "Pending",    Priority = "Low",    StartedAt = null,                            EndedAt = null,                             DurationMinutes = 0   },
        },
        ["dev-004"] = new()
        {
            new() { TaskId = "t-401", Title = "PasswordResetToken entity",    Status = "Completed",  Priority = "High",   StartedAt = DateTime.UtcNow.AddHours(-6.5), EndedAt = DateTime.UtcNow.AddHours(-6),    DurationMinutes = 30  },
            new() { TaskId = "t-402", Title = "Token hash storage",           Status = "Completed",  Priority = "High",   StartedAt = DateTime.UtcNow.AddHours(-5.8), EndedAt = DateTime.UtcNow.AddHours(-4.47), DurationMinutes = 80  },
            new() { TaskId = "t-403", Title = "Reset expiry validation",      Status = "InProgress", Priority = "Medium", StartedAt = DateTime.UtcNow.AddHours(-2),   EndedAt = null,                             DurationMinutes = 0   },
            new() { TaskId = "t-404", Title = "Revoke all refresh tokens",    Status = "InProgress", Priority = "Medium", StartedAt = DateTime.UtcNow.AddHours(-1),   EndedAt = null,                             DurationMinutes = 0   },
            new() { TaskId = "t-405", Title = "E2E reset password tests",     Status = "Pending",    Priority = "Low",    StartedAt = null,                            EndedAt = null,                             DurationMinutes = 0   },
        },
    };

    // ── Velocity base data (same 30 values as Angular) ─────────────────────────
    private static readonly int[] VelocityBase = { 6,9,7,11,8,12,10,8,14,9,7,13,11,8,10,12,9,11,7,13,10,8,14,11,9,12,8,10,13,9 };

    // ══════════════════════════════════════════════════════════════════════════
    //  Interface Implementations
    // ══════════════════════════════════════════════════════════════════════════

    public Task<DashboardOverviewDto?> GetOverviewAsync(CancellationToken ct = default)
    {
        var dto = new DashboardOverviewDto
        {
            TotalTasks     = 142,
            CompletedToday = 18,
            InProgress     = 23,
            Blocked        = 4,
            TeamVelocity   = 8.3,
            SprintProgress = 67,
            LastRefreshed  = DateTime.UtcNow
        };
        return Task.FromResult<DashboardOverviewDto?>(dto);
    }

    public Task<TeamReportDto?> GetTeamReportAsync(CancellationToken ct = default)
    {
        var dto = new TeamReportDto
        {
            Date              = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            OverallCompletion = 67,
            Developers        = new List<DeveloperSummaryDto>(MockDevs)
        };
        return Task.FromResult<TeamReportDto?>(dto);
    }

    public Task<DeveloperSummaryDto?> GetDeveloperAsync(string developerId, CancellationToken ct = default)
    {
        var dev = MockDevs.FirstOrDefault(d => d.DeveloperId == developerId) ?? MockDevs[0];
        return Task.FromResult<DeveloperSummaryDto?>(dev);
    }

    public Task<TodayTimelineDto?> GetDeveloperTodayAsync(string developerId, CancellationToken ct = default)
    {
        var dev     = MockDevs.FirstOrDefault(d => d.DeveloperId == developerId) ?? MockDevs[0];
        var entries = MockTimelines.TryGetValue(developerId, out var tl) ? tl : MockTimelines["dev-001"];

        var dto = new TodayTimelineDto
        {
            DeveloperId   = developerId,
            DeveloperName = dev.Name,
            Entries       = new List<TimelineEntryDto>(entries)
        };
        return Task.FromResult<TodayTimelineDto?>(dto);
    }

    public Task<StatusBreakdownDto?> GetStatusBreakdownAsync(CancellationToken ct = default)
    {
        var dto = new StatusBreakdownDto
        {
            Pending    = 34,
            InProgress = 23,
            Completed  = 81,
            Blocked    = 4
        };
        return Task.FromResult<StatusBreakdownDto?>(dto);
    }

    public Task<List<VelocityPointDto>> GetVelocityAsync(int days = 7, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 30);
        var points = Enumerable.Range(0, days).Select(i =>
        {
            var date = DateTime.UtcNow.Date.AddDays(-(days - 1 - i));
            var val  = VelocityBase[i % VelocityBase.Length];
            return new VelocityPointDto
            {
                Date           = date.ToString("yyyy-MM-dd"),
                TasksCompleted = val,
                HoursLogged    = Math.Round(val * 0.75, 1)
            };
        }).ToList();

        return Task.FromResult(points);
    }
}
