using Moq;
using TaskManager.Application.Common;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;

namespace TaskManager.Application.Tests;

public class DeleteTaskUseCaseTests
{
    private static readonly DateOnly Tomorrow = new DateOnly(2026, 6, 22).AddDays(1);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly DeleteTaskUseCase _sut;

    public DeleteTaskUseCaseTests()
    {
        _uow.Setup(u => u.Tasks).Returns(_taskRepo.Object);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _taskRepo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new DeleteTaskUseCase(_uow.Object);
    }

    private void SetupGetById(Guid taskId, TaskItem? task) =>
        _taskRepo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>())).ReturnsAsync(task);

    // ── Happy path (AC: 204 No Content, task no longer exists) ───────────────

    [Fact]
    public async Task ExecuteAsync_WithOwnedTask_ReturnsOkAndCallsDelete()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, TaskItem.Reconstitute(taskId, "My task", null, TaskItemStatus.Todo, Tomorrow, userId));

        var result = await _sut.ExecuteAsync(taskId, userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _taskRepo.Verify(r => r.DeleteAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AC: non-existent task returns 404 ────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNonExistentTask_ReturnsNotFound()
    {
        SetupGetById(Guid.NewGuid(), null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.NotFound, result.Kind);
        _taskRepo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Ownership violation ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithOtherUsersTask_ReturnsForbidden()
    {
        var taskId = Guid.NewGuid();
        SetupGetById(taskId, TaskItem.Reconstitute(taskId, "Their task", null, TaskItemStatus.Todo, Tomorrow, Guid.NewGuid()));

        var result = await _sut.ExecuteAsync(taskId, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Forbidden, result.Kind);
        _taskRepo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
