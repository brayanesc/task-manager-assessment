namespace TaskManager.Application.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    ITaskRepository Tasks { get; }
    IUserRepository Users { get; }
    Task CommitAsync(CancellationToken ct = default);
}
