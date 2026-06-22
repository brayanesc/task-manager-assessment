using TaskManager.Application.DTOs;
using TaskManager.Application.Extensions;
using TaskManager.Application.Interfaces;

namespace TaskManager.Application.UseCases;

public sealed class GetTaskByIdUseCase(ITaskRepository taskRepo)
{
    public async Task<TaskItemResponse?> ExecuteAsync(
        Guid taskId,
        Guid userId,
        CancellationToken ct = default)
    {
        var task = await taskRepo.GetByIdAsync(taskId, ct);

        // Treat "not found" and "not owned" identically: both surface as 404.
        // This prevents callers from inferring the existence of tasks they don't own.
        if (task is null || task.UserId != userId) return null;

        return task.ToResponse();
    }
}
