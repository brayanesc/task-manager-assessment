using TaskManager.Application.Common;
using TaskManager.Application.Interfaces;

namespace TaskManager.Application.UseCases;

public sealed class DeleteTaskUseCase(IUnitOfWork uow)
{
    public async Task<Result<Unit>> ExecuteAsync(
        Guid taskId,
        Guid userId,
        CancellationToken ct = default)
    {
        var task = await uow.Tasks.GetByIdAsync(taskId, ct);
        if (task is null)
            return Result<Unit>.NotFound($"Task '{taskId}' was not found.");

        if (task.UserId != userId)
            return Result<Unit>.Fail("You do not own this task.");

        await uow.Tasks.DeleteAsync(taskId, ct);
        await uow.CommitAsync(ct);
        return Result<Unit>.Ok(Unit.Value);
    }
}
