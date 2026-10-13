namespace OneClick.Domain.Common;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// Used throughout the domain to avoid throwing exceptions for expected failures.
/// </summary>
/// <typeparam name="T">The type of value returned on success</typeparam>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error ?? throw new ArgumentNullException(nameof(error));
        IsSuccess = false;
    }

    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The value returned by a successful operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a failed result</exception>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access value of failed result");

    /// <summary>
    /// The error information for a failed operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a successful result</exception>
    public Error Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access error of successful result");

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static Result<T> Failure(string message) => new(Error.General(message));

    /// <summary>
    /// Transforms the value if the result is successful.
    /// </summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return IsSuccess
            ? Result<TResult>.Success(mapper(_value!))
            : Result<TResult>.Failure(_error!);
    }

    /// <summary>
    /// Chains another operation if this result is successful.
    /// </summary>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> binder)
    {
        return IsSuccess
            ? binder(_value!)
            : Result<TResult>.Failure(_error!);
    }

    /// <summary>
    /// Executes an action if the result is successful, returns the original result.
    /// </summary>
    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess)
        {
            action(_value!);
        }
        return this;
    }

    /// <summary>
    /// Returns the value if successful, otherwise returns the provided default value.
    /// </summary>
    public T ValueOrDefault(T defaultValue) => IsSuccess ? _value! : defaultValue;

    /// <summary>
    /// Returns the value if successful, otherwise returns the result of the provided function.
    /// </summary>
    public T ValueOrDefault(Func<Error, T> defaultValueProvider) => 
        IsSuccess ? _value! : defaultValueProvider(_error!);
}

/// <summary>
/// Represents the result of an operation that can either succeed or fail (no return value).
/// </summary>
public sealed class Result
{
    private readonly Error? _error;

    private Result()
    {
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _error = error ?? throw new ArgumentNullException(nameof(error));
        IsSuccess = false;
    }

    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The error information for a failed operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a successful result</exception>
    public Error Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access error of successful result");

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new();

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result Failure(Error error) => new(error);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static Result Failure(string message) => new(Error.General(message));

    /// <summary>
    /// Chains another operation if this result is successful.
    /// </summary>
    public Result<T> Bind<T>(Func<Result<T>> binder)
    {
        return IsSuccess
            ? binder()
            : Result<T>.Failure(_error!);
    }

    /// <summary>
    /// Chains another operation if this result is successful.
    /// </summary>
    public Result Bind(Func<Result> binder)
    {
        return IsSuccess
            ? binder()
            : Failure(_error!);
    }

    /// <summary>
    /// Executes an action if the result is successful, returns the original result.
    /// </summary>
    public Result Tap(Action action)
    {
        if (IsSuccess)
        {
            action();
        }
        return this;
    }
}