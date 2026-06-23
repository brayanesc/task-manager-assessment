using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Extensions;
using TaskManager.Application.Interfaces;

namespace TaskManager.Application.UseCases;

public sealed class GetTasksUseCase(IUnitOfWork uow)
{
    public async Task<Result<PagedResult<TaskItemResponse>>> ExecuteAsync(
        Guid userId,
        int page,
        int pageSize,
        string? statusFilter = null,
        string? search = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var paged = await uow.Tasks.GetPagedByUserAsync(
            userId, page, pageSize, statusFilter, search, ct);
        var items = paged.Items.Select(t => t.ToResponse()).ToList();
        return Result<PagedResult<TaskItemResponse>>.Ok(
            new PagedResult<TaskItemResponse>(items, paged.Page, paged.PageSize, paged.TotalCount));
    }
}
