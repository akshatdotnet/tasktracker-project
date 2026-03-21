namespace TaskTracker.Domain.Enums;

/// <summary>User role controlling API access level.</summary>
public enum UserRole
{
    Admin     = 1,  // Full access: all developers, system config
    Developer = 2,  // Own timeline, team overview
    Viewer    = 3   // Read-only dashboard
}

/// <summary>Task lifecycle status.</summary>
public enum TaskStatus
{
    Pending    = 1,
    InProgress = 2,
    Completed  = 3,
    Blocked    = 4
}

/// <summary>Task urgency level.</summary>
public enum TaskPriority
{
    Low      = 1,
    Medium   = 2,
    High     = 3,
    Critical = 4
}
