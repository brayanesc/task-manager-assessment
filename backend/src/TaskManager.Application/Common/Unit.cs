namespace TaskManager.Application.Common;

/// <summary>
/// Represents the absence of a meaningful return value (void equivalent for Result&lt;T&gt;).
/// </summary>
public readonly struct Unit
{
    public static readonly Unit Value = default;
}
