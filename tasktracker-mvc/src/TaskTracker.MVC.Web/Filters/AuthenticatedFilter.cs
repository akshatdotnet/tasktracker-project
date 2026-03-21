using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TaskTracker.MVC.Application.Interfaces;

namespace TaskTracker.MVC.Web.Filters;

/// <summary>
/// Action filter that protects dashboard routes.
/// Checks session token validity and silently refreshes if expired.
/// Apply with [ServiceFilter(typeof(AuthenticatedFilter))] on controllers.
/// </summary>
public sealed class AuthenticatedFilter : IAsyncActionFilter
{
    private readonly ITokenSessionService _session;
    private readonly IAuthService         _authSvc;

    public AuthenticatedFilter(ITokenSessionService session, IAuthService authSvc)
    {
        _session = session;
        _authSvc = authSvc;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Not authenticated at all → redirect to login
        if (string.IsNullOrEmpty(_session.GetAccessToken()))
        {
            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }

        // Token expired → try silent refresh
        if (_session.IsExpired())
        {
            var refreshToken = _session.GetRefreshToken();
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var refreshed = await _authSvc.RefreshTokenAsync(refreshToken);
                if (refreshed != null)
                {
                    _session.SaveTokens(refreshed);
                    await next();
                    return;
                }
            }

            // Refresh failed → clear session and redirect
            _session.ClearTokens();
            context.Result = new RedirectToActionResult("Login", "Auth",
                new { returnUrl = context.HttpContext.Request.Path });
            return;
        }

        await next();
    }
}
