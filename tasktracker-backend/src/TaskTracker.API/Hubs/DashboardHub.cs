using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaskTracker.API.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard updates.
/// Angular connects using: new HubConnectionBuilder().withUrl("/hubs/dashboard").build()
/// JWT token passed as ?access_token=... query param (configured in JwtBearer events).
/// </summary>
[Authorize]
public sealed class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("Dashboard client connected: {UserId}", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Dashboard client disconnected: {UserId}", Context.UserIdentifier);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by Angular to join a project-specific group.
    /// Allows scoped broadcasts (only users on the same project get updates).
    /// </summary>
    public async Task JoinProject(string projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project:{projectId}");
        _logger.LogInformation("User {UserId} joined project group {ProjectId}",
            Context.UserIdentifier, projectId);
    }

    public async Task LeaveProject(string projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project:{projectId}");
    }
}

/// <summary>
/// Service for pushing SignalR events from other services/background jobs.
/// Inject IHubContext&lt;DashboardHub&gt; and call methods to push updates.
/// </summary>
public interface IDashboardNotifier
{
    Task NotifyTaskUpdatedAsync(Guid taskId, string status, CancellationToken ct = default);
    Task NotifyOverviewRefreshedAsync(CancellationToken ct = default);
}

public sealed class DashboardNotifier : IDashboardNotifier
{
    private readonly IHubContext<DashboardHub> _hub;

    public DashboardNotifier(IHubContext<DashboardHub> hub) => _hub = hub;

    /// <summary>Push task status change to all connected dashboard clients.</summary>
    public async Task NotifyTaskUpdatedAsync(Guid taskId, string status, CancellationToken ct = default)
        => await _hub.Clients.All.SendAsync("OnTaskUpdated", new { taskId, status }, ct);

    /// <summary>Tell all clients to refresh the overview panel.</summary>
    public async Task NotifyOverviewRefreshedAsync(CancellationToken ct = default)
        => await _hub.Clients.All.SendAsync("OnOverviewRefreshed", ct);
}
