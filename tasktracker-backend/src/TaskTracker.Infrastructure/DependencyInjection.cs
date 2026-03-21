using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskTracker.Application.Common.Interfaces;
using TaskTracker.Domain.Interfaces;
using TaskTracker.Infrastructure.Persistence;
using TaskTracker.Infrastructure.Persistence.Repositories;
using TaskTracker.Infrastructure.Services;

namespace TaskTracker.Infrastructure;

/// <summary>
/// Infrastructure layer DI registration.
/// Called from API's Program.cs: builder.Services.AddInfrastructure(config);
/// NOTE: JWT authentication middleware is configured in the API layer (Program.cs)
///       because it requires Microsoft.AspNetCore.Authentication which belongs in
///       the Web SDK project, not a class library.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(3)));

        // IUnitOfWork resolves to AppDbContext (same scoped instance)
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // ── Repositories ──────────────────────────────────────────────────
        services.AddScoped<IUserRepository,              UserRepository>();
        services.AddScoped<IRefreshTokenRepository,       RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IProjectRepository,            ProjectRepository>();
        services.AddScoped<ISprintRepository,             SprintRepository>();
        services.AddScoped<ITaskRepository,               TaskRepository>();
        services.AddScoped<ITimeLogRepository,            TimeLogRepository>();

        // ── Application Services ──────────────────────────────────────────
        services.AddSingleton<IJwtTokenService,     JwtTokenService>();
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddSingleton<IDateTimeService,     DateTimeService>();
        services.AddScoped<ICurrentUserService,     CurrentUserService>();
        services.AddScoped<IEmailService,           ConsoleEmailService>();
        services.AddSingleton<ICacheService,        MemoryCacheService>();

        // ── Settings ──────────────────────────────────────────────────────
        services.Configure<JwtSettings>     (configuration.GetSection("JwtSettings"));
        services.Configure<SecuritySettings>(configuration.GetSection("SecuritySettings"));

        // ── Memory Cache ──────────────────────────────────────────────────
        services.AddMemoryCache();

        // ── HttpContextAccessor (for CurrentUserService) ──────────────────
        services.AddHttpContextAccessor();

        return services;
    }
}
