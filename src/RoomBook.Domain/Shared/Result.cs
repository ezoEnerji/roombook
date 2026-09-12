namespace RoomBook.Domain.Shared;

/// <summary>
/// The outcome of an operation that can be rejected by a rule. Rule violations are values here,
/// never exceptions (ADR-0003); exceptions are reserved for programmer errors, which is exactly
/// what reading <see cref="Value"/> off a failed result is.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({_error!.Code}); read Error instead.");

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Result is a success; there is no error to read.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);

    public static Result<T> Failure(string code, string message) => new(new Error(code, message));
}
