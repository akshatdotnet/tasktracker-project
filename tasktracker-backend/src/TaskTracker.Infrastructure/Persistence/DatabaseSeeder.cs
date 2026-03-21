using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Infrastructure.Persistence;

/// <summary>
/// Seeds the database with realistic development data on first startup.
/// Skips seeding if any users already exist (idempotent).
/// Called from Program.cs: await DatabaseSeeder.SeedAsync(app.Services);
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope   = services.CreateScope();
        var       context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var       logger  = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            // Apply any pending migrations
            await context.Database.MigrateAsync();

            // Safety check: if no migrations exist, tables won't be created.
            // Check that the Users table actually exists before querying it.
            var tableExists = await context.Database
                .ExecuteSqlRawAsync("SELECT 1 WHERE OBJECT_ID(N'[Users]', N'U') IS NOT NULL") >= 0;

            // A cleaner check: try to get pending migrations
            var pending = await context.Database.GetPendingMigrationsAsync();
            if (pending.Any())
            {
                logger.LogWarning(
                    "Pending migrations found: {Count}. Run: dotnet ef database update",
                    pending.Count());
                return;
            }

            // Check if schema exists by looking at applied migrations count
            var applied = await context.Database.GetAppliedMigrationsAsync();
            if (!applied.Any())
            {
                logger.LogWarning(
                    "No migrations have been applied. " +
                    "Run these commands first:\n" +
                    "  dotnet ef migrations add InitialCreate --project src/TaskTracker.Infrastructure --startup-project src/TaskTracker.API\n" +
                    "  dotnet ef database update --project src/TaskTracker.Infrastructure --startup-project src/TaskTracker.API");
                return;
            }

            // Skip if already seeded
            if (await context.Users.AnyAsync())
            {
                logger.LogInformation("Database already seeded. Skipping.");
                return;
            }

            logger.LogInformation("Seeding database with development data...");

            // ── Users ─────────────────────────────────────────────────────
            // Passwords use bcrypt factor 12. Raw values for local testing:
            // Admin: Admin@1234  |  Developers: Dev@12345
            var adminHash = BCrypt.Net.BCrypt.HashPassword("Admin@1234", workFactor: 12);
            var devHash   = BCrypt.Net.BCrypt.HashPassword("Dev@12345",  workFactor: 12);
            var salt      = "seeded"; // Simplified for seed data

            var admin  = User.Create("admin@tasktracker.dev",  adminHash, salt, "System",  "Admin",    UserRole.Admin);
            var arjun  = User.Create("arjun@tasktracker.dev",  devHash,   salt, "Arjun",   "Mehta",    UserRole.Developer);
            var priya  = User.Create("priya@tasktracker.dev",  devHash,   salt, "Priya",   "Sharma",   UserRole.Developer);
            var rohan  = User.Create("rohan@tasktracker.dev",  devHash,   salt, "Rohan",   "Desai",    UserRole.Developer);
            var sneha  = User.Create("sneha@tasktracker.dev",  devHash,   salt, "Sneha",   "Kulkarni", UserRole.Developer);
            var viewer = User.Create("viewer@tasktracker.dev", devHash,   salt, "Client",  "Viewer",   UserRole.Viewer);

            await context.Users.AddRangeAsync(admin, arjun, priya, rohan, sneha, viewer);
            await context.SaveChangesAsync();

            // ── Project ───────────────────────────────────────────────────
            var project = Project.Create("TaskTracker Platform",
                "Internal task management and developer analytics platform built with Angular + .NET Core 8");
            await context.Projects.AddAsync(project);
            await context.SaveChangesAsync();

            // ── Sprint ────────────────────────────────────────────────────
            var sprintStart = DateTime.UtcNow.Date.AddDays(-7);
            var sprintEnd   = DateTime.UtcNow.Date.AddDays(7);
            var sprint = Sprint.Create(project.Id, "Sprint 1 — Core Auth + Dashboard", sprintStart, sprintEnd);
            await context.Sprints.AddAsync(sprint);
            await context.SaveChangesAsync();

            // ── Tasks ─────────────────────────────────────────────────────
            var now = DateTime.UtcNow;

            // Arjun's tasks — Auth module
            var t1 = CreateTask(project.Id, sprint.Id, "Implement JWT login endpoint",           arjun.Id, TaskStatus.Completed,  TaskPriority.Critical, 90,  now.AddDays(-5), now.AddDays(-5).AddHours(1.5));
            var t2 = CreateTask(project.Id, sprint.Id, "Implement refresh token rotation",       arjun.Id, TaskStatus.Completed,  TaskPriority.High,     60,  now.AddDays(-5).AddHours(2), now.AddDays(-5).AddHours(3));
            var t3 = CreateTask(project.Id, sprint.Id, "Add bcrypt password hashing (factor 12)",arjun.Id, TaskStatus.Completed,  TaskPriority.High,     45,  now.AddDays(-4), now.AddDays(-4).AddMinutes(50));
            var t4 = CreateTask(project.Id, sprint.Id, "Unit tests for AuthCommandHandler",      arjun.Id, TaskStatus.InProgress, TaskPriority.Medium,   120, now.AddHours(-3), null);
            var t5 = CreateTask(project.Id, sprint.Id, "Code review: Reset password PR",         arjun.Id, TaskStatus.Pending,    TaskPriority.Low,      30,  null, null);

            // Priya's tasks — Dashboard module
            var t6  = CreateTask(project.Id, sprint.Id, "Design forgot-password flow",            priya.Id, TaskStatus.Completed,  TaskPriority.High,     45,  now.AddDays(-4), now.AddDays(-4).AddMinutes(50));
            var t7  = CreateTask(project.Id, sprint.Id, "Implement ForgotPasswordCommandHandler", priya.Id, TaskStatus.Completed,  TaskPriority.High,     90,  now.AddDays(-3), now.AddDays(-3).AddHours(1.5));
            var t8  = CreateTask(project.Id, sprint.Id, "Dashboard overview API endpoint",        priya.Id, TaskStatus.Completed,  TaskPriority.Critical, 120, now.AddDays(-2), now.AddDays(-2).AddHours(2));
            var t9  = CreateTask(project.Id, sprint.Id, "Team report endpoint + caching",         priya.Id, TaskStatus.InProgress, TaskPriority.High,     90,  now.AddHours(-4), null);
            var t10 = CreateTask(project.Id, sprint.Id, "Integration tests for dashboard",        priya.Id, TaskStatus.Pending,    TaskPriority.Medium,   180, null, null);

            // Rohan's tasks — Infrastructure
            var t11 = CreateTask(project.Id, sprint.Id, "EF Core DbContext + seed data",          rohan.Id, TaskStatus.Completed,  TaskPriority.High,     90,  now.AddDays(-6), now.AddDays(-6).AddHours(1.5));
            var t12 = CreateTask(project.Id, sprint.Id, "SignalR DashboardHub setup",             rohan.Id, TaskStatus.Completed,  TaskPriority.Medium,   60,  now.AddDays(-3), now.AddDays(-3).AddHours(1));
            var t13 = CreateTask(project.Id, sprint.Id, "Background cache refresh service",       rohan.Id, TaskStatus.InProgress, TaskPriority.Medium,   120, now.AddHours(-5), null);
            var t14 = CreateTask(project.Id, sprint.Id, "Rate limiting middleware configuration", rohan.Id, TaskStatus.Pending,    TaskPriority.High,     60,  null, null);
            var t15 = CreateTask(project.Id, sprint.Id, "Docker Compose setup for local dev",     rohan.Id, TaskStatus.Blocked,    TaskPriority.Low,      90,  null, null);

            // Sneha's tasks — Angular frontend
            var t16 = CreateTask(project.Id, sprint.Id, "Angular login component + interceptor",  sneha.Id, TaskStatus.Completed,  TaskPriority.High,     120, now.AddDays(-4), now.AddDays(-4).AddHours(2));
            var t17 = CreateTask(project.Id, sprint.Id, "Dashboard overview component",           sneha.Id, TaskStatus.Completed,  TaskPriority.High,     90,  now.AddDays(-2), now.AddDays(-2).AddHours(1.5));
            var t18 = CreateTask(project.Id, sprint.Id, "Velocity chart with CSS bars",           sneha.Id, TaskStatus.InProgress, TaskPriority.Medium,   60,  now.AddHours(-2), null);
            var t19 = CreateTask(project.Id, sprint.Id, "Developer timeline component",           sneha.Id, TaskStatus.Pending,    TaskPriority.Medium,   90,  null, null);
            var t20 = CreateTask(project.Id, sprint.Id, "Team report table component",            sneha.Id, TaskStatus.Pending,    TaskPriority.Low,      60,  null, null);

            await context.Tasks.AddRangeAsync(t1,t2,t3,t4,t5,t6,t7,t8,t9,t10,t11,t12,t13,t14,t15,t16,t17,t18,t19,t20);
            await context.SaveChangesAsync();

            // ── Time Logs ─────────────────────────────────────────────────
            // Historical logs for velocity chart (last 7 days)
            var timelogs = new List<TimeLog>();

            // Day -6: Rohan works on DbContext
            timelogs.AddRange(new[] {
                TimeLog.Create(t11.Id, rohan.Id, 90, now.AddDays(-6).AddHours(9), "EF Core configuration"),
            });

            // Day -5: Arjun does auth work
            timelogs.AddRange(new[] {
                TimeLog.Create(t1.Id, arjun.Id, 90, now.AddDays(-5).AddHours(9), "Login endpoint implementation"),
                TimeLog.Create(t2.Id, arjun.Id, 60, now.AddDays(-5).AddHours(11), "Refresh token rotation"),
            });

            // Day -4: Arjun + Priya + Sneha
            timelogs.AddRange(new[] {
                TimeLog.Create(t3.Id,  arjun.Id, 50, now.AddDays(-4).AddHours(9),  "bcrypt integration"),
                TimeLog.Create(t6.Id,  priya.Id, 45, now.AddDays(-4).AddHours(9),  "Design forgot-password"),
                TimeLog.Create(t16.Id, sneha.Id, 120,now.AddDays(-4).AddHours(9),  "Login component"),
            });

            // Day -3: Priya + Rohan
            timelogs.AddRange(new[] {
                TimeLog.Create(t7.Id,  priya.Id, 90, now.AddDays(-3).AddHours(9),  "ForgotPassword handler"),
                TimeLog.Create(t12.Id, rohan.Id, 60, now.AddDays(-3).AddHours(9),  "SignalR hub"),
            });

            // Day -2: Priya + Sneha
            timelogs.AddRange(new[] {
                TimeLog.Create(t8.Id,  priya.Id, 120, now.AddDays(-2).AddHours(9), "Dashboard overview API"),
                TimeLog.Create(t17.Id, sneha.Id, 90,  now.AddDays(-2).AddHours(9), "Overview component"),
            });

            // Day -1: Multiple devs
            timelogs.AddRange(new[] {
                TimeLog.Create(t4.Id,  arjun.Id, 60, now.AddDays(-1).AddHours(9),  "Auth unit tests"),
                TimeLog.Create(t9.Id,  priya.Id, 60, now.AddDays(-1).AddHours(9),  "Team report endpoint"),
                TimeLog.Create(t13.Id, rohan.Id, 90, now.AddDays(-1).AddHours(9),  "Cache refresh service"),
            });

            // Today: Active work
            timelogs.AddRange(new[] {
                TimeLog.Create(t4.Id,  arjun.Id, 45, now.AddHours(-3), "More unit tests"),
                TimeLog.Create(t9.Id,  priya.Id, 60, now.AddHours(-4), "Caching implementation"),
                TimeLog.Create(t13.Id, rohan.Id, 90, now.AddHours(-5), "Background service"),
                TimeLog.Create(t18.Id, sneha.Id, 30, now.AddHours(-2), "Velocity chart CSS"),
            });

            await context.TimeLogs.AddRangeAsync(timelogs);
            await context.SaveChangesAsync();

            logger.LogInformation(
                "Seeding complete: {Users} users, {Tasks} tasks, {Logs} time logs",
                6, 20, timelogs.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database seeding failed");
            throw;
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private static TaskItem CreateTask(
        Guid projectId, Guid sprintId, string title,
        Guid assignedToId, TaskStatus status, TaskPriority priority,
        int estimatedMinutes, DateTime? startedAt, DateTime? completedAt)
    {
        var task = TaskItem.Create(projectId, title, assignedToId, sprintId, priority, estimatedMinutes);

        // Apply status transitions using domain methods
        if (status == TaskStatus.InProgress) task.Start();
        if (status == TaskStatus.Completed)  task.Complete();
        if (status == TaskStatus.Blocked)    task.Block();

        // Override dates using entry-point safe approach via EF shadow state
        // We set the backing fields through the domain methods above which set them to UtcNow.
        // For realistic historical seed dates we override via reflection (seed-data only, not production code).
        // Use NonPublic to reach private setters — acceptable for seed data only
        if (startedAt is not null)
        {
            typeof(TaskItem)
                .GetProperty("StartedAt")!
                .SetValue(task, startedAt);
        }
        if (completedAt is not null)
        {
            typeof(TaskItem)
                .GetProperty("CompletedAt")!
                .SetValue(task, completedAt);
        }

        return task;
    }
}
