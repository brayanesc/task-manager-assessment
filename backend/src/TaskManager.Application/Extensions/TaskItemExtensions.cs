using TaskManager.Application.DTOs;
using TaskManager.Domain.Entities;

namespace TaskManager.Application.Extensions;

internal static class TaskItemExtensions
{
    internal static TaskItemResponse ToResponse(this TaskItem task) =>
        new(task.Id, task.Title, task.Description, task.Status, task.DueDate, task.UserId);
}
