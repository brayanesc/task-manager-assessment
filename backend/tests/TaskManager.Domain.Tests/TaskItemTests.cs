using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Domain.Tests;

public class TaskItemTests
{
    private static readonly DateOnly Today = new(2026, 6, 22);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly Guid OwnerId = Guid.NewGuid();

    // ── Create: happy path ────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidInput_SetsAllFieldsAndDefaultsTodoStatus()
    {
        var task = TaskItem.Create("Fix login bug", "Desc", Tomorrow, OwnerId, Today);

        Assert.Equal("Fix login bug", task.Title);
        Assert.Equal("Desc", task.Description);
        Assert.Equal(Tomorrow, task.DueDate);
        Assert.Equal(OwnerId, task.UserId);
        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.NotEqual(Guid.Empty, task.Id);
    }

    [Fact]
    public void Create_WithDueDateEqualToToday_Succeeds()
    {
        var task = TaskItem.Create("Task", null, Today, OwnerId, Today);
        Assert.Equal(Today, task.DueDate);
    }

    [Fact]
    public void Create_WithNullDescription_DefaultsToEmpty()
    {
        var task = TaskItem.Create("Task", null, Tomorrow, OwnerId, Today);
        Assert.Equal(string.Empty, task.Description);
    }

    // ── Create: title validation ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceTitle_ThrowsDomainException(string title)
    {
        var ex = Assert.Throws<DomainException>(() =>
            TaskItem.Create(title, null, Tomorrow, OwnerId, Today));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithTitleOf120Chars_Succeeds()
    {
        var title = new string('x', 120);
        var task = TaskItem.Create(title, null, Tomorrow, OwnerId, Today);
        Assert.Equal(title, task.Title);
    }

    [Fact]
    public void Create_WithTitleOf121Chars_ThrowsDomainException()
    {
        var title = new string('x', 121);
        var ex = Assert.Throws<DomainException>(() =>
            TaskItem.Create(title, null, Tomorrow, OwnerId, Today));
        Assert.Contains("120", ex.Message);
    }

    // ── Create: due-date validation ───────────────────────────────────────────

    [Fact]
    public void Create_WithPastDueDate_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            TaskItem.Create("Task", null, Yesterday, OwnerId, Today));
        Assert.Contains("due date", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WithValidInput_UpdatesAllMutableFields()
    {
        var task = TaskItem.Create("Original", "Old desc", Tomorrow, OwnerId, Today);
        task.Update("Updated", "New desc", TaskItemStatus.InProgress, Tomorrow, Today);

        Assert.Equal("Updated", task.Title);
        Assert.Equal("New desc", task.Description);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Equal(Tomorrow, task.DueDate);
    }

    [Fact]
    public void Update_WithDoneStatus_Succeeds()
    {
        var task = TaskItem.Create("Task", null, Tomorrow, OwnerId, Today);
        task.Update("Task", null, TaskItemStatus.Done, Tomorrow, Today);
        Assert.Equal(TaskItemStatus.Done, task.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithEmptyTitle_ThrowsDomainException(string title)
    {
        var task = TaskItem.Create("Task", null, Tomorrow, OwnerId, Today);
        Assert.Throws<DomainException>(() =>
            task.Update(title, null, TaskItemStatus.Todo, Tomorrow, Today));
    }

    [Fact]
    public void Update_WithPastDueDate_ThrowsDomainException()
    {
        var task = TaskItem.Create("Task", null, Tomorrow, OwnerId, Today);
        Assert.Throws<DomainException>(() =>
            task.Update("Task", null, TaskItemStatus.Todo, Yesterday, Today));
    }

    [Fact]
    public void Update_DoesNotChangeUserId()
    {
        var task = TaskItem.Create("Task", null, Tomorrow, OwnerId, Today);
        var originalOwner = task.UserId;
        task.Update("Task", null, TaskItemStatus.Todo, Tomorrow, Today);
        Assert.Equal(originalOwner, task.UserId);
    }

    // ── Reconstitute (ADO.NET mapping — bypasses validation) ─────────────────

    [Fact]
    public void Reconstitute_WithPastDueDate_DoesNotThrow()
    {
        var id = Guid.NewGuid();
        var task = TaskItem.Reconstitute(id, "Old task", "Desc",
            TaskItemStatus.InProgress, Yesterday, OwnerId, DateTimeOffset.UtcNow);

        Assert.Equal(id, task.Id);
        Assert.Equal(Yesterday, task.DueDate);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
    }
}
