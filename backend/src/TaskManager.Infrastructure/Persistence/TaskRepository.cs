using System.Data.Common;
using Microsoft.Data.Sqlite;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;

namespace TaskManager.Infrastructure.Persistence;

internal sealed class TaskRepository(SqliteConnection connection, Func<SqliteTransaction?> getTransaction)
    : ITaskRepository
{
    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = getTransaction();
        cmd.CommandText = "SELECT id, title, description, status, due_date, user_id, updated_at, priority FROM Tasks WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<PagedResult<TaskItem>> GetPagedByUserAsync(
        Guid userId, int page, int pageSize,
        string? statusFilter = null, string? search = null,
        CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        // Build shared WHERE fragment
        var where = new System.Text.StringBuilder("WHERE user_id = @userId");
        if (!string.IsNullOrWhiteSpace(statusFilter))
            where.Append(" AND status = @status");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(" AND (title LIKE @search OR description LIKE @search)");

        await using var countCmd = connection.CreateCommand();
        countCmd.Transaction = getTransaction();
        countCmd.CommandText = $"SELECT COUNT(*) FROM Tasks {where}";
        countCmd.Parameters.AddWithValue("@userId", userId.ToString());
        if (!string.IsNullOrWhiteSpace(statusFilter))
            countCmd.Parameters.AddWithValue("@status", statusFilter);
        if (!string.IsNullOrWhiteSpace(search))
            countCmd.Parameters.AddWithValue("@search", $"%{search}%");
        var totalCount = (long)(await countCmd.ExecuteScalarAsync(ct))!;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = getTransaction();
        cmd.CommandText = $"""
            SELECT id, title, description, status, due_date, user_id, updated_at, priority
            FROM Tasks
            {where}
            ORDER BY created_at DESC
            LIMIT @pageSize OFFSET @offset
            """;
        cmd.Parameters.AddWithValue("@userId", userId.ToString());
        if (!string.IsNullOrWhiteSpace(statusFilter))
            cmd.Parameters.AddWithValue("@status", statusFilter);
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("@search", $"%{search}%");
        cmd.Parameters.AddWithValue("@pageSize", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

        var items = new List<TaskItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(Map(reader));

        return new PagedResult<TaskItem>(items, page, pageSize, (int)totalCount);
    }

    public async Task CreateAsync(TaskItem task, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = getTransaction();
        cmd.CommandText = """
            INSERT INTO Tasks (id, title, description, status, due_date, user_id, priority, created_at, updated_at)
            VALUES (@id, @title, @description, @status, @dueDate, @userId, @priority, @createdAt, @updatedAt)
            """;
        AddTaskParameters(cmd, task);
        var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        cmd.Parameters.AddWithValue("@createdAt", now);
        cmd.Parameters.AddWithValue("@updatedAt", now);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = getTransaction();
        cmd.CommandText = """
            UPDATE Tasks
            SET title = @title, description = @description, status = @status, due_date = @dueDate,
                priority = @priority, updated_at = @updatedAt
            WHERE id = @id
            """;
        AddTaskParameters(cmd, task);
        cmd.Parameters.AddWithValue("@updatedAt", task.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = getTransaction();
        cmd.CommandText = "DELETE FROM Tasks WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddTaskParameters(SqliteCommand cmd, TaskItem task)
    {
        cmd.Parameters.AddWithValue("@id", task.Id.ToString());
        cmd.Parameters.AddWithValue("@title", task.Title);
        cmd.Parameters.AddWithValue("@description", task.Description);
        cmd.Parameters.AddWithValue("@status", task.Status.ToString());
        cmd.Parameters.AddWithValue("@dueDate", task.DueDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@userId", task.UserId.ToString());
        cmd.Parameters.AddWithValue("@priority", task.Priority.ToString());
    }

    private static TaskItem Map(DbDataReader r)
    {
        var priorityOrdinal = r.GetOrdinal("priority");
        var priority = r.IsDBNull(priorityOrdinal)
            ? TaskPriority.Medium
            : Enum.Parse<TaskPriority>(r.GetString(priorityOrdinal));

        return TaskItem.Reconstitute(
            Guid.Parse(r.GetString(r.GetOrdinal("id"))),
            r.GetString(r.GetOrdinal("title")),
            r.GetString(r.GetOrdinal("description")),
            Enum.Parse<TaskItemStatus>(r.GetString(r.GetOrdinal("status"))),
            DateOnly.ParseExact(r.GetString(r.GetOrdinal("due_date")), "yyyy-MM-dd"),
            Guid.Parse(r.GetString(r.GetOrdinal("user_id"))),
            DateTimeOffset.Parse(r.GetString(r.GetOrdinal("updated_at"))),
            priority
        );
    }
}
