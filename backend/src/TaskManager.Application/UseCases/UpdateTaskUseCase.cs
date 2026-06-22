using TaskManager.Application.DTOs;
using TaskManager.Application.Exceptions;
using TaskManager.Application.Extensions;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Application.UseCases;

public sealed class UpdateTaskUseCase(ITaskRepository taskRepo, IClock clock)
{
    public async Task<TaskItemResponse> ExecuteAsync(
        Guid taskId,
        TaskItemRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var task = await taskRepo.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException($"Task '{taskId}' was not found.");

        if (task.UserId != userId)
            throw new DomainException("You do not own this task.");

        // Only enforce due-date validation when the date is actually changing.
        // A task with a past due date that persisted in the DB is still valid to edit
        // as long as the caller is not trying to set a new past date.
        var today = request.DueDate == task.DueDate ? request.DueDate : clock.Today;

        task.Update(request.Title, request.Description, request.Status, request.DueDate, today);

        await taskRepo.UpdateAsync(task, ct);
        return task.ToResponse();
    }
}
