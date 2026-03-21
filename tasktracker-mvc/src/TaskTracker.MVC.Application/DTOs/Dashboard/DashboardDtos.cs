namespace TaskTracker.MVC.Application.DTOs.Dashboard;

public sealed class DashboardOverviewDto
{
    public int      TotalTasks     { get; set; }
    public int      CompletedToday { get; set; }
    public int      InProgress     { get; set; }
    public int      Blocked        { get; set; }
    public double   TeamVelocity   { get; set; }
    public double   SprintProgress { get; set; }
    public DateTime LastRefreshed  { get; set; }
}

public sealed class DeveloperSummaryDto
{
    public string   DeveloperId         { get; set; } = string.Empty;
    public string   Name                { get; set; } = string.Empty;
    public string   Email               { get; set; } = string.Empty;
    public int      TotalTasks          { get; set; }
    public int      CompletedToday      { get; set; }
    public int      InProgress          { get; set; }
    public int      Pending             { get; set; }
    public double   TotalHoursToday     { get; set; }
    public double   AverageTaskDuration { get; set; }
    public DateTime? LastActivityAt     { get; set; }
    public string   Initials            => Name.Length >= 2 ? Name[..2].ToUpper() : Name.ToUpper();
}

public sealed class TimelineEntryDto
{
    public string    TaskId          { get; set; } = string.Empty;
    public string    Title           { get; set; } = string.Empty;
    public string    Status          { get; set; } = string.Empty;
    public string    Priority        { get; set; } = string.Empty;
    public DateTime? StartedAt       { get; set; }
    public DateTime? EndedAt         { get; set; }
    public int       DurationMinutes { get; set; }
    public string    StatusClass     => Status.ToLower().Replace(" ", "");
}

public sealed class TodayTimelineDto
{
    public string                   DeveloperId   { get; set; } = string.Empty;
    public string                   DeveloperName { get; set; } = string.Empty;
    public List<TimelineEntryDto>   Entries       { get; set; } = new();
}

public sealed class TeamReportDto
{
    public string                     Date              { get; set; } = string.Empty;
    public double                     OverallCompletion { get; set; }
    public List<DeveloperSummaryDto>  Developers        { get; set; } = new();
}

public sealed class StatusBreakdownDto
{
    public int Pending    { get; set; }
    public int InProgress { get; set; }
    public int Completed  { get; set; }
    public int Blocked    { get; set; }
    public int Total      => Pending + InProgress + Completed + Blocked;
}

public sealed class VelocityPointDto
{
    public string Date           { get; set; } = string.Empty;
    public int    TasksCompleted { get; set; }
    public double HoursLogged    { get; set; }
}
