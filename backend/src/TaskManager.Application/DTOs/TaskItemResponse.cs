using TaskManager.Domain.Enums;

namespace TaskManager.Application.DTOs;

public sealed record TaskItemResponse(
    Guid Id,
    string Title,
    string Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly DueDate,
    Guid UserId,
    DateTimeOffset UpdatedAt);
