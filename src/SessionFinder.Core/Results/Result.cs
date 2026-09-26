namespace SessionFinder.Core.Results;

/// <summary>
/// The outcome of an operation that either worked or failed in a way the user interface renders.
/// </summary>
/// <remarks>
/// A struct rather than a class because these are created on every keystroke-driven action and
/// never outlive the call that returns them. The failure is the presence of an
/// <see cref="AppError"/>, which is what makes a <see langword="default"/> value a success rather
/// than a failure carrying no explanation.
/// </remarks>
public readonly record struct Result
{
    private Result(AppError? error) => Error = error;

    /// <summary>Whether the operation did what was asked.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Whether the operation did not do what was asked.</summary>
    public bool IsFailure => Error is not null;

    /// <summary>Why the operation failed, or <see langword="null"/> when it did not.</summary>
    public AppError? Error { get; }

    /// <summary>
    /// Builds the outcome of an operation that worked.
    /// </summary>
    /// <returns>The successful result.</returns>
    public static Result Success() => new(error: null);

    /// <summary>
    /// Builds the outcome of an operation that failed.
    /// </summary>
    /// <param name="error">Why it failed.</param>
    /// <returns>The failed result.</returns>
    public static Result Failure(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result(error);
    }

    /// <summary>
    /// Collapses both outcomes into one value, which is how a caller consumes a result without
    /// asking whether it succeeded first.
    /// </summary>
    /// <typeparam name="TOut">What the caller wants back.</typeparam>
    /// <param name="onSuccess">Produces the value for a success.</param>
    /// <param name="onFailure">Produces the value for a failure.</param>
    /// <returns>Whichever branch applied.</returns>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<AppError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return Error is null ? onSuccess() : onFailure(Error);
    }
}
