using FluentAssertions;
using Moq;
using TaskTracker.Application.Common.Interfaces;
using TaskTracker.Application.Dashboard.Queries;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;
using TaskTracker.Domain.Interfaces;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;
using Xunit;

namespace TaskTracker.UnitTests.Dashboard;

public sealed class GetStatusBreakdownQueryHandlerTests
{
    private readonly Mock<ITaskRepository> _tasks = new();
    private readonly Mock<ICacheService>   _cache = new();

    private GetStatusBreakdownQueryHandler CreateHandler() =>
        new(_tasks.Object, _cache.Object);

    [Fact]
    public async Task Handle_NoCache_QueriesRepositoryAndCachesResult()
    {
        // Arrange
        _cache.Setup(c => c.GetAsync<object>(It.IsAny<string>(), default))
              .ReturnsAsync((object?)null);

        var breakdown = new Dictionary<TaskStatus, int>
        {
            { TaskStatus.Pending,    10 },
            { TaskStatus.InProgress,  5 },
            { TaskStatus.Completed,  20 },
            { TaskStatus.Blocked,     2 },
        };
        _tasks.Setup(t => t.GetStatusBreakdownAsync(default)).ReturnsAsync(breakdown);
        _cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), default))
              .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetStatusBreakdownQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Pending.Should().Be(10);
        result.Value.InProgress.Should().Be(5);
        result.Value.Completed.Should().Be(20);
        result.Value.Blocked.Should().Be(2);

        _tasks.Verify(t => t.GetStatusBreakdownAsync(default), Times.Once);
        _cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), default), Times.Once);
    }
}

public sealed class GetVelocityQueryHandlerTests
{
    private readonly Mock<ITimeLogRepository> _timeLogs = new();
    private readonly Mock<ICacheService>      _cache    = new();

    private GetVelocityQueryHandler CreateHandler() =>
        new(_timeLogs.Object, _cache.Object);

    [Fact]
    public async Task Handle_SevenDays_ReturnsSevenDataPoints()
    {
        // Arrange
        _cache.Setup(c => c.GetAsync<object>(It.IsAny<string>(), default))
              .ReturnsAsync((object?)null);

        var velocityData = Enumerable.Range(0, 7)
            .Select(i => (Date: DateTime.UtcNow.Date.AddDays(-6 + i), TasksCompleted: i + 3, HoursLogged: (i + 3) * 0.8))
            .ToList<(DateTime Date, int TasksCompleted, double HoursLogged)>();

        _timeLogs.Setup(t => t.GetVelocityAsync(7, default)).ReturnsAsync(velocityData);
        _cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), default))
              .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetVelocityQuery(7), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(7);
        result.Value[0].TasksCompleted.Should().Be(3);
        result.Value[6].TasksCompleted.Should().Be(9);
    }

    [Fact]
    public async Task Handle_DaysExceedsMax_ClampsToNinety()
    {
        // Arrange
        _cache.Setup(c => c.GetAsync<object>(It.IsAny<string>(), default))
              .ReturnsAsync((object?)null);

        var velocityData = new List<(DateTime, int, double)>();
        _timeLogs.Setup(t => t.GetVelocityAsync(90, default)).ReturnsAsync(velocityData);
        _cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), default))
              .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        // Act — request 999 days; should be clamped to 90
        await handler.Handle(new GetVelocityQuery(999), default);

        // Assert — repository called with clamped value
        _timeLogs.Verify(t => t.GetVelocityAsync(90, default), Times.Once);
    }
}

public sealed class GetDeveloperTodayQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITaskRepository> _tasks = new();

    private GetDeveloperTodayQueryHandler CreateHandler() =>
        new(_users.Object, _tasks.Object);

    [Fact]
    public async Task Handle_UnknownDeveloper_ReturnsFailure()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _users.Setup(u => u.GetByIdAsync(unknownId, default)).ReturnsAsync((User?)null);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetDeveloperTodayQuery(unknownId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _tasks.Verify(t => t.GetByAssigneeTodayAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_KnownDeveloper_ReturnsTimelineWithEntries()
    {
        // Arrange
        var devId = Guid.NewGuid();
        var user  = User.Create("dev@dev.local", "h", "s", "Arjun", "Mehta", UserRole.Developer);

        var tasks = new List<TaskItem>
        {
            CreateCompletedTask("Implement login", devId),
            CreateInProgressTask("Write tests", devId),
        };

        _users.Setup(u => u.GetByIdAsync(devId, default)).ReturnsAsync(user);
        _tasks.Setup(t => t.GetByAssigneeTodayAsync(devId, default)).ReturnsAsync(tasks);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetDeveloperTodayQuery(devId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(2);
        result.Value.DeveloperName.Should().Be("Arjun Mehta");
        result.Value.Entries.Should().Contain(e => e.Status == "Completed");
        result.Value.Entries.Should().Contain(e => e.Status == "InProgress");
    }

    private static TaskItem CreateCompletedTask(string title, Guid assignedTo)
    {
        var t = TaskItem.Create(Guid.NewGuid(), title, assignedTo);
        t.Start(); t.Complete();
        return t;
    }

    private static TaskItem CreateInProgressTask(string title, Guid assignedTo)
    {
        var t = TaskItem.Create(Guid.NewGuid(), title, assignedTo);
        t.Start();
        return t;
    }
}
