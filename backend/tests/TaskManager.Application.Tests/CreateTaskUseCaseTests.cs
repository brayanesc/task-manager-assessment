using Moq;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Application.Tests;

public class CreateTaskUseCaseTests
{
    private static readonly DateOnly Today = new(2026, 6, 22);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly Mock<IClock> _clock = new();
    private readonly CreateTaskUseCase _sut;

    public CreateTaskUseCaseTests()
    {
        _clock.Setup(c => c.Today).Returns(Today);
        _taskRepo
            .Setup(r => r.CreateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new CreateTaskUseCase(_taskRepo.Object, _clock.Object);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReturnsResponse()
    {
        var userId = Guid.NewGuid();
        var request = new TaskItemRequest("Fix login bug", "Desc", TaskItemStatus.Done, Tomorrow);

        var result = await _sut.ExecuteAsync(request, userId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Fix login bug", result.Title);
        Assert.Equal("Desc", result.Description);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(TaskItemStatus.Todo, result.Status); // domain forces Todo on create
        Assert.Equal(Tomorrow, result.DueDate);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WithDueDateToday_Succeeds()
    {
        var request = new TaskItemRequest("Task", null, TaskItemStatus.Todo, Today);
        var result = await _sut.ExecuteAsync(request, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(Today, result.DueDate);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullDescription_DefaultsToEmpty()
    {
        var request = new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow);
        var result = await _sut.ExecuteAsync(request, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(string.Empty, result.Description);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRepositoryCreateOnce()
    {
        var request = new TaskItemRequest("Task", null, TaskItemStatus.Todo, Tomorrow);
        await _sut.ExecuteAsync(request, Guid.NewGuid(), CancellationToken.None);
        _taskRepo.Verify(r => r.CreateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Title validation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyTitle_ThrowsDomainException(string title)
    {
        var request = new TaskItemRequest(title, null, TaskItemStatus.Todo, Tomorrow);
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(request, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithTitleOf121Chars_ThrowsDomainException()
    {
        var request = new TaskItemRequest(new string('x', 121), null, TaskItemStatus.Todo, Tomorrow);
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(request, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithTitleOf120Chars_Succeeds()
    {
        var request = new TaskItemRequest(new string('x', 120), null, TaskItemStatus.Todo, Tomorrow);
        var result = await _sut.ExecuteAsync(request, Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    // ── Due-date validation ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithPastDueDate_ThrowsDomainException()
    {
        var request = new TaskItemRequest("Task", null, TaskItemStatus.Todo, Today.AddDays(-1));
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(request, Guid.NewGuid(), CancellationToken.None));
    }

    // ── Repository not called on validation failure ───────────────────────────

    [Fact]
    public async Task ExecuteAsync_OnValidationFailure_NeverCallsRepository()
    {
        var request = new TaskItemRequest("", null, TaskItemStatus.Todo, Tomorrow);
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(request, Guid.NewGuid(), CancellationToken.None));
        _taskRepo.Verify(r => r.CreateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
