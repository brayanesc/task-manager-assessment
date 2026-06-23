using Moq;
using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;

namespace TaskManager.Application.Tests;

public class UpdateTaskUseCaseTests
{
    private static readonly DateOnly Today = new(2026, 6, 22);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);
    private static readonly DateOnly PastDate = new(2026, 1, 1);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly Mock<IClock> _clock = new();
    private readonly UpdateTaskUseCase _sut;

    public UpdateTaskUseCaseTests()
    {
        _clock.Setup(c => c.Today).Returns(Today);
        _uow.Setup(u => u.Tasks).Returns(_taskRepo.Object);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _taskRepo.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new UpdateTaskUseCase(_uow.Object, _clock.Object);
    }

    private void SetupGetById(Guid taskId, TaskItem? task) =>
        _taskRepo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>())).ReturnsAsync(task);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReturnsOkWithUpdatedTask()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, TaskItem.Reconstitute(taskId, "Original", "Old", TaskItemStatus.Todo, Tomorrow, userId, DateTimeOffset.UtcNow));

        var result = await _sut.ExecuteAsync(
            taskId, new TaskItemRequest("Updated", "New", TaskItemStatus.InProgress, Tomorrow), userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated", result.Value!.Title);
        Assert.Equal(TaskItemStatus.InProgress, result.Value.Status);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AC: unchanged past due date must not fail ──────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithUnchangedPastDueDate_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, TaskItem.Reconstitute(taskId, "Task", null, TaskItemStatus.InProgress, PastDate, userId, DateTimeOffset.UtcNow));

        var result = await _sut.ExecuteAsync(
            taskId, new TaskItemRequest("Updated", null, TaskItemStatus.Done, PastDate), userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PastDate, result.Value!.DueDate);
    }

    [Fact]
    public async Task ExecuteAsync_ChangingToFutureDueDate_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, TaskItem.Reconstitute(taskId, "Task", null, TaskItemStatus.Todo, Tomorrow, userId, DateTimeOffset.UtcNow));

        var newDate = Tomorrow.AddDays(7);
        var result = await _sut.ExecuteAsync(
            taskId, new TaskItemRequest("Task", null, TaskItemStatus.Todo, newDate), userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(newDate, result.Value!.DueDate);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNonExistentTask_ReturnsNotFound()
    {
        SetupGetById(Guid.NewGuid(), null);

        var result = await _sut.ExecuteAsync(
            Guid.NewGuid(), new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow),
            Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.NotFound, result.Kind);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Ownership (AC: rejected when task belongs to another user) ─────────────

    [Fact]
    public async Task ExecuteAsync_WithOtherUsersTask_ReturnsForbidden()
    {
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, TaskItem.Reconstitute(taskId, "Task", null, TaskItemStatus.Todo, Tomorrow, Guid.NewGuid(), DateTimeOffset.UtcNow));

        var result = await _sut.ExecuteAsync(
            taskId, new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow),
            Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Forbidden, result.Kind);
        _taskRepo.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Title validation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyTitle_ReturnsFailResult(string title)
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, TaskItem.Reconstitute(taskId, "Task", null, TaskItemStatus.Todo, Tomorrow, userId, DateTimeOffset.UtcNow));

        var result = await _sut.ExecuteAsync(
            taskId, new TaskItemRequest(title, null, TaskItemStatus.Todo, Tomorrow), userId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Validation, result.Kind);
    }

    // ── Due-date validation (when date is changing) ───────────────────────────

    [Fact]
    public async Task ExecuteAsync_ChangingToPastDueDate_ReturnsFailResult()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, TaskItem.Reconstitute(taskId, "Task", null, TaskItemStatus.Todo, Tomorrow, userId, DateTimeOffset.UtcNow));

        var result = await _sut.ExecuteAsync(
            taskId, new TaskItemRequest("Task", null, TaskItemStatus.Todo, Today.AddDays(-1)), userId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Validation, result.Kind);
    }
}
