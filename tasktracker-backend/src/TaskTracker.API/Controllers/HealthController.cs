using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace TaskTracker.API.Controllers;

/// <summary>Abstract base controller with shared helpers.</summary>
[ApiController]
public abstract class BaseController : ControllerBase { }

/// <summary>Health check endpoint for load balancers and monitoring.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns 200 OK when API is running.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status    = "healthy",
        timestamp = DateTime.UtcNow,
        version   = "1.0",
        service   = "TaskTracker.API"
    });
}
