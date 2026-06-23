using System.Data.Common;
using Microsoft.Data.Sqlite;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Enums;

namespace TaskManager.Infrastructure.Persistence;

internal sealed class TaskRepository(SqliteConnection connection, SqliteTransaction transaction)
    : ITaskRepository
{
    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT id, title, description, status, due_date, user_id FROM Tasks WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<PagedResult<TaskItem>> GetPagedByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;

        await using var countCmd = connection.CreateCommand();
        countCmd.Transaction = transaction;
        countCmd.CommandText = "SELECT COUNT(*) FROM Tasks WHERE user_id = @userId";
        countCmd.Parameters.AddWithValue("@userId", userId.ToString());
        var totalCount = (long)(await countCmd.ExecuteScalarAsync(ct))!;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT id, title, description, status, due_date, user_id
            FROM Tasks
            WHERE user_id = @userId
            ORDER BY due_date ASC
            LIMIT @pageSize OFFSET @offset
            """;
        cmd.Parameters.AddWithValue("@userId", userId.ToString());
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
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO Tasks (id, title, description, status, due_date, user_id)
            VALUES (@id, @title, @description, @status, @dueDate, @userId)
            """;
        AddTaskParameters(cmd, task);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            UPDATE Tasks
            SET title = @title, description = @description, status = @status, due_date = @dueDate
            WHERE id = @id
            """;
        AddTaskParameters(cmd, task);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
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
    }

    private static TaskItem Map(DbDataReader r) =>
        TaskItem.Reconstitute(
            Guid.Parse(r.GetString(r.GetOrdinal("id"))),
            r.GetString(r.GetOrdinal("title")),
            r.GetString(r.GetOrdinal("description")),
            Enum.Parse<TaskItemStatus>(r.GetString(r.GetOrdinal("status"))),
            DateOnly.ParseExact(r.GetString(r.GetOrdinal("due_date")), "yyyy-MM-dd"),
            Guid.Parse(r.GetString(r.GetOrdinal("user_id")))
        );
}
