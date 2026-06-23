using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Extensions;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Application.UseCases;

public sealed class CreateTaskUseCase(IUnitOfWork uow, IClock clock)
{
    public async Task<Result<TaskItemResponse>> ExecuteAsync(
        TaskItemRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        try
        {
            var task = TaskItem.Create(request.Title, request.Description, request.DueDate, userId, clock.Today, request.Status, request.Priority);
            await uow.Tasks.CreateAsync(task, ct);
            await uow.CommitAsync(ct);
            return Result<TaskItemResponse>.Ok(task.ToResponse());
        }
        catch (DomainException ex)
        {
            return Result<TaskItemResponse>.Fail(ex.Message);
        }
    }
}
