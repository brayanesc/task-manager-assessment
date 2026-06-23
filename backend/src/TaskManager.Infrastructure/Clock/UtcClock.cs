using TaskManager.Application.Interfaces;

namespace TaskManager.Infrastructure.Clock;

public sealed class UtcClock : IClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
