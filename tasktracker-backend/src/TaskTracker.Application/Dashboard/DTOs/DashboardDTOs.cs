namespace TaskTracker.Application.Dashboard.DTOs;

public record DashboardOverviewDto(
    int      TotalTasks,
    int      CompletedToday,
    int      InProgress,
    int      Blocked,
    double   TeamVelocity,    // avg tasks completed per day (7-day window)
    double   SprintProgress,  // 0–100 percentage
    DateTime LastRefreshed);

public record DeveloperSummaryDto(
    Guid      DeveloperId,
    string    Name,
    string    Email,
    int       TotalTasks,
    int       CompletedToday,
    int       InProgress,
    int       Pending,
    double    TotalHoursToday,
    double    AverageTaskDuration,  // minutes
    DateTime? LastActivityAt);

public record TimelineEntryDto(
    Guid      TaskId,
    string    Title,
    string    Status,
    string    Priority,
    DateTime? StartedAt,
    DateTime? EndedAt,
    int       DurationMinutes);

public record TodayTimelineDto(
    Guid                        DeveloperId,
    string                      DeveloperName,
    IReadOnlyList<TimelineEntryDto> Entries);

public record TeamReportDto(
    string                              Date,
    double                              OverallCompletion,
    IReadOnlyList<DeveloperSummaryDto>  Developers);

public record StatusBreakdownDto(
    int Pending,
    int InProgress,
    int Completed,
    int Blocked);

public record VelocityPointDto(
    string Date,    // yyyy-MM-dd
    int    TasksCompleted,
    double HoursLogged);
