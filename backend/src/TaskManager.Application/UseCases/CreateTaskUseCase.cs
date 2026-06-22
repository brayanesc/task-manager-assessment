using TaskManager.Application.DTOs;
using TaskManager.Application.Extensions;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Entities;

namespace TaskManager.Application.UseCases;

public sealed class CreateTaskUseCase(ITaskRepository taskRepo, IClock clock)
{
    public async Task<TaskItemResponse> ExecuteAsync(
        TaskItemRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        // TaskItem.Create enforces title and due-date domain rules; throws DomainException on violation.
        var task = TaskItem.Create(request.Title, request.Description, request.DueDate, userId, clock.Today);
        await taskRepo.CreateAsync(task, ct);
        return task.ToResponse();
    }
}
