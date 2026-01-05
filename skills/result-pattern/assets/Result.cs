namespace YourNamespace.Domain.Common;

/// <summary>
/// Represents the result of an operation that returns a value.
/// Use instead of throwing exceptions for expected failures.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    public bool IsSuccess => _error is null;
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on failure: {_error}");

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on success");

    private Result(T value)
    {
        _value = value;
        _error = null;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}

/// <summary>
/// Represents the result of an operation that returns no value.
/// </summary>
public readonly struct Result
{
    private readonly Error? _error;

    public bool IsSuccess => _error is null;
    public bool IsFailure => !IsSuccess;

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on success");

    private Result(Error error) => _error = error;

    public static Result Success() => new();
    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);

    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess() : onFailure(_error!);
}

/// <summary>
/// Represents an error with code, message, and type.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string message) =>
        new("VALIDATION_ERROR", message, ErrorType.Validation);

    public static Error Validation(string field, string message) =>
        new("VALIDATION_ERROR", $"{field}: {message}", ErrorType.Validation);

    public static Error NotFound(string resource, object id) =>
        new("NOT_FOUND", $"{resource} with ID '{id}' was not found", ErrorType.NotFound);

    public static Error Unauthorized(string? message = null) =>
        new("UNAUTHORIZED", message ?? "Authentication required", ErrorType.Unauthorized);

    public static Error Forbidden(string? message = null) =>
        new("FORBIDDEN", message ?? "Access denied", ErrorType.Forbidden);

    public static Error Conflict(string message) =>
        new("CONFLICT", message, ErrorType.Conflict);

    public static Error Internal(string message) =>
        new("INTERNAL_ERROR", message, ErrorType.Internal);
}

public enum ErrorType
{
    Validation,
    NotFound,
    Unauthorized,
    Forbidden,
    Conflict,
    Internal
}

/// <summary>
/// Extension methods for Result types.
/// </summary>
public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> mapper) =>
        result.IsSuccess
            ? Result<TOut>.Success(mapper(result.Value))
            : Result<TOut>.Failure(result.Error);

    public static Result<TOut> Bind<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Result<TOut>> binder) =>
        result.IsSuccess
            ? binder(result.Value)
            : Result<TOut>.Failure(result.Error);

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<TOut>> mapper) =>
        result.IsSuccess
            ? Result<TOut>.Success(await mapper(result.Value).ConfigureAwait(false))
            : Result<TOut>.Failure(result.Error);

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<Result<TOut>>> binder) =>
        result.IsSuccess
            ? await binder(result.Value).ConfigureAwait(false)
            : Result<TOut>.Failure(result.Error);

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, TOut> mapper)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Map(mapper);
    }

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Result<TOut>> binder)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Bind(binder);
    }

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TOut>>> binder)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.IsSuccess
            ? await binder(result.Value).ConfigureAwait(false)
            : Result<TOut>.Failure(result.Error);
    }
}
