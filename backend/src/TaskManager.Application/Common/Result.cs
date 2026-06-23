namespace TaskManager.Application.Common;

public enum ResultKind { Ok, Validation, NotFound, Conflict, Unauthorized, Forbidden }

public readonly struct Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public ResultKind Kind { get; }
    public bool IsSuccess => Kind == ResultKind.Ok;

    private Result(T? value, string? error, ResultKind kind)
    {
        Value = value;
        Error = error;
        Kind = kind;
    }

    public static Result<T> Ok(T value) => new(value, null, ResultKind.Ok);
    public static Result<T> Fail(string error) => new(default, error, ResultKind.Validation);
    public static Result<T> NotFound(string error) => new(default, error, ResultKind.NotFound);
    public static Result<T> Conflict(string error) => new(default, error, ResultKind.Conflict);
    public static Result<T> Unauthorized(string error) => new(default, error, ResultKind.Unauthorized);
    public static Result<T> Forbidden(string error) => new(default, error, ResultKind.Forbidden);
}
