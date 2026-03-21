using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;
using FluentAssertions;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;
using Xunit;

namespace TaskTracker.UnitTests.Auth;

/// <summary>Tests for domain entity business rules — no mocks needed.</summary>
public sealed class UserEntityTests
{
    [Fact]
    public void Create_ValidInputs_SetsPropertiesCorrectly()
    {
        var user = User.Create("Test@Dev.Local", "hash", "salt", "Test", "User", UserRole.Developer);

        user.Email.Should().Be("test@dev.local");  // normalized to lowercase
        user.FirstName.Should().Be("Test");
        user.IsActive.Should().BeTrue();
        user.FailedLoginCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
    }

    [Fact]
    public void RecordFailedLogin_FiveAttempts_LocksAccount()
    {
        var user = User.Create("dev@dev.local", "hash", "salt", "Dev", "User");

        for (int i = 0; i < 5; i++) user.RecordFailedLogin();

        user.IsLockedOut().Should().BeTrue();
        user.LockoutEnd.Should().NotBeNull();
        user.LockoutEnd!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordFailedLogin_FourAttempts_DoesNotLock()
    {
        var user = User.Create("dev@dev.local", "hash", "salt", "Dev", "User");

        for (int i = 0; i < 4; i++) user.RecordFailedLogin();

        user.IsLockedOut().Should().BeFalse();
        user.FailedLoginCount.Should().Be(4);
    }

    [Fact]
    public void RecordSuccessfulLogin_ResetsFailureCounter()
    {
        var user = User.Create("dev@dev.local", "hash", "salt", "Dev", "User");
        for (int i = 0; i < 3; i++) user.RecordFailedLogin();

        user.RecordSuccessfulLogin();

        user.FailedLoginCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
        user.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePassword_ChangesHashAndClearsLockout()
    {
        var user = User.Create("dev@dev.local", "old-hash", "old-salt", "Dev", "User");
        for (int i = 0; i < 5; i++) user.RecordFailedLogin();
        user.IsLockedOut().Should().BeTrue();

        user.UpdatePassword("new-hash", "new-salt");

        user.PasswordHash.Should().Be("new-hash");
        user.FailedLoginCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
    }

    [Fact]
    public void FullName_ReturnsConcatenatedFirstAndLast()
    {
        var user = User.Create("dev@dev.local", "h", "s", "Arjun", "Mehta");
        user.FullName.Should().Be("Arjun Mehta");
    }
}

public sealed class TaskItemEntityTests
{
    private static TaskItem MakeTask() =>
        TaskItem.Create(Guid.NewGuid(), "Test Task", Guid.NewGuid());

    [Fact]
    public void Create_DefaultStatus_IsPending()
    {
        var task = MakeTask();
        task.Status.Should().Be(TaskStatus.Pending);
    }

    [Fact]
    public void Start_SetsStatusToInProgressAndSetsStartedAt()
    {
        var task = MakeTask();
        task.Start();

        task.Status.Should().Be(TaskStatus.InProgress);
        task.StartedAt.Should().NotBeNull();
        task.StartedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Complete_SetsStatusAndCompletedAt()
    {
        var task = MakeTask();
        task.Start();
        task.Complete();

        task.Status.Should().Be(TaskStatus.Completed);
        task.CompletedAt.Should().NotBeNull();
        task.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Block_SetsStatusToBlocked()
    {
        var task = MakeTask();
        task.Start();
        task.Block();

        task.Status.Should().Be(TaskStatus.Blocked);
    }

    [Fact]
    public void Reopen_ResetsToDefaultState()
    {
        var task = MakeTask();
        task.Complete();
        task.Reopen();

        task.Status.Should().Be(TaskStatus.Pending);
        task.CompletedAt.Should().BeNull();
    }
}

public sealed class ResultPatternTests
{
    [Fact]
    public void Success_IsSuccessTrue_IsFailureFalse()
    {
        var result = TaskTracker.Domain.Common.Result.Success();
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_IsFailureTrue_ContainsError()
    {
        var result = TaskTracker.Domain.Common.Result.Failure("Something went wrong");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Something went wrong");
    }

    [Fact]
    public void SuccessOfT_ContainsValue()
    {
        var result = TaskTracker.Domain.Common.Result.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void FailureOfT_ValueIsDefault()
    {
        var result = TaskTracker.Domain.Common.Result.Failure<string>("error");
        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
