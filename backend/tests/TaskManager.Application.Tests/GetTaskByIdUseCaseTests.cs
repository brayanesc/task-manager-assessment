using Moq;
using TaskManager.Application.Common;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;

namespace TaskManager.Application.Tests;

public class GetTaskByIdUseCaseTests
{
    private static readonly DateOnly Tomorrow = new DateOnly(2026, 6, 22).AddDays(1);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly GetTaskByIdUseCase _sut;

    public GetTaskByIdUseCaseTests()
    {
        _uow.Setup(u => u.Tasks).Returns(_taskRepo.Object);
        _sut = new GetTaskByIdUseCase(_uow.Object);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithOwnedTask_ReturnsOkWithMappedResponse()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "My task", "Desc", TaskItemStatus.Todo, Tomorrow, userId, DateTimeOffset.UtcNow);
        _taskRepo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await _sut.ExecuteAsync(taskId, userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(taskId, result.Value!.Id);
        Assert.Equal("My task", result.Value.Title);
        Assert.Equal(userId, result.Value.UserId);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNonExistentTask_ReturnsNotFound()
    {
        _taskRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.NotFound, result.Kind);
    }

    // ── AC: task belonging to another user returns Forbidden ─────────────────

    [Fact]
    public async Task ExecuteAsync_WithOtherUsersTask_ReturnsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Their task", null, TaskItemStatus.Todo, Tomorrow, ownerId, DateTimeOffset.UtcNow);
        _taskRepo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await _sut.ExecuteAsync(taskId, requesterId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Forbidden, result.Kind);
    }
}
