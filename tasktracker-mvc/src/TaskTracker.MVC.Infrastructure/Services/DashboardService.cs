using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaskTracker.MVC.Application.DTOs.Dashboard;
using TaskTracker.MVC.Application.Interfaces;

namespace TaskTracker.MVC.Infrastructure.Services;

/// <summary>
/// Calls the existing TaskTracker .NET Core 8 API dashboard endpoints.
/// Attaches the JWT Bearer token from session on every request.
/// SRP: only handles HTTP calls to /api/v1/dashboard/* endpoints.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly HttpClient _http;
    private readonly ITokenSessionService _session;
    private readonly ILogger<DashboardService> _log;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DashboardService(HttpClient http, ITokenSessionService session, ILogger<DashboardService> log)
    {
        _http    = http;
        _session = session;
        _log     = log;
    }

    // ── GET /api/v1/dashboard/overview ────────────────────────────────────────
    public async Task<DashboardOverviewDto?> GetOverviewAsync(CancellationToken ct = default)
        => await GetAsync<DashboardOverviewDto>("dashboard/overview", ct);

    // ── GET /api/v1/dashboard/team ────────────────────────────────────────────
    public async Task<TeamReportDto?> GetTeamReportAsync(CancellationToken ct = default)
        => await GetAsync<TeamReportDto>("dashboard/team", ct);

    // ── GET /api/v1/dashboard/developer/{id} ──────────────────────────────────
    public async Task<DeveloperSummaryDto?> GetDeveloperAsync(string developerId, CancellationToken ct = default)
        => await GetAsync<DeveloperSummaryDto>($"dashboard/developer/{developerId}", ct);

    // ── GET /api/v1/dashboard/developer/{id}/today ────────────────────────────
    public async Task<TodayTimelineDto?> GetDeveloperTodayAsync(string developerId, CancellationToken ct = default)
        => await GetAsync<TodayTimelineDto>($"dashboard/developer/{developerId}/today", ct);

    // ── GET /api/v1/dashboard/tasks/by-status ────────────────────────────────
    public async Task<StatusBreakdownDto?> GetStatusBreakdownAsync(CancellationToken ct = default)
        => await GetAsync<StatusBreakdownDto>("dashboard/tasks/by-status", ct);

    // ── GET /api/v1/dashboard/velocity?days=N ────────────────────────────────
    public async Task<List<VelocityPointDto>> GetVelocityAsync(int days = 7, CancellationToken ct = default)
        => await GetAsync<List<VelocityPointDto>>($"dashboard/velocity?days={days}", ct)
           ?? new List<VelocityPointDto>();

    // ── Private helper — attaches Bearer token and handles errors ─────────────
    private async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct) where T : class
    {
        try
        {
            var token = _session.GetAccessToken();
            if (!string.IsNullOrEmpty(token))
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _http.GetAsync(endpoint, ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Dashboard API [{Endpoint}] returned {Status}",
                    endpoint, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<T>(_json, ct);
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "Dashboard API call failed: {Endpoint}", endpoint);
            return null;
        }
    }
}
