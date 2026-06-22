using Moq;
using TaskManager.Application.DTOs;
using TaskManager.Application.Exceptions;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Application.Tests;

public class UpdateTaskUseCaseTests
{
    private static readonly DateOnly Today = new(2026, 6, 22);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);
    private static readonly DateOnly PastDate = new(2026, 1, 1);

    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly Mock<IClock> _clock = new();
    private readonly UpdateTaskUseCase _sut;

    public UpdateTaskUseCaseTests()
    {
        _clock.Setup(c => c.Today).Returns(Today);
        _taskRepo
            .Setup(r => r.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new UpdateTaskUseCase(_taskRepo.Object, _clock.Object);
    }

    private void SetupGetById(Guid taskId, TaskItem? task) =>
        _taskRepo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>())).ReturnsAsync(task);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithValidInput_UpdatesAndReturnsResponse()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Original", "Old", TaskItemStatus.Todo, Tomorrow, userId);
        SetupGetById(taskId, task);

        var request = new TaskItemRequest("Updated", "New", TaskItemStatus.InProgress, Tomorrow);
        var result = await _sut.ExecuteAsync(taskId, request, userId, CancellationToken.None);

        Assert.Equal("Updated", result.Title);
        Assert.Equal("New", result.Description);
        Assert.Equal(TaskItemStatus.InProgress, result.Status);
        _taskRepo.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AC: unchanged past due date must not fail validation ──────────────────

    [Fact]
    public async Task ExecuteAsync_WithUnchangedPastDueDate_Succeeds()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        // Existing task has a past due date (was valid when created, clock moved on)
        var task = TaskItem.Reconstitute(taskId, "Old task", null, TaskItemStatus.InProgress, PastDate, userId);
        SetupGetById(taskId, task);

        // Update title/status but keep the same past due date — must not throw
        var request = new TaskItemRequest("Updated", null, TaskItemStatus.Done, PastDate);
        var result = await _sut.ExecuteAsync(taskId, request, userId, CancellationToken.None);

        Assert.Equal("Updated", result.Title);
        Assert.Equal(TaskItemStatus.Done, result.Status);
        Assert.Equal(PastDate, result.DueDate);
    }

    [Fact]
    public async Task ExecuteAsync_ChangingToFutureDueDate_Succeeds()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Task", null, TaskItemStatus.Todo, Tomorrow, userId);
        SetupGetById(taskId, task);

        var newDate = Tomorrow.AddDays(7);
        var request = new TaskItemRequest("Task", null, TaskItemStatus.Todo, newDate);
        var result = await _sut.ExecuteAsync(taskId, request, userId, CancellationToken.None);

        Assert.Equal(newDate, result.DueDate);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNonExistentTask_ThrowsNotFoundException()
    {
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, null);

        var request = new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.ExecuteAsync(taskId, request, Guid.NewGuid(), CancellationToken.None));
    }

    // ── Ownership (AC: rejected when task belongs to another user) ─────────────

    [Fact]
    public async Task ExecuteAsync_WithOtherUsersTask_ThrowsDomainException()
    {
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Their task", null, TaskItemStatus.Todo, Tomorrow, ownerId);
        SetupGetById(taskId, task);

        var request = new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow);
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(taskId, request, requesterId, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_OnOwnershipViolation_NeverCallsUpdateRepository()
    {
        var ownerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Task", null, TaskItemStatus.Todo, Tomorrow, ownerId);
        SetupGetById(taskId, task);

        var request = new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow);
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(taskId, request, Guid.NewGuid(), CancellationToken.None));

        _taskRepo.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Title validation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyTitle_ThrowsDomainException(string title)
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Task", null, TaskItemStatus.Todo, Tomorrow, userId);
        SetupGetById(taskId, task);

        var request = new TaskItemRequest(title, null, TaskItemStatus.Todo, Tomorrow);
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(taskId, request, userId, CancellationToken.None));
    }

    // ── Due-date validation (when date is changing) ───────────────────────────

    [Fact]
    public async Task ExecuteAsync_ChangingToPastDueDate_ThrowsDomainException()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Task", null, TaskItemStatus.Todo, Tomorrow, userId);
        SetupGetById(taskId, task);

        // The new date is different AND in the past — must fail
        var request = new TaskItemRequest("Task", null, TaskItemStatus.Todo, Today.AddDays(-1));
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(taskId, request, userId, CancellationToken.None));
    }
}
