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

        // Treat "not found" and "not owned" identically — both surface as 404
        // to prevent callers from inferring the existence of tasks they don't own.
        if (task is null || task.UserId != userId)
            return Result<TaskItemResponse>.NotFound("Task not found.");

        return Result<TaskItemResponse>.Ok(task.ToResponse());
    }
}
