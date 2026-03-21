using TaskTracker.MVC.Application.DTOs.Auth;
using TaskTracker.MVC.Application.DTOs.Dashboard;

namespace TaskTracker.MVC.Application.Interfaces;

// ── Auth Service Interface ─────────────────────────────────────────────────────

/// <summary>
/// ISP: Auth operations only. Concrete implementation in Infrastructure.
/// </summary>
public interface IAuthService
{
    Task<LoginResponseDto?>         LoginAsync         (LoginRequestDto request,       CancellationToken ct = default);
    Task<LoginResponseDto?>         RefreshTokenAsync  (string refreshToken,            CancellationToken ct = default);
    Task                            ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken ct = default);
    Task<ValidateTokenResponseDto>  ValidateTokenAsync (string token,                   CancellationToken ct = default);
    Task<bool>                      ResetPasswordAsync (ResetPasswordRequestDto request, CancellationToken ct = default);
}

// ── Dashboard Service Interface ───────────────────────────────────────────────

/// <summary>
/// ISP: Dashboard read operations only.
/// </summary>
public interface IDashboardService
{
    Task<DashboardOverviewDto?>        GetOverviewAsync      (CancellationToken ct = default);
    Task<TeamReportDto?>               GetTeamReportAsync    (CancellationToken ct = default);
    Task<DeveloperSummaryDto?>         GetDeveloperAsync     (string developerId, CancellationToken ct = default);
    Task<TodayTimelineDto?>            GetDeveloperTodayAsync(string developerId, CancellationToken ct = default);
    Task<StatusBreakdownDto?>          GetStatusBreakdownAsync(CancellationToken ct = default);
    Task<List<VelocityPointDto>>       GetVelocityAsync      (int days = 7,       CancellationToken ct = default);
}

// ── Token Session Interface ────────────────────────────────────────────────────

/// <summary>
/// ISP: Session token management. MVC stores tokens server-side in session
/// so the browser never sees raw JWT tokens (more secure than Angular SPA).
/// </summary>
public interface ITokenSessionService
{
    void   SaveTokens      (LoginResponseDto response);
    string? GetAccessToken ();
    string? GetRefreshToken();
    bool    IsAuthenticated();
    bool    IsExpired      ();
    void    ClearTokens    ();
    string? GetUserId      ();
    string? GetUserRole    ();
    string? GetUserFullName();
}
