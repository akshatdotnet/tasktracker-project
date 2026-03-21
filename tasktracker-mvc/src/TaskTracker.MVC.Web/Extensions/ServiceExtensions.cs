using TaskTracker.MVC.Web.Filters;

namespace TaskTracker.MVC.Web.Extensions;

/// <summary>
/// Extension methods to keep Program.cs clean and readable.
/// OCP: add new registrations here without touching Program.cs.
/// </summary>
public static class ServiceExtensions
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        // Auth filter registered as service so DI injects dependencies into it
        services.AddScoped<AuthenticatedFilter>();

        return services;
    }

    public static WebApplication UseWebMiddleware(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseSession();
        return app;
    }
}
