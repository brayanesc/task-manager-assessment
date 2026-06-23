using Moq;
using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;

namespace TaskManager.Application.Tests;

public class GetTasksUseCaseTests
{
    private static readonly DateOnly Tomorrow = new DateOnly(2026, 6, 22).AddDays(1);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly GetTasksUseCase _sut;

    public GetTasksUseCaseTests()
    {
        _uow.Setup(u => u.Tasks).Returns(_taskRepo.Object);
        _sut = new GetTasksUseCase(_uow.Object);
    }

    private static TaskItem MakeTask(Guid userId) =>
        TaskItem.Reconstitute(Guid.NewGuid(), "Task", "Desc", TaskItemStatus.Todo, Tomorrow, userId);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithTasks_ReturnsOkPagedResult()
    {
        var userId = Guid.NewGuid();
        var tasks = new[] { MakeTask(userId), MakeTask(userId) };
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TaskItem>(tasks, 1, 10, 2));

        var result = await _sut.ExecuteAsync(userId, 1, 10, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(10, result.Value.PageSize);
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoTasks_ReturnsOkEmptyResult()
    {
        var userId = Guid.NewGuid();
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TaskItem>(Array.Empty<TaskItem>(), 1, 10, 0));

        var result = await _sut.ExecuteAsync(userId, 1, 10, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
    }

    // ── Pagination boundary (AC: paginated — page 1, pageSize 1) ─────────────

    [Fact]
    public async Task ExecuteAsync_PageOneSizeOne_ReturnsSingleItem()
    {
        var userId = Guid.NewGuid();
        var task = MakeTask(userId);
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TaskItem>(new[] { task }, 1, 1, 5));

        var result = await _sut.ExecuteAsync(userId, 1, 1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal(5, result.Value.TotalCount);
    }

    // ── Scoped to requesting user ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_PassesCorrectUserIdToRepository()
    {
        var userId = Guid.NewGuid();
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TaskItem>(Array.Empty<TaskItem>(), 1, 10, 0));

        await _sut.ExecuteAsync(userId, 1, 10, CancellationToken.None);

        _taskRepo.Verify(r => r.GetPagedByUserAsync(userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Response mapping ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MapsTaskItemFieldsToResponse()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Fix it", "Desc", TaskItemStatus.InProgress, Tomorrow, userId);
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TaskItem>(new[] { task }, 1, 10, 1));

        var result = await _sut.ExecuteAsync(userId, 1, 10, CancellationToken.None);

        var item = result.Value!.Items[0];
        Assert.Equal(taskId, item.Id);
        Assert.Equal("Fix it", item.Title);
        Assert.Equal(TaskItemStatus.InProgress, item.Status);
        Assert.Equal(userId, item.UserId);
    }
}
