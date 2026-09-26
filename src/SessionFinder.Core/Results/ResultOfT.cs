namespace SessionFinder.Core.Results;

/// <summary>
/// The outcome of an operation that either produced a value or failed in a way the user interface
/// renders.
/// </summary>
/// <typeparam name="TValue">What a successful operation produces.</typeparam>
/// <remarks>
/// Reading <see cref="Value"/> on a failure throws, on purpose: a caller that skipped the check has
/// a bug, and returning a default would let that bug travel silently into the user interface.
/// </remarks>
public readonly record struct Result<TValue>
{
    private readonly TValue? _value;

    private Result(TValue? value, AppError? error)
    {
        _value = value;
        Error = error;
    }

    /// <summary>Whether the operation produced a value.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Whether the operation failed instead of producing a value.</summary>
    public bool IsFailure => Error is not null;

    /// <summary>Why the operation failed, or <see langword="null"/> when it did not.</summary>
    public AppError? Error { get; }

    /// <summary>The value produced, for a successful result only.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public TValue Value => Error is null
        ? _value!
        : throw new InvalidOperationException($"A failed result has no value: {Error.Code}.");

    /// <summary>
    /// Builds the outcome of an operation that produced a value.
    /// </summary>
    /// <param name="value">What it produced.</param>
    /// <returns>The successful result.</returns>
    public static Result<TValue> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Result<TValue>(value, error: null);
    }

    /// <summary>
    /// Builds the outcome of an operation that failed.
    /// </summary>
    /// <param name="error">Why it failed.</param>
    /// <returns>The failed result.</returns>
    public static Result<TValue> Failure(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<TValue>(value: default, error);
    }

    /// <summary>
    /// Collapses both outcomes into one value, which is how a caller consumes a result without
    /// asking whether it succeeded first.
    /// </summary>
    /// <typeparam name="TOut">What the caller wants back.</typeparam>
    /// <param name="onSuccess">Produces the value for a success.</param>
    /// <param name="onFailure">Produces the value for a failure.</param>
    /// <returns>Whichever branch applied.</returns>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<AppError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return Error is null ? onSuccess(_value!) : onFailure(Error);
    }
}
