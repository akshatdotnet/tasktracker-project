using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaskTracker.MVC.Application.Interfaces;
using TaskTracker.MVC.Infrastructure.HttpClients;
using TaskTracker.MVC.Web.Filters;
using TaskTracker.MVC.Web.Models.ViewModels;

namespace TaskTracker.MVC.Web.Controllers;

/// <summary>
/// Dashboard pages — Overview, Team, Developer, Velocity.
/// Protected by AuthenticatedFilter (auto token-refresh on expiry).
/// SRP: orchestrates data fetch and view rendering only.
/// </summary>
[ServiceFilter(typeof(AuthenticatedFilter))]
public sealed class DashboardController : Controller
{
    private readonly IDashboardService    _dash;
    private readonly ITokenSessionService _session;
    private readonly bool                 _isMock;

    public DashboardController(
        IDashboardService dash,
        ITokenSessionService session,
        IOptions<ApiSettings> apiSettings)
    {
        _dash   = dash;
        _session = session;
        _isMock = apiSettings.Value.UseMockData;
    }

    // ── Shared user info helper ───────────────────────────────────────────────
    private (string name, string role, string initials) UserInfo()
    {
        var name     = _session.GetUserFullName() ?? "User";
        var role     = _session.GetUserRole()     ?? "Developer";
        var initials = string.Join("",
            name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2).Select(w => w[0])).ToUpper();
        return (name, role, initials);
    }

    // Passes IsMock to every view so the sidebar badge shows correctly
    private void SetViewBag(string activePage, string name, string role, string initials)
    {
        ViewBag.ActivePage = activePage;
        ViewBag.UserName   = name;
        ViewBag.UserRole   = role;
        ViewBag.Initials   = initials;
        ViewBag.IsMock     = _isMock;
    }

    // ── GET /dashboard ────────────────────────────────────────────────────────
    [HttpGet("/dashboard")]
    public IActionResult Index() => RedirectToAction(nameof(Overview));

    // ── GET /dashboard/overview ───────────────────────────────────────────────
    [HttpGet("/dashboard/overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var (name, role, initials) = UserInfo();
        SetViewBag("overview", name, role, initials);

        var vm = new OverviewViewModel
        {
            UserName = name, UserRole = role, Initials = initials, IsLive = !_isMock
        };

        try
        {
            vm.Overview = await _dash.GetOverviewAsync(ct);
            vm.Team     = await _dash.GetTeamReportAsync(ct);
            if (vm.Overview is null)
                vm.Error = "Could not load dashboard data. Check API connection or set UseMockData=true.";
        }
        catch (Exception ex) { vm.Error = ex.Message; }

        return View(vm);
    }

    // ── GET /dashboard/team ───────────────────────────────────────────────────
    [HttpGet("/dashboard/team")]
    public async Task<IActionResult> Team(CancellationToken ct)
    {
        var (name, role, initials) = UserInfo();
        SetViewBag("team", name, role, initials);

        var vm = new TeamViewModel { UserName = name, UserRole = role, Initials = initials };
        try
        {
            vm.Report = await _dash.GetTeamReportAsync(ct);
            if (vm.Report is null) vm.Error = "Could not load team data.";
        }
        catch (Exception ex) { vm.Error = ex.Message; }

        return View(vm);
    }

    // ── GET /dashboard/developer/{id} ─────────────────────────────────────────
    [HttpGet("/dashboard/developer/{id}")]
    public async Task<IActionResult> Developer(string id, CancellationToken ct)
    {
        var (name, role, initials) = UserInfo();
        SetViewBag("", name, role, initials);

        var vm = new DeveloperViewModel { UserName = name, UserRole = role, Initials = initials };
        try
        {
            vm.Developer = await _dash.GetDeveloperAsync(id, ct);
            vm.Timeline  = await _dash.GetDeveloperTodayAsync(id, ct);
            if (vm.Developer is null) vm.Error = "Developer not found.";
        }
        catch (Exception ex) { vm.Error = ex.Message; }

        return View(vm);
    }

    // ── GET /dashboard/velocity ───────────────────────────────────────────────
    [HttpGet("/dashboard/velocity")]
    public async Task<IActionResult> Velocity(int days = 7, CancellationToken ct = default)
    {
        var (name, role, initials) = UserInfo();
        SetViewBag("velocity", name, role, initials);

        var vm = new VelocityViewModel
        {
            Days = Math.Clamp(days, 7, 30),
            UserName = name, UserRole = role, Initials = initials
        };
        try
        {
            vm.Status = await _dash.GetStatusBreakdownAsync(ct);
            vm.Points = await _dash.GetVelocityAsync(vm.Days, ct);
        }
        catch (Exception ex) { vm.Error = ex.Message; }

        return View(vm);
    }
}
