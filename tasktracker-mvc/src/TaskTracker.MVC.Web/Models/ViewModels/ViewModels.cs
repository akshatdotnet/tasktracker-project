using TaskTracker.MVC.Application.DTOs.Auth;
using TaskTracker.MVC.Application.DTOs.Dashboard;

namespace TaskTracker.MVC.Web.Models.ViewModels;

// ── Auth ViewModels ───────────────────────────────────────────────────────────

public sealed class LoginViewModel
{
    public LoginRequestDto Form      { get; set; } = new();
    public string?         ApiError  { get; set; }
    public bool            IsApiDown { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    public ForgotPasswordRequestDto Form    { get; set; } = new();
    public bool                     Sent    { get; set; }
    public string?                  ApiError{ get; set; }
}

public sealed class ResetPasswordViewModel
{
    public ResetPasswordRequestDto Form     { get; set; } = new();
    public string                  State    { get; set; } = "checking"; // checking | valid | invalid | done
    public string?                 ApiError { get; set; }
}

// ── Dashboard ViewModels ──────────────────────────────────────────────────────

public sealed class OverviewViewModel
{
    public DashboardOverviewDto?       Overview { get; set; }
    public TeamReportDto?              Team     { get; set; }
    public string?                     Error    { get; set; }
    public string                      UserName { get; set; } = string.Empty;
    public string                      UserRole { get; set; } = string.Empty;
    public string                      Initials { get; set; } = string.Empty;
    public bool                        IsLive   { get; set; } = true;
}

public sealed class TeamViewModel
{
    public TeamReportDto? Report  { get; set; }
    public string?        Error   { get; set; }
    public string         UserName{ get; set; } = string.Empty;
    public string         UserRole{ get; set; } = string.Empty;
    public string         Initials{ get; set; } = string.Empty;
}

public sealed class DeveloperViewModel
{
    public DeveloperSummaryDto? Developer { get; set; }
    public TodayTimelineDto?    Timeline  { get; set; }
    public string?              Error     { get; set; }
    public string               UserName  { get; set; } = string.Empty;
    public string               UserRole  { get; set; } = string.Empty;
    public string               Initials  { get; set; } = string.Empty;
}

public sealed class VelocityViewModel
{
    public List<VelocityPointDto> Points  { get; set; } = new();
    public StatusBreakdownDto?    Status  { get; set; }
    public int                    Days    { get; set; } = 7;
    public string?                Error   { get; set; }
    public string                 UserName{ get; set; } = string.Empty;
    public string                 UserRole{ get; set; } = string.Empty;
    public string                 Initials{ get; set; } = string.Empty;
}
