namespace TaskTracker.MVC.Infrastructure.HttpClients;

/// <summary>
/// Bound from appsettings.json ApiSettings section.
/// UseMockData: true  = in-memory mock services (no API needed)
/// UseMockData: false = real HTTP calls to the .NET Core 8 REST API
/// </summary>
public sealed class ApiSettings
{
    public string BaseUrl        { get; init; } = "http://localhost:5000/api/v1";
    public int    TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Toggle between mock data and live API — same as Angular useMockData flag.
    /// true  = use MockAuthService + MockDashboardService (hardcoded data, no API needed)
    /// false = use AuthService + DashboardService (calls http://localhost:5000)
    /// </summary>
    public bool UseMockData { get; init; } = false;
}
