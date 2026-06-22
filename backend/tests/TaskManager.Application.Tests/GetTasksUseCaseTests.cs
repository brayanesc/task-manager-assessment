using Moq;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;

namespace TaskManager.Application.Tests;

public class GetTasksUseCaseTests
{
    private static readonly DateOnly Tomorrow = new DateOnly(2026, 6, 22).AddDays(1);

    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly GetTasksUseCase _sut;

    public GetTasksUseCaseTests()
    {
        _sut = new GetTasksUseCase(_taskRepo.Object);
    }

    private static TaskItem MakeTask(Guid userId) =>
        TaskItem.Reconstitute(Guid.NewGuid(), "Task", "Desc", TaskItemStatus.Todo, Tomorrow, userId);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithTasks_ReturnsMappedPagedResult()
    {
        var userId = Guid.NewGuid();
        var tasks = new[] { MakeTask(userId), MakeTask(userId) };
        var paged = new PagedResult<TaskItem>(tasks, 1, 10, 2);
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, 1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(paged);

        var result = await _sut.ExecuteAsync(userId, 1, 10, CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoTasks_ReturnsEmptyPagedResult()
    {
        var userId = Guid.NewGuid();
        var paged = new PagedResult<TaskItem>(Array.Empty<TaskItem>(), 1, 10, 0);
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, 1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(paged);

        var result = await _sut.ExecuteAsync(userId, 1, 10, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // ── Pagination boundary (AC: paginated — page 1, pageSize 1) ─────────────

    [Fact]
    public async Task ExecuteAsync_PageOneSizeOne_ReturnsSingleItem()
    {
        var userId = Guid.NewGuid();
        var task = MakeTask(userId);
        var paged = new PagedResult<TaskItem>(new[] { task }, 1, 1, 5);
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, 1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(paged);

        var result = await _sut.ExecuteAsync(userId, 1, 1, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(5, result.TotalCount);
    }

    // ── Scoped to requesting user ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_PassesCorrectUserIdToRepository()
    {
        var userId = Guid.NewGuid();
        var paged = new PagedResult<TaskItem>(Array.Empty<TaskItem>(), 1, 10, 0);
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

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
        var paged = new PagedResult<TaskItem>(new[] { task }, 1, 10, 1);
        _taskRepo.Setup(r => r.GetPagedByUserAsync(userId, 1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(paged);

        var result = await _sut.ExecuteAsync(userId, 1, 10, CancellationToken.None);

        var item = result.Items[0];
        Assert.Equal(taskId, item.Id);
        Assert.Equal("Fix it", item.Title);
        Assert.Equal(TaskItemStatus.InProgress, item.Status);
        Assert.Equal(userId, item.UserId);
    }
}
