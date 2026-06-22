using TaskManager.Application.Exceptions;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Application.UseCases;

public sealed class DeleteTaskUseCase(ITaskRepository taskRepo)
{
    public async Task ExecuteAsync(
        Guid taskId,
        Guid userId,
        CancellationToken ct = default)
    {
        var task = await taskRepo.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException($"Task '{taskId}' was not found.");

        if (task.UserId != userId)
            throw new DomainException("You do not own this task.");

        await taskRepo.DeleteAsync(taskId, ct);
    }
}
