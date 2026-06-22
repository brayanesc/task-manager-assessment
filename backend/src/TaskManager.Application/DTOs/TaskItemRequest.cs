using TaskManager.Domain.Enums;

namespace TaskManager.Application.DTOs;

public sealed record TaskItemRequest(
    string Title,
    string? Description,
    TaskItemStatus Status,
    DateOnly DueDate);
