using MediatR;
using TaskTracker.Application.Common.Interfaces;
using TaskTracker.Application.Dashboard.DTOs;
using TaskTracker.Domain.Common;
using TaskTracker.Domain.Interfaces;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Application.Dashboard.Queries;

// ══════════════════════════════════════════════════════════════════════════════
//  OVERVIEW
// ══════════════════════════════════════════════════════════════════════════════

public record GetOverviewQuery : IRequest<Result<DashboardOverviewDto>>;

public sealed class GetOverviewQueryHandler
    : IRequestHandler<GetOverviewQuery, Result<DashboardOverviewDto>>
{
    private readonly ITaskRepository     _tasks;
    private readonly ITimeLogRepository  _timeLogs;
    private readonly ICacheService       _cache;
    private const string CacheKey = "dashboard:overview";

    public GetOverviewQueryHandler(ITaskRepository tasks, ITimeLogRepository timeLogs, ICacheService cache)
        => (_tasks, _timeLogs, _cache) = (tasks, timeLogs, cache);

    public async Task<Result<DashboardOverviewDto>> Handle(GetOverviewQuery _, CancellationToken ct)
    {
        // Try cache first (2-minute TTL)
        var cached = await _cache.GetAsync<DashboardOverviewDto>(CacheKey, ct);
        if (cached is not null) return Result.Success(cached);

        var breakdown = await _tasks.GetStatusBreakdownAsync(ct);
        var velocity  = await _timeLogs.GetVelocityAsync(7, ct);

        var total         = breakdown.Values.Sum();
        var completed     = breakdown.GetValueOrDefault(TaskStatus.Completed);
        var inProgress    = breakdown.GetValueOrDefault(TaskStatus.InProgress);
        var blocked       = breakdown.GetValueOrDefault(TaskStatus.Blocked);
        var completedToday = await _tasks.CountCompletedTodayAsync(ct);
        var teamVelocity  = velocity.Any() ? Math.Round(velocity.Average(v => v.TasksCompleted), 1) : 0;
        var sprintProgress = total == 0 ? 0 : Math.Round((double)completed / total * 100, 1);

        var dto = new DashboardOverviewDto(
            TotalTasks:     total,
            CompletedToday: completedToday,
            InProgress:     inProgress,
            Blocked:        blocked,
            TeamVelocity:   teamVelocity,
            SprintProgress: sprintProgress,
            LastRefreshed:  DateTime.UtcNow);

        await _cache.SetAsync(CacheKey, dto, TimeSpan.FromMinutes(2), ct);
        return Result.Success(dto);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  DEVELOPER SUMMARY
// ══════════════════════════════════════════════════════════════════════════════

public record GetDeveloperQuery(Guid DeveloperId) : IRequest<Result<DeveloperSummaryDto>>;

public sealed class GetDeveloperQueryHandler
    : IRequestHandler<GetDeveloperQuery, Result<DeveloperSummaryDto>>
{
    private readonly IUserRepository     _users;
    private readonly ITaskRepository     _tasks;
    private readonly ITimeLogRepository  _timeLogs;
    private readonly ICacheService       _cache;

    public GetDeveloperQueryHandler(
        IUserRepository users, ITaskRepository tasks,
        ITimeLogRepository timeLogs, ICacheService cache)
    {
        (_users, _tasks, _timeLogs, _cache) = (users, tasks, timeLogs, cache);
    }

    public async Task<Result<DeveloperSummaryDto>> Handle(GetDeveloperQuery q, CancellationToken ct)
    {
        var cacheKey = $"dashboard:developer:{q.DeveloperId}";
        var cached   = await _cache.GetAsync<DeveloperSummaryDto>(cacheKey, ct);
        if (cached is not null) return Result.Success(cached);

        var user = await _users.GetByIdAsync(q.DeveloperId, ct);
        if (user is null) return Result.Failure<DeveloperSummaryDto>("Developer not found.");

        var allTasks   = await _tasks.GetByAssigneeAsync(q.DeveloperId, ct);
        var todayTasks = await _tasks.GetByAssigneeTodayAsync(q.DeveloperId, ct);
        var hours      = await _timeLogs.GetTotalHoursByUserTodayAsync(q.DeveloperId, ct);

        var completedTasks = allTasks.Where(t => t.Status == TaskStatus.Completed && t.ActualMinutes > 0).ToList();
        var avgDuration    = completedTasks.Any()
            ? Math.Round(completedTasks.Average(t => t.ActualMinutes), 0) : 0;

        var lastActivity = allTasks
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefault()?.UpdatedAt;

        var dto = new DeveloperSummaryDto(
            DeveloperId:         user.Id,
            Name:                user.FullName,
            Email:               user.Email,
            TotalTasks:          allTasks.Count,
            CompletedToday:      todayTasks.Count(t => t.Status == TaskStatus.Completed),
            InProgress:          allTasks.Count(t => t.Status == TaskStatus.InProgress),
            Pending:             allTasks.Count(t => t.Status == TaskStatus.Pending),
            TotalHoursToday:     Math.Round(hours, 1),
            AverageTaskDuration: avgDuration,
            LastActivityAt:      lastActivity);

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(2), ct);
        return Result.Success(dto);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  TODAY'S TIMELINE
// ══════════════════════════════════════════════════════════════════════════════

public record GetDeveloperTodayQuery(Guid DeveloperId) : IRequest<Result<TodayTimelineDto>>;

public sealed class GetDeveloperTodayQueryHandler
    : IRequestHandler<GetDeveloperTodayQuery, Result<TodayTimelineDto>>
{
    private readonly IUserRepository _users;
    private readonly ITaskRepository _tasks;

    public GetDeveloperTodayQueryHandler(IUserRepository users, ITaskRepository tasks)
        => (_users, _tasks) = (users, tasks);

    public async Task<Result<TodayTimelineDto>> Handle(GetDeveloperTodayQuery q, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(q.DeveloperId, ct);
        if (user is null) return Result.Failure<TodayTimelineDto>("Developer not found.");

        var tasks = await _tasks.GetByAssigneeTodayAsync(q.DeveloperId, ct);

        var entries = tasks
            .OrderBy(t => t.StartedAt ?? t.CreatedAt)
            .Select(t => new TimelineEntryDto(
                TaskId:          t.Id,
                Title:           t.Title,
                Status:          t.Status.ToString(),
                Priority:        t.Priority.ToString(),
                StartedAt:       t.StartedAt,
                EndedAt:         t.CompletedAt,
                DurationMinutes: t.ActualMinutes > 0 ? t.ActualMinutes : t.EstimatedMinutes))
            .ToList();

        return Result.Success(new TodayTimelineDto(q.DeveloperId, user.FullName, entries));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  TEAM REPORT
// ══════════════════════════════════════════════════════════════════════════════

public record GetTeamReportQuery : IRequest<Result<TeamReportDto>>;

public sealed class GetTeamReportQueryHandler
    : IRequestHandler<GetTeamReportQuery, Result<TeamReportDto>>
{
    private readonly IUserRepository     _users;
    private readonly ITaskRepository     _tasks;
    private readonly ITimeLogRepository  _timeLogs;
    private readonly ICacheService       _cache;
    private const string CacheKey = "dashboard:team";

    public GetTeamReportQueryHandler(
        IUserRepository users, ITaskRepository tasks,
        ITimeLogRepository timeLogs, ICacheService cache)
    {
        (_users, _tasks, _timeLogs, _cache) = (users, tasks, timeLogs, cache);
    }

    public async Task<Result<TeamReportDto>> Handle(GetTeamReportQuery _, CancellationToken ct)
    {
        var cached = await _cache.GetAsync<TeamReportDto>(CacheKey, ct);
        if (cached is not null) return Result.Success(cached);

        var allUsers = await _users.GetAllAsync(ct);
        var developers = allUsers.Where(u => u.IsActive).ToList();

        var summaries = new List<DeveloperSummaryDto>();
        foreach (var dev in developers)
        {
            var allTasks   = await _tasks.GetByAssigneeAsync(dev.Id, ct);
            var todayTasks = await _tasks.GetByAssigneeTodayAsync(dev.Id, ct);
            var hours      = await _timeLogs.GetTotalHoursByUserTodayAsync(dev.Id, ct);
            var completed  = allTasks.Where(t => t.Status == TaskStatus.Completed && t.ActualMinutes > 0).ToList();

            summaries.Add(new DeveloperSummaryDto(
                DeveloperId:         dev.Id,
                Name:                dev.FullName,
                Email:               dev.Email,
                TotalTasks:          allTasks.Count,
                CompletedToday:      todayTasks.Count(t => t.Status == TaskStatus.Completed),
                InProgress:          allTasks.Count(t => t.Status == TaskStatus.InProgress),
                Pending:             allTasks.Count(t => t.Status == TaskStatus.Pending),
                TotalHoursToday:     Math.Round(hours, 1),
                AverageTaskDuration: completed.Any() ? Math.Round(completed.Average(t => t.ActualMinutes), 0) : 0,
                LastActivityAt:      allTasks.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt).FirstOrDefault()?.UpdatedAt));
        }

        var totalTasks     = summaries.Sum(s => s.TotalTasks);
        var totalCompleted = summaries.Sum(s => s.CompletedToday);
        var overall        = totalTasks == 0 ? 0 : Math.Round((double)totalCompleted / totalTasks * 100, 1);

        var dto = new TeamReportDto(
            Date:              DateTime.UtcNow.ToString("yyyy-MM-dd"),
            OverallCompletion: overall,
            Developers:        summaries);

        await _cache.SetAsync(CacheKey, dto, TimeSpan.FromMinutes(2), ct);
        return Result.Success(dto);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  STATUS BREAKDOWN
// ══════════════════════════════════════════════════════════════════════════════

public record GetStatusBreakdownQuery : IRequest<Result<StatusBreakdownDto>>;

public sealed class GetStatusBreakdownQueryHandler
    : IRequestHandler<GetStatusBreakdownQuery, Result<StatusBreakdownDto>>
{
    private readonly ITaskRepository _tasks;
    private readonly ICacheService   _cache;

    public GetStatusBreakdownQueryHandler(ITaskRepository tasks, ICacheService cache)
        => (_tasks, _cache) = (tasks, cache);

    public async Task<Result<StatusBreakdownDto>> Handle(GetStatusBreakdownQuery _, CancellationToken ct)
    {
        const string cacheKey = "dashboard:status";
        var cached = await _cache.GetAsync<StatusBreakdownDto>(cacheKey, ct);
        if (cached is not null) return Result.Success(cached);

        var breakdown = await _tasks.GetStatusBreakdownAsync(ct);
        var dto = new StatusBreakdownDto(
            Pending:    breakdown.GetValueOrDefault(TaskStatus.Pending),
            InProgress: breakdown.GetValueOrDefault(TaskStatus.InProgress),
            Completed:  breakdown.GetValueOrDefault(TaskStatus.Completed),
            Blocked:    breakdown.GetValueOrDefault(TaskStatus.Blocked));

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(2), ct);
        return Result.Success(dto);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  VELOCITY TREND
// ══════════════════════════════════════════════════════════════════════════════

public record GetVelocityQuery(int Days = 7) : IRequest<Result<IReadOnlyList<VelocityPointDto>>>;

public sealed class GetVelocityQueryHandler
    : IRequestHandler<GetVelocityQuery, Result<IReadOnlyList<VelocityPointDto>>>
{
    private readonly ITimeLogRepository _timeLogs;
    private readonly ICacheService      _cache;

    public GetVelocityQueryHandler(ITimeLogRepository timeLogs, ICacheService cache)
        => (_timeLogs, _cache) = (timeLogs, cache);

    public async Task<Result<IReadOnlyList<VelocityPointDto>>> Handle(GetVelocityQuery q, CancellationToken ct)
    {
        var days     = Math.Clamp(q.Days, 1, 90);
        var cacheKey = $"dashboard:velocity:{days}";

        var cached = await _cache.GetAsync<List<VelocityPointDto>>(cacheKey, ct);
        if (cached is not null) return Result.Success<IReadOnlyList<VelocityPointDto>>(cached);

        var data   = await _timeLogs.GetVelocityAsync(days, ct);
        var points = data
            .Select(d => new VelocityPointDto(
                Date:           d.Date.ToString("yyyy-MM-dd"),
                TasksCompleted: d.TasksCompleted,
                HoursLogged:    Math.Round(d.HoursLogged, 1)))
            .ToList();

        await _cache.SetAsync(cacheKey, points, TimeSpan.FromMinutes(2), ct);
        return Result.Success<IReadOnlyList<VelocityPointDto>>(points);
    }
}
