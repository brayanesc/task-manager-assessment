using Moq;
using TaskManager.Application.Exceptions;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Application.Tests;

public class DeleteTaskUseCaseTests
{
    private static readonly DateOnly Tomorrow = new DateOnly(2026, 6, 22).AddDays(1);

    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly DeleteTaskUseCase _sut;

    public DeleteTaskUseCaseTests()
    {
        _taskRepo
            .Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new DeleteTaskUseCase(_taskRepo.Object);
    }

    private void SetupGetById(Guid taskId, TaskItem? task) =>
        _taskRepo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>())).ReturnsAsync(task);

    // ── Happy path (AC: 204 No Content, task no longer exists) ───────────────

    [Fact]
    public async Task ExecuteAsync_WithOwnedTask_CallsDeleteRepository()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "My task", null, TaskItemStatus.Todo, Tomorrow, userId);
        SetupGetById(taskId, task);

        await _sut.ExecuteAsync(taskId, userId, CancellationToken.None);

        _taskRepo.Verify(r => r.DeleteAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AC: non-existent task returns 404 ────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNonExistentTask_ThrowsNotFoundException()
    {
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.ExecuteAsync(taskId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentTask_NeverCallsDeleteRepository()
    {
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.ExecuteAsync(taskId, Guid.NewGuid(), CancellationToken.None));

        _taskRepo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Ownership violation ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithOtherUsersTask_ThrowsDomainException()
    {
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Their task", null, TaskItemStatus.Todo, Tomorrow, ownerId);
        SetupGetById(taskId, task);

        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(taskId, requesterId, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_OnOwnershipViolation_NeverCallsDeleteRepository()
    {
        var ownerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = TaskItem.Reconstitute(taskId, "Their task", null, TaskItemStatus.Todo, Tomorrow, ownerId);
        SetupGetById(taskId, task);

        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(taskId, Guid.NewGuid(), CancellationToken.None));

        _taskRepo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
