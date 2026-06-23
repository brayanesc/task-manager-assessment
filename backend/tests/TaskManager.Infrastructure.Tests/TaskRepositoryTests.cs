using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Tests.TestFixtures;

namespace TaskManager.Infrastructure.Tests;

public class TaskRepositoryTests : IClassFixture<DatabaseFixture>
{
    private static readonly DateOnly Today = new(2026, 6, 22);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);
    private static readonly DateOnly NextWeek = Today.AddDays(7);

    private readonly string _cs;

    public TaskRepositoryTests(DatabaseFixture fixture)
    {
        _cs = fixture.ConnectionString;
    }

    // Creates a persisted user and returns their ID so tasks can satisfy the FK.
    private async Task<Guid> CreateUserAsync(string? email = null)
    {
        var user = User.Create(email ?? $"u_{Guid.NewGuid():N}@test.com", "hash");
        await using var uow = new UnitOfWork(_cs);
        await uow.Users.CreateAsync(user);
        await uow.CommitAsync();
        return user.Id;
    }

    private static TaskItem NewTask(Guid userId, string title = "Task", DateOnly? dueDate = null) =>
        TaskItem.Create(title, "Description", dueDate ?? Tomorrow, userId, Today);

    // ── Create / GetById ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ThenGetByIdAsync_ReturnsCorrectTask()
    {
        var userId = await CreateUserAsync();
        var task = NewTask(userId, "My task");

        await using var uow = new UnitOfWork(_cs);
        await uow.Tasks.CreateAsync(task);
        await uow.CommitAsync();

        await using var uow2 = new UnitOfWork(_cs);
        var fetched = await uow2.Tasks.GetByIdAsync(task.Id);

        Assert.NotNull(fetched);
        Assert.Equal(task.Id, fetched!.Id);
        Assert.Equal("My task", fetched.Title);
        Assert.Equal("Description", fetched.Description);
        Assert.Equal(TaskItemStatus.Todo, fetched.Status);
        Assert.Equal(Tomorrow, fetched.DueDate);
        Assert.Equal(userId, fetched.UserId);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        await using var uow = new UnitOfWork(_cs);
        var result = await uow.Tasks.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var userId = await CreateUserAsync();
        var task = NewTask(userId, "Original");

        await using var uow = new UnitOfWork(_cs);
        await uow.Tasks.CreateAsync(task);
        await uow.CommitAsync();

        task.Update("Updated title", "New desc", TaskItemStatus.InProgress, NextWeek, Today);

        await using var uow2 = new UnitOfWork(_cs);
        await uow2.Tasks.UpdateAsync(task);
        await uow2.CommitAsync();

        await using var uow3 = new UnitOfWork(_cs);
        var fetched = await uow3.Tasks.GetByIdAsync(task.Id);

        Assert.Equal("Updated title", fetched!.Title);
        Assert.Equal("New desc", fetched.Description);
        Assert.Equal(TaskItemStatus.InProgress, fetched.Status);
        Assert.Equal(NextWeek, fetched.DueDate);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesTask()
    {
        var userId = await CreateUserAsync();
        var task = NewTask(userId);

        await using var uow = new UnitOfWork(_cs);
        await uow.Tasks.CreateAsync(task);
        await uow.CommitAsync();

        await using var uow2 = new UnitOfWork(_cs);
        await uow2.Tasks.DeleteAsync(task.Id);
        await uow2.CommitAsync();

        await using var uow3 = new UnitOfWork(_cs);
        Assert.Null(await uow3.Tasks.GetByIdAsync(task.Id));
    }

    // ── GetPagedByUser (pagination + scoping) ─────────────────────────────────

    [Fact]
    public async Task GetPagedByUserAsync_ReturnsOnlyOwnedTasks()
    {
        var userId = await CreateUserAsync();
        var otherId = await CreateUserAsync();

        await using var uow = new UnitOfWork(_cs);
        await uow.Tasks.CreateAsync(NewTask(userId, "Mine 1"));
        await uow.Tasks.CreateAsync(NewTask(userId, "Mine 2"));
        await uow.Tasks.CreateAsync(NewTask(otherId, "Theirs"));
        await uow.CommitAsync();

        await using var uow2 = new UnitOfWork(_cs);
        var result = await uow2.Tasks.GetPagedByUserAsync(userId, 1, 10);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, t => Assert.Equal(userId, t.UserId));
    }

    [Fact]
    public async Task GetPagedByUserAsync_PageOne_ReturnsFirstPageItems()
    {
        var userId = await CreateUserAsync();

        await using var uow = new UnitOfWork(_cs);
        for (var i = 1; i <= 5; i++)
            await uow.Tasks.CreateAsync(NewTask(userId, $"Page1-Task {i}"));
        await uow.CommitAsync();

        await using var uow2 = new UnitOfWork(_cs);
        var page1 = await uow2.Tasks.GetPagedByUserAsync(userId, 1, 3);

        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(1, page1.Page);
        Assert.Equal(3, page1.PageSize);
    }

    [Fact]
    public async Task GetPagedByUserAsync_PageTwo_ReturnsRemainingItems()
    {
        var userId = await CreateUserAsync();

        await using var uow = new UnitOfWork(_cs);
        for (var i = 1; i <= 5; i++)
            await uow.Tasks.CreateAsync(NewTask(userId, $"Page2-Task {i}"));
        await uow.CommitAsync();

        await using var uow2 = new UnitOfWork(_cs);
        var page2 = await uow2.Tasks.GetPagedByUserAsync(userId, 2, 3);

        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(2, page2.Page);
    }

    [Fact]
    public async Task GetPagedByUserAsync_WithNoTasks_ReturnsEmptyResult()
    {
        await using var uow = new UnitOfWork(_cs);
        var result = await uow.Tasks.GetPagedByUserAsync(Guid.NewGuid(), 1, 10);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
