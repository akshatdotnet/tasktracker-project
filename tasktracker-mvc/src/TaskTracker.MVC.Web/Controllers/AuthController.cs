using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaskTracker.MVC.Application.DTOs.Auth;
using TaskTracker.MVC.Application.Interfaces;
using TaskTracker.MVC.Infrastructure.HttpClients;
using TaskTracker.MVC.Web.Models.ViewModels;

namespace TaskTracker.MVC.Web.Controllers;

public sealed class AuthController : Controller
{
    private readonly IAuthService         _authSvc;
    private readonly ITokenSessionService _session;
    private readonly bool                 _isMock;

    public AuthController(
        IAuthService authSvc,
        ITokenSessionService session,
        IOptions<ApiSettings> apiSettings)
    {
        _authSvc = authSvc;
        _session = session;
        _isMock  = apiSettings.Value.UseMockData;
    }

    // ── GET /login ────────────────────────────────────────────────────────────
    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_session.IsAuthenticated())
            return RedirectToAction("Overview", "Dashboard");

        ViewBag.ReturnUrl = returnUrl;
        ViewBag.IsMock    = _isMock;
        return View(new LoginViewModel());
    }

    // ── POST /login ───────────────────────────────────────────────────────────
    // Accept flat fields directly to avoid nested model binding issues
    [HttpPost("/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        [FromForm(Name = "Form.Email")]    string email    = "",
        [FromForm(Name = "Form.Password")] string password = "",
        string? returnUrl = null)
    {
        ViewBag.IsMock = _isMock;

        var vm = new LoginViewModel();
        vm.Form.Email    = email;
        vm.Form.Password = password;

        // Manual validation
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            vm.ApiError = "Please enter a valid email address.";
            ModelState.AddModelError("Form.Email", "Enter a valid email.");
            return View(vm);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            vm.ApiError = "Password is required.";
            ModelState.AddModelError("Form.Password", "Password is required.");
            return View(vm);
        }

        var result = await _authSvc.LoginAsync(new LoginRequestDto
        {
            Email    = email.Trim(),
            Password = password
        });

        if (result is null)
        {
            vm.ApiError  = _isMock
                ? "Invalid credentials. Check the mock credentials list below."
                : "Invalid credentials or the API server is not reachable. Ensure dotnet run is running on port 5000.";
            vm.IsApiDown = !_isMock;
            return View(vm);
        }

        _session.SaveTokens(result);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Overview", "Dashboard");
    }

    // ── POST /logout ──────────────────────────────────────────────────────────
    [HttpPost("/logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _session.ClearTokens();
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    // ── GET /forgot-password ──────────────────────────────────────────────────
    [HttpGet("/forgot-password")]
    public IActionResult ForgotPassword()
    {
        ViewBag.IsMock = _isMock;
        return View(new ForgotPasswordViewModel());
    }

    // ── POST /forgot-password ─────────────────────────────────────────────────
    [HttpPost("/forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        [FromForm(Name = "Form.Email")] string email = "")
    {
        ViewBag.IsMock = _isMock;
        var vm = new ForgotPasswordViewModel();
        vm.Form.Email = email;

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            ModelState.AddModelError("Form.Email", "Enter a valid email.");
            return View(vm);
        }

        await _authSvc.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = email.Trim() });
        vm.Sent = true;
        return View(vm);
    }

    // ── GET /reset-password?token=xxx ─────────────────────────────────────────
    [HttpGet("/reset-password")]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        ViewBag.IsMock = _isMock;
        var vm = new ResetPasswordViewModel();

        if (string.IsNullOrEmpty(token))
        {
            vm.State = "invalid";
            return View(vm);
        }

        var validation = await _authSvc.ValidateTokenAsync(token);
        vm.State      = validation.IsValid ? "valid" : "invalid";
        vm.Form.Token = token;
        return View(vm);
    }

    // ── POST /reset-password ──────────────────────────────────────────────────
    [HttpPost("/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        [FromForm(Name = "Form.Token")]           string token           = "",
        [FromForm(Name = "Form.NewPassword")]     string newPassword     = "",
        [FromForm(Name = "Form.ConfirmPassword")] string confirmPassword = "")
    {
        ViewBag.IsMock = _isMock;
        var vm = new ResetPasswordViewModel { Form = { Token = token } };

        if (newPassword != confirmPassword)
        {
            vm.State    = "valid";
            vm.ApiError = "Passwords do not match.";
            return View(vm);
        }

        if (newPassword.Length < 8)
        {
            vm.State    = "valid";
            vm.ApiError = "Password must be at least 8 characters.";
            return View(vm);
        }

        var success = await _authSvc.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Token           = token,
            NewPassword     = newPassword,
            ConfirmPassword = confirmPassword
        });

        vm.State = success ? "done" : "valid";
        if (!success) vm.ApiError = "Reset token is invalid or has expired.";
        return View(vm);
    }
}
