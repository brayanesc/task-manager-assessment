using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Extensions;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Application.UseCases;

public sealed class UpdateTaskUseCase(IUnitOfWork uow, IClock clock)
{
    public async Task<Result<TaskItemResponse>> ExecuteAsync(
        Guid taskId,
        TaskItemRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var task = await uow.Tasks.GetByIdAsync(taskId, ct);
        if (task is null)
            return Result<TaskItemResponse>.NotFound($"Task '{taskId}' was not found.");

        if (task.UserId != userId)
            return Result<TaskItemResponse>.Forbidden("You do not own this task.");

        try
        {
            // Pass the task's own DueDate as "today" when the date is unchanged,
            // so the domain's dueDate >= today check is trivially satisfied for
            // tasks that were created with a date that has since passed.
            var today = request.DueDate == task.DueDate ? request.DueDate : clock.Today;
            task.Update(request.Title, request.Description, request.Status, request.DueDate, today);
        }
        catch (DomainException ex)
        {
            return Result<TaskItemResponse>.Fail(ex.Message);
        }

        await uow.Tasks.UpdateAsync(task, ct);
        await uow.CommitAsync(ct);
        return Result<TaskItemResponse>.Ok(task.ToResponse());
    }
}
