using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskTracker.MVC.Application.Interfaces;
using TaskTracker.MVC.Infrastructure.HttpClients;
using TaskTracker.MVC.Infrastructure.Services;
using TaskTracker.MVC.Infrastructure.Services.Mock;

namespace TaskTracker.MVC.Infrastructure;

/// <summary>
/// Infrastructure DI registration.
/// Reads ApiSettings.UseMockData from appsettings.json and wires
/// either the real HTTP services or in-memory mock services.
/// This is the ONLY place that knows which implementation is used (DIP).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var apiSettings = configuration.GetSection("ApiSettings").Get<ApiSettings>()
                          ?? new ApiSettings();

        // Bind settings so they are available via IOptions<ApiSettings> if needed
        services.Configure<ApiSettings>(configuration.GetSection("ApiSettings"));

        if (apiSettings.UseMockData)
        {
            // ── MOCK MODE ──────────────────────────────────────────────────
            // No HTTP calls — serves hardcoded in-memory data.
            // Identical to Angular useMockData: true behaviour.
            services.AddScoped<IAuthService,      MockAuthService>();
            services.AddScoped<IDashboardService, MockDashboardService>();
        }
        else
        {
            // ── LIVE MODE ──────────────────────────────────────────────────
            // Typed HttpClients pointing at the .NET Core 8 REST API.
            // Set BaseUrl in appsettings.json ApiSettings.BaseUrl.
            services.AddHttpClient<IAuthService, AuthService>(client =>
            {
                client.BaseAddress = new Uri(apiSettings.BaseUrl.TrimEnd('/') + "/");
                client.Timeout     = TimeSpan.FromSeconds(apiSettings.TimeoutSeconds);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            services.AddHttpClient<IDashboardService, DashboardService>(client =>
            {
                client.BaseAddress = new Uri(apiSettings.BaseUrl.TrimEnd('/') + "/");
                client.Timeout     = TimeSpan.FromSeconds(apiSettings.TimeoutSeconds);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });
        }

        // Token session service — always real (stores in ASP.NET Core session)
        services.AddScoped<ITokenSessionService, TokenSessionService>();

        return services;
    }
}
