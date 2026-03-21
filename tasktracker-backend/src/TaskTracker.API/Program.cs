using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskTracker.API.Hubs;
using TaskTracker.API.Middleware;
using TaskTracker.Application;
using TaskTracker.Infrastructure;
using TaskTracker.Infrastructure.Persistence;
using TaskTracker.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Application + Infrastructure ─────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── JWT Authentication (configured here in API layer — requires Web SDK) ──────
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JwtSettings section missing from appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // KeyId must match exactly what JwtTokenService sets when generating tokens.
        // Mismatch causes IDX10517: Signature validation failed (kid missing).
        var validationKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
        {
            KeyId = "tasktracker-key-v1"
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSettings.Issuer,
            ValidAudience            = jwtSettings.Audience,
            IssuerSigningKey         = validationKey,
            ClockSkew                = TimeSpan.Zero
        };

        // Allow JWT via query string for SignalR hub connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.Request.Path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opts.JsonSerializerOptions.PropertyNamingPolicy   = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ── API Versioning ────────────────────────────────────────────────────────────
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion                   = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions                   = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-API-Version"));
}).AddApiExplorer(options =>
{
    options.GroupNameFormat           = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "TaskTracker API",
        Version     = "v1",
        Description = "RESTful API — Clean Architecture + CQRS + JWT Auth + SignalR"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Paste your JWT access token here (without Bearer prefix).",
        Reference    = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    opts.AddSecurityDefinition("Bearer", securityScheme);
    opts.AddSecurityRequirement(new OpenApiSecurityRequirement
        { { securityScheme, Array.Empty<string>() } });
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("CorsSettings:AllowedOrigins")
    .Get<string[]>() ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());

    options.AddPolicy("Production", policy =>
        policy.WithOrigins(allowedOrigins)
              .WithHeaders("Authorization", "Content-Type", "Idempotency-Key")
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
              .AllowCredentials());
});

// ── Rate Limiting (built-in to .NET 8 — no NuGet needed) ─────────────────────
builder.Services.AddRateLimiter(opts =>
{
    opts.AddSlidingWindowLimiter("general", limiter =>
    {
        limiter.PermitLimit          = 60;
        limiter.Window               = TimeSpan.FromMinutes(1);
        limiter.SegmentsPerWindow    = 6;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit           = 5;
    });

    opts.AddSlidingWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit       = 10;
        limiter.Window            = TimeSpan.FromMinutes(1);
        limiter.SegmentsPerWindow = 6;
    });

    opts.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.Headers["Retry-After"] = "60";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            error      = "Too many requests. Please slow down.",
            retryAfter = "60 seconds"
        }, ct);
    };
});

// ── SignalR (built-in to .NET 8 Web SDK) ──────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddScoped<IDashboardNotifier, DashboardNotifier>();

// ═════════════════════════════════════════════════════════════════════════════
var app = builder.Build();

// Seed database on first startup (runs migrations + inserts test data)
await DatabaseSeeder.SeedAsync(app.Services);

// Global exception handler — must be first
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opts =>
    {
        opts.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskTracker API v1");
        opts.RoutePrefix   = "swagger";
        opts.DocumentTitle = "TaskTracker API";
    });
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("general");
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();
