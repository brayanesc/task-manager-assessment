using TaskManager.Domain.Enums;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Domain.Entities;

public sealed class TaskItem
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TaskItemStatus Status { get; private set; }
    public DateOnly DueDate { get; private set; }
    public Guid UserId { get; private set; }

    private TaskItem() { }

    public static TaskItem Create(
        string title,
        string? description,
        DateOnly dueDate,
        Guid userId,
        DateOnly today)
    {
        Validate(title, dueDate, today);
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description ?? string.Empty,
            Status = TaskItemStatus.Todo,
            DueDate = dueDate,
            UserId = userId
        };
    }

    // Used by Infrastructure repositories to map rows — skips business-rule validation
    // because persisted records may have due dates that have since passed.
    public static TaskItem Reconstitute(
        Guid id,
        string title,
        string? description,
        TaskItemStatus status,
        DateOnly dueDate,
        Guid userId) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description ?? string.Empty,
            Status = status,
            DueDate = dueDate,
            UserId = userId
        };

    public void Update(
        string title,
        string? description,
        TaskItemStatus status,
        DateOnly dueDate,
        DateOnly today)
    {
        Validate(title, dueDate, today);
        Title = title;
        Description = description ?? string.Empty;
        Status = status;
        DueDate = dueDate;
    }

    private static void Validate(string title, DateOnly dueDate, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Title must not be empty.");
        if (title.Length > 120)
            throw new DomainException("Title must not exceed 120 characters.");
        if (dueDate < today)
            throw new DomainException("Due date must be today or in the future.");
    }
}
