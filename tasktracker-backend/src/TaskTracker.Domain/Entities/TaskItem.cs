using TaskTracker.Domain.Common;
using TaskTracker.Domain.Enums;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Domain.Entities;

/// <summary>Project — top-level grouping for tasks and sprints.</summary>
public sealed class Project : AuditableEntity
{
    public string  Name        { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool    IsActive    { get; private set; } = true;

    public ICollection<TaskItem> Tasks   { get; private set; } = new List<TaskItem>();
    public ICollection<Sprint>   Sprints { get; private set; } = new List<Sprint>();

    private Project() { }

    public static Project Create(string name, string? description = null)
        => new() { Name = name.Trim(), Description = description };

    public void Update(string name, string? description)
    {
        Name        = name.Trim();
        Description = description;
        UpdatedAt   = DateTime.UtcNow;
    }
}

/// <summary>
/// Sprint — a time-boxed iteration within a project.
/// Sprint progress is computed from elapsed/total days.
/// </summary>
public sealed class Sprint : AuditableEntity
{
    public Guid     ProjectId { get; private set; }
    public string   Name      { get; private set; } = default!;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate   { get; private set; }
    public bool     IsActive  { get; private set; }

    public Project            Project { get; private set; } = default!;
    public ICollection<TaskItem> Tasks { get; private set; } = new List<TaskItem>();

    private Sprint() { }

    public static Sprint Create(Guid projectId, string name, DateTime start, DateTime end)
        => new() { ProjectId = projectId, Name = name, StartDate = start, EndDate = end, IsActive = true };

    /// <summary>0–100 sprint completion percentage based on calendar days.</summary>
    public double ProgressPercent
    {
        get
        {
            var total   = (EndDate - StartDate).TotalDays;
            var elapsed = (DateTime.UtcNow - StartDate).TotalDays;
            if (total <= 0) return 0;
            return Math.Round(Math.Min(elapsed / total * 100, 100), 1);
        }
    }
}

/// <summary>
/// TaskItem — core business entity. Tracks developer work items through their lifecycle.
/// Status transitions are enforced through methods, not direct property sets.
/// </summary>
public sealed class TaskItem : AuditableEntity
{
    public Guid         ProjectId     { get; private set; }
    public Guid?        SprintId      { get; private set; }
    public Guid?        AssignedToId  { get; private set; }
    public string       Title         { get; private set; } = default!;
    public string?      Description   { get; private set; }
    public TaskStatus   Status        { get; private set; } = TaskStatus.Pending;
    public TaskPriority Priority      { get; private set; } = TaskPriority.Medium;
    public DateTime?    StartedAt     { get; private set; }
    public DateTime?    CompletedAt   { get; private set; }
    public int          EstimatedMinutes { get; private set; }

    // Navigation
    public Project   Project    { get; private set; } = default!;
    public Sprint?   Sprint     { get; private set; }
    public User?     AssignedTo { get; private set; }
    public ICollection<TimeLog> TimeLogs { get; private set; } = new List<TimeLog>();

    private TaskItem() { }

    public static TaskItem Create(
        Guid projectId, string title,
        Guid? assignedToId    = null,
        Guid? sprintId        = null,
        TaskPriority priority = TaskPriority.Medium,
        int estimatedMinutes  = 60,
        string? description   = null)
        => new()
        {
            ProjectId        = projectId,
            SprintId         = sprintId,
            Title            = title.Trim(),
            Description      = description,
            AssignedToId     = assignedToId,
            Priority         = priority,
            EstimatedMinutes = estimatedMinutes,
        };

    // ── Status transition methods ────────────────────────────────────────
    public void Start()
    {
        Status    = TaskStatus.InProgress;
        StartedAt ??= DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status      = TaskStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        StartedAt ??= DateTime.UtcNow;
        UpdatedAt   = DateTime.UtcNow;
    }

    public void Block()
    {
        Status    = TaskStatus.Blocked;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        Status      = TaskStatus.Pending;
        CompletedAt = null;
        UpdatedAt   = DateTime.UtcNow;
    }

    public void UpdateDetails(string title, string? description, TaskPriority priority, int estimatedMinutes)
    {
        Title            = title.Trim();
        Description      = description;
        Priority         = priority;
        EstimatedMinutes = estimatedMinutes;
        UpdatedAt        = DateTime.UtcNow;
    }

    /// <summary>Total actual minutes from all time log entries.</summary>
    public int ActualMinutes => TimeLogs.Sum(t => t.DurationMinutes);

    /// <summary>True if the task was touched (started or completed) today.</summary>
    public bool IsTouched =>
        (StartedAt.HasValue   && StartedAt.Value.Date   == DateTime.UtcNow.Date) ||
        (CompletedAt.HasValue && CompletedAt.Value.Date == DateTime.UtcNow.Date) ||
        (UpdatedAt.HasValue   && UpdatedAt.Value.Date   == DateTime.UtcNow.Date);
}

/// <summary>
/// TimeLog — records hours spent by a developer on a task.
/// Feeds velocity calculations and hours-logged dashboard metrics.
/// </summary>
public sealed class TimeLog : BaseEntity
{
    public Guid     TaskId          { get; private set; }
    public Guid     UserId          { get; private set; }
    public DateTime LoggedAt        { get; private set; }
    public int      DurationMinutes { get; private set; }
    public string?  Notes           { get; private set; }

    public TaskItem Task { get; private set; } = default!;
    public User     User { get; private set; } = default!;

    private TimeLog() { }

    public static TimeLog Create(
        Guid taskId, Guid userId, int durationMinutes,
        DateTime? loggedAt = null, string? notes = null)
        => new()
        {
            TaskId          = taskId,
            UserId          = userId,
            DurationMinutes = durationMinutes,
            LoggedAt        = loggedAt ?? DateTime.UtcNow,
            Notes           = notes,
        };

    public double DurationHours => Math.Round(DurationMinutes / 60.0, 2);
}
