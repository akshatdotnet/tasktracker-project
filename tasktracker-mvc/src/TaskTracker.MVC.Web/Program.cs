using Microsoft.Extensions.Options;
using TaskTracker.MVC.Infrastructure;
using TaskTracker.MVC.Infrastructure.HttpClients;
using TaskTracker.MVC.Web.Filters;

var builder = WebApplication.CreateBuilder(args);

// ── Bind ApiSettings FIRST so IOptions<ApiSettings> is available everywhere ───
builder.Services.Configure<ApiSettings>(
    builder.Configuration.GetSection("ApiSettings"));

// ── MVC ────────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(
        new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));

// ── Session ───────────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout         = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly     = true;
    options.Cookie.IsEssential  = true;
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite     = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
    options.Cookie.Name         = "__tt_session";
});

// ── HttpContextAccessor ───────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ── Infrastructure (reads UseMockData flag and wires correct services) ─────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Auth filter ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthenticatedFilter>();

var app = builder.Build();

// Log which mode is active on startup
var apiSettings = app.Services.GetRequiredService<IOptions<ApiSettings>>().Value;
app.Logger.LogInformation(
    "TaskTracker MVC starting — Mode: {Mode} | API: {Url}",
    apiSettings.UseMockData ? "MOCK" : "LIVE",
    apiSettings.UseMockData ? "N/A"  : apiSettings.BaseUrl);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(name: "default", pattern: "{controller=Auth}/{action=Login}/{id?}");
app.MapGet("/", () => Results.Redirect("/login"));

app.Run();
