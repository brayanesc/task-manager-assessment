using TaskManager.Application.DTOs;
using TaskManager.Application.Extensions;
using TaskManager.Application.Interfaces;

namespace TaskManager.Application.UseCases;

public sealed class GetTasksUseCase(ITaskRepository taskRepo)
{
    public async Task<PagedResult<TaskItemResponse>> ExecuteAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Coerce out-of-range values instead of failing — the API layer validates input format.
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var paged = await taskRepo.GetPagedByUserAsync(userId, page, pageSize, ct);
        var items = paged.Items.Select(t => t.ToResponse()).ToList();
        return new PagedResult<TaskItemResponse>(items, paged.Page, paged.PageSize, paged.TotalCount);
    }
}
