using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Common.Models;
using TaskTracker.Application.Dashboard.DTOs;
using TaskTracker.Application.Dashboard.Queries;

namespace TaskTracker.API.Controllers;

/// <summary>
/// Dashboard analytics endpoints — all require JWT authentication.
/// Results are cached server-side for 2 minutes.
/// SignalR hub pushes real-time updates when tasks change.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboard")]
[Authorize]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    // ── GET /api/v1/dashboard/overview ────────────────────────────────────

    /// <summary>Full dashboard snapshot — total tasks, sprint progress, team velocity.</summary>
    /// <response code="200">Dashboard overview returned.</response>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(DashboardOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOverviewQuery(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse(result.Error!));
    }

    // ── GET /api/v1/dashboard/developer/{id} ──────────────────────────────

    /// <summary>Summary stats for a specific developer.</summary>
    /// <param name="id">Developer's UserId (GUID).</param>
    /// <response code="200">Developer summary returned.</response>
    /// <response code="404">Developer not found.</response>
    [HttpGet("developer/{id:guid}")]
    [ProducesResponseType(typeof(DeveloperSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse),        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeveloper(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDeveloperQuery(id), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ErrorResponse(result.Error!, "DEVELOPER_NOT_FOUND"));
    }

    // ── GET /api/v1/dashboard/developer/{id}/today ────────────────────────

    /// <summary>Today's task timeline for a specific developer.</summary>
    /// <param name="id">Developer's UserId (GUID).</param>
    /// <response code="200">Today's timeline returned.</response>
    /// <response code="404">Developer not found.</response>
    [HttpGet("developer/{id:guid}/today")]
    [ProducesResponseType(typeof(TodayTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse),     StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeveloperToday(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDeveloperTodayQuery(id), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ErrorResponse(result.Error!, "DEVELOPER_NOT_FOUND"));
    }

    // ── GET /api/v1/dashboard/team ────────────────────────────────────────

    /// <summary>Full team report — all active developers with today's stats.</summary>
    /// <response code="200">Team report returned.</response>
    [HttpGet("team")]
    [ProducesResponseType(typeof(TeamReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamReport(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTeamReportQuery(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse(result.Error!));
    }

    // ── GET /api/v1/dashboard/tasks/by-status ────────────────────────────

    /// <summary>Task count grouped by status: pending, inProgress, completed, blocked.</summary>
    /// <response code="200">Status breakdown returned.</response>
    [HttpGet("tasks/by-status")]
    [ProducesResponseType(typeof(StatusBreakdownDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatusBreakdown(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStatusBreakdownQuery(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse(result.Error!));
    }

    // ── GET /api/v1/dashboard/velocity?days=7 ────────────────────────────

    /// <summary>N-day velocity trend — tasks completed and hours logged per day.</summary>
    /// <param name="days">Number of days to include (1–90). Default: 7.</param>
    /// <response code="200">Velocity trend data returned.</response>
    [HttpGet("velocity")]
    [ProducesResponseType(typeof(IReadOnlyList<VelocityPointDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVelocity(
        [FromQuery] int days = 7,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetVelocityQuery(days), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse(result.Error!));
    }
}
