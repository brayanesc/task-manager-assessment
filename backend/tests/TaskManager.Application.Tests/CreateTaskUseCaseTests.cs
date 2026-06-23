using Moq;
using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;

namespace TaskManager.Application.Tests;

public class CreateTaskUseCaseTests
{
    private static readonly DateOnly Today = new(2026, 6, 22);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly Mock<IClock> _clock = new();
    private readonly CreateTaskUseCase _sut;

    public CreateTaskUseCaseTests()
    {
        _clock.Setup(c => c.Today).Returns(Today);
        _uow.Setup(u => u.Tasks).Returns(_taskRepo.Object);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _taskRepo.Setup(r => r.CreateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new CreateTaskUseCase(_uow.Object, _clock.Object);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReturnsOkResult()
    {
        var userId = Guid.NewGuid();
        var request = new TaskItemRequest("Fix login bug", "Desc", TaskItemStatus.Done, Tomorrow);

        var result = await _sut.ExecuteAsync(request, userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Fix login bug", result.Value!.Title);
        Assert.Equal("Desc", result.Value.Description);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal(TaskItemStatus.Done, result.Value.Status); // status from request is honoured
        Assert.Equal(Tomorrow, result.Value.DueDate);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WithDueDateToday_ReturnsOk()
    {
        var result = await _sut.ExecuteAsync(
            new TaskItemRequest("Task", null, TaskItemStatus.Todo, Today), Guid.NewGuid(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(Today, result.Value!.DueDate);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullDescription_DefaultsToEmpty()
    {
        var result = await _sut.ExecuteAsync(
            new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow), Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(string.Empty, result.Value!.Description);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_CommitsTransaction()
    {
        await _sut.ExecuteAsync(
            new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow), Guid.NewGuid(), CancellationToken.None);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_CallsRepositoryCreate()
    {
        await _sut.ExecuteAsync(
            new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow), Guid.NewGuid(), CancellationToken.None);
        _taskRepo.Verify(r => r.CreateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Title validation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyTitle_ReturnsFailResult(string title)
    {
        var result = await _sut.ExecuteAsync(
            new TaskItemRequest(title, null, TaskItemStatus.Todo, Tomorrow), Guid.NewGuid(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ResultKind.Validation, result.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_WithTitleOf121Chars_ReturnsFailResult()
    {
        var result = await _sut.ExecuteAsync(
            new TaskItemRequest(new string('x', 121), null, TaskItemStatus.Todo, Tomorrow),
            Guid.NewGuid(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Contains("120", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithTitleOf120Chars_ReturnsOk()
    {
        var result = await _sut.ExecuteAsync(
            new TaskItemRequest(new string('x', 120), null, TaskItemStatus.Todo, Tomorrow),
            Guid.NewGuid(), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    // ── Due-date validation ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithPastDueDate_ReturnsFailResult()
    {
        var result = await _sut.ExecuteAsync(
            new TaskItemRequest("Task", null, TaskItemStatus.Todo, Today.AddDays(-1)),
            Guid.NewGuid(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Contains("due date", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Repository not called on validation failure ───────────────────────────

    [Fact]
    public async Task ExecuteAsync_OnValidationFailure_NeverCallsRepositoryOrCommit()
    {
        await _sut.ExecuteAsync(
            new TaskItemRequest("", null, TaskItemStatus.Todo, Tomorrow), Guid.NewGuid(), CancellationToken.None);
        _taskRepo.Verify(r => r.CreateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
