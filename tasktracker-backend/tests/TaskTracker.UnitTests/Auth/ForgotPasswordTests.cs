using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaskTracker.Application.Auth.Commands;
using TaskTracker.Application.Common.Interfaces;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;
using TaskTracker.Domain.Interfaces;
using Xunit;

namespace TaskTracker.UnitTests.Auth;

public sealed class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository>               _users       = new();
    private readonly Mock<IPasswordResetTokenRepository> _resetTokens = new();
    private readonly Mock<IEmailService>                 _email       = new();
    private readonly Mock<ICurrentUserService>           _currentUser = new();
    private readonly Mock<IUnitOfWork>                   _uow         = new();
    private readonly Mock<ILogger<ForgotPasswordCommandHandler>> _log = new();

    private ForgotPasswordCommandHandler CreateHandler() => new(
        _users.Object, _resetTokens.Object, _email.Object,
        _currentUser.Object, _uow.Object, _log.Object);

    [Fact]
    public async Task Handle_UnknownEmail_ReturnsSuccessWithoutSendingEmail()
    {
        // Arrange — email not registered
        _users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), default))
              .ReturnsAsync((User?)null);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ForgotPasswordCommand("ghost@dev.local"), default);

        // Assert — OWASP: still returns success (no enumeration)
        result.IsSuccess.Should().BeTrue();
        _email.Verify(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_KnownEmail_CreatesTokenAndSendsEmail()
    {
        // Arrange
        var user = User.Create("dev@dev.local", "hash", "salt", "Dev", "User", UserRole.Developer);
        _users.Setup(u => u.GetByEmailAsync("dev@dev.local", default)).ReturnsAsync(user);
        _currentUser.Setup(c => c.IpAddress).Returns("127.0.0.1");
        _resetTokens.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        _email.Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), default))
              .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ForgotPasswordCommand("dev@dev.local"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _resetTokens.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), default), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
        _email.Verify(e => e.SendPasswordResetEmailAsync("dev@dev.local", It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsSuccessWithoutSendingEmail()
    {
        // Arrange
        var user = User.Create("dev@dev.local", "hash", "salt", "Dev", "User", UserRole.Developer);
        user.Deactivate();
        _users.Setup(u => u.GetByEmailAsync("dev@dev.local", default)).ReturnsAsync(user);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ForgotPasswordCommand("dev@dev.local"), default);

        // Assert — inactive user treated same as unknown
        result.IsSuccess.Should().BeTrue();
        _email.Verify(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }
}

public sealed class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IPasswordResetTokenRepository> _resetTokens   = new();
    private readonly Mock<IRefreshTokenRepository>       _refreshTokens = new();
    private readonly Mock<IUserRepository>               _users         = new();
    private readonly Mock<IPasswordHashService>          _hasher        = new();
    private readonly Mock<IUnitOfWork>                   _uow           = new();
    private readonly Mock<ILogger<ResetPasswordCommandHandler>> _log    = new();

    private ResetPasswordCommandHandler CreateHandler() => new(
        _resetTokens.Object, _refreshTokens.Object, _users.Object,
        _hasher.Object, _uow.Object, _log.Object);

    [Fact]
    public async Task Handle_ValidToken_UpdatesPasswordAndRevokesRefreshTokens()
    {
        // Arrange
        var user       = User.Create("dev@dev.local", "old-hash", "old-salt", "Dev", "User", UserRole.Developer);
        var rawToken   = "valid-raw-token";
        var hash       = Sha256(rawToken);
        var resetToken = PasswordResetToken.Create(user.Id, hash);

        _resetTokens.Setup(r => r.GetByHashAsync(hash, default)).ReturnsAsync(resetToken);
        _users.Setup(u => u.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _hasher.Setup(h => h.HashPassword("NewPass@1234", out It.Ref<string>.IsAny)).Returns("new-hash");
        _refreshTokens.Setup(r => r.RevokeAllForUserAsync(user.Id, default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new ResetPasswordCommand(rawToken, "NewPass@1234", "NewPass@1234"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        resetToken.IsUsed.Should().BeTrue();
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(user.Id, default), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_ExpiredOrUsedToken_ReturnsFailure()
    {
        // Arrange — token not found
        _resetTokens.Setup(r => r.GetByHashAsync(It.IsAny<string>(), default))
                    .ReturnsAsync((PasswordResetToken?)null);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new ResetPasswordCommand("bad-token", "Pass@123", "Pass@123"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("invalid");
        _hasher.Verify(h => h.HashPassword(It.IsAny<string>(), out It.Ref<string>.IsAny), Times.Never);
    }

    private static string Sha256(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input)));
    }
}
