using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Extensions;
using TaskManager.Application.Interfaces;

namespace TaskManager.Application.UseCases;

public sealed class GetTaskByIdUseCase(IUnitOfWork uow)
{
    public async Task<Result<TaskItemResponse>> ExecuteAsync(
        Guid taskId,
        Guid userId,
        CancellationToken ct = default)
    {
        var task = await uow.Tasks.GetByIdAsync(taskId, ct);

        if (task is null)
            return Result<TaskItemResponse>.NotFound("Task not found.");

        if (task.UserId != userId)
            return Result<TaskItemResponse>.Forbidden("You do not own this task.");

        return Result<TaskItemResponse>.Ok(task.ToResponse());
    }
}
