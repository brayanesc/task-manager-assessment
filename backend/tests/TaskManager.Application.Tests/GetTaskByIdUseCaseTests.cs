using Moq;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;

namespace TaskManager.Application.Tests;

public class GetTaskByIdUseCaseTests
{
    private static readonly DateOnly Tomorrow = new DateOnly(2026, 6, 22).AddDays(1);

    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly GetTaskByIdUseCase _sut;

    public GetTaskByIdUseCaseTests()
    {
        _sut = new GetTaskByIdUseCase(_taskRepo.Object);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithOwnedTask_ReturnsMappedResponse()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "My task", "Desc", TaskItemStatus.Todo, Tomorrow, userId);
        _taskRepo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await _sut.ExecuteAsync(taskId, userId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(taskId, result!.Id);
        Assert.Equal("My task", result.Title);
        Assert.Equal(userId, result.UserId);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNonExistentTask_ReturnsNull()
    {
        var taskId = Guid.NewGuid();
        _taskRepo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>())).ReturnsAsync((TaskItem?)null);

        var result = await _sut.ExecuteAsync(taskId, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    // ── AC: task belonging to another user returns null (404) ─────────────────

    [Fact]
    public async Task ExecuteAsync_WithOtherUsersTask_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid(); // different user
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Their task", null, TaskItemStatus.Todo, Tomorrow, ownerId);
        _taskRepo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await _sut.ExecuteAsync(taskId, requesterId, CancellationToken.None);

        Assert.Null(result);
    }
}
