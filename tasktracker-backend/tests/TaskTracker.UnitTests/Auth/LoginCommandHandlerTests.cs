using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaskTracker.Application.Auth.Commands;
using TaskTracker.Application.Common.Interfaces;
using TaskTracker.Domain.Common;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;
using TaskTracker.Domain.Interfaces;
using Xunit;

namespace TaskTracker.UnitTests.Auth;

/// <summary>
/// Unit tests for LoginCommandHandler.
/// Every external dependency is mocked — no DB, no network, no time dependency.
/// </summary>
public sealed class LoginCommandHandlerTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────
    private readonly Mock<IUserRepository>         _users         = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IPasswordHashService>    _hasher        = new();
    private readonly Mock<IJwtTokenService>        _jwt           = new();
    private readonly Mock<ICurrentUserService>     _currentUser   = new();
    private readonly Mock<IUnitOfWork>             _uow           = new();
    private readonly Mock<ILogger<LoginCommandHandler>> _log      = new();

    private LoginCommandHandler CreateHandler() => new(
        _users.Object, _refreshTokens.Object, _hasher.Object,
        _jwt.Object, _currentUser.Object, _uow.Object, _log.Object);

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static User CreateActiveUser(string email = "test@dev.local") =>
        User.Create(email, "hashed-password", "salt", "Test", "User", UserRole.Developer);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSuccessWithTokens()
    {
        // Arrange
        var user = CreateActiveUser();
        _users.Setup(u => u.GetByEmailAsync("test@dev.local", default))
              .ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyPassword("password123", user.PasswordHash, user.Salt))
               .Returns(true);
        _jwt.Setup(j => j.GenerateAccessToken(user)).Returns("fake-access-token");
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns("fake-refresh-token");
        _currentUser.Setup(c => c.IpAddress).Returns("127.0.0.1");
        _refreshTokens.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default))
                      .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new LoginCommand("test@dev.local", "password123"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("fake-access-token");
        result.Value.Role.Should().Be("Developer");
        result.Value.FullName.Should().Be("Test User");
    }

    [Fact]
    public async Task Handle_UnknownEmail_ReturnsFailureWithGenericMessage()
    {
        // Arrange — user not found
        _users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), default))
              .ReturnsAsync((User?)null);
        _currentUser.Setup(c => c.IpAddress).Returns("127.0.0.1");

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new LoginCommand("nobody@dev.local", "any"), default);

        // Assert — OWASP: same error message regardless of reason
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Invalid credentials.");
        _hasher.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsFailureAndIncrementsCounter()
    {
        // Arrange
        var user = CreateActiveUser();
        _users.Setup(u => u.GetByEmailAsync("test@dev.local", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyPassword("wrong", user.PasswordHash, user.Salt)).Returns(false);
        _currentUser.Setup(c => c.IpAddress).Returns("127.0.0.1");
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new LoginCommand("test@dev.local", "wrong"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Invalid credentials.");
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once); // failed login persisted
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsGenericFailure()
    {
        // Arrange
        var user = CreateActiveUser();
        user.Deactivate();
        _users.Setup(u => u.GetByEmailAsync("test@dev.local", default)).ReturnsAsync(user);
        _currentUser.Setup(c => c.IpAddress).Returns("127.0.0.1");

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new LoginCommand("test@dev.local", "any"), default);

        // Assert — OWASP: inactive shows same error as wrong credentials
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Invalid credentials.");
    }

    [Fact]
    public async Task Handle_LockedAccount_ReturnsLockoutMessage()
    {
        // Arrange — simulate 5 failed attempts
        var user = CreateActiveUser();
        for (int i = 0; i < 5; i++) user.RecordFailedLogin();

        _users.Setup(u => u.GetByEmailAsync("test@dev.local", default)).ReturnsAsync(user);
        _currentUser.Setup(c => c.IpAddress).Returns("127.0.0.1");

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new LoginCommand("test@dev.local", "any"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("locked");
        _hasher.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidLogin_CallsRefreshTokenAddAndSave()
    {
        // Arrange
        var user = CreateActiveUser();
        _users.Setup(u => u.GetByEmailAsync("test@dev.local", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _jwt.Setup(j => j.GenerateAccessToken(user)).Returns("access");
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns("refresh");
        _currentUser.Setup(c => c.IpAddress).Returns("::1");
        _refreshTokens.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = CreateHandler();

        // Act
        await handler.Handle(new LoginCommand("test@dev.local", "pass"), default);

        // Assert — refresh token persisted exactly once
        _refreshTokens.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), default), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
