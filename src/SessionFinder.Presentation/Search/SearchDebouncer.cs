namespace SessionFinder.Presentation.Search;

/// <summary>
/// Turns a stream of keystrokes into at most one search, and guarantees that the result that lands
/// is the result of the last thing the user typed.
/// </summary>
/// <remarks>
/// <para>
/// Two mechanisms, and both are needed. Cancelling the previous request stops work nobody is
/// waiting for; the request sequence decides which answer is allowed to be shown. Cancellation
/// alone is not enough, because a request that is a millisecond from returning will ignore a token
/// cancelled a microsecond ago and complete <em>after</em> a newer request that started later and
/// finished sooner. Without the sequence check, the search box would then show the results of a
/// prefix of what is in it.
/// </para>
/// <para>
/// The delay comes from an injected <see cref="TimeProvider"/> rather than from the clock, so the
/// behaviour above is asserted in tests that take microseconds and do not flake.
/// </para>
/// </remarks>
/// <param name="timeProvider">The clock the delay is measured against.</param>
/// <param name="delay">How long typing must pause before the work runs.</param>
public sealed class SearchDebouncer(TimeProvider timeProvider, TimeSpan delay) : IDisposable
{
    private readonly Lock _gate = new();

    private CancellationTokenSource? _pending;
    private long _issuedSequence;
    private volatile bool _isDisposed;

    /// <summary>The sequence number of the most recently scheduled request.</summary>
    public long LatestSequence => Interlocked.Read(ref _issuedSequence);

    /// <summary>
    /// Whether a request is still the newest one, and therefore still allowed to publish.
    /// </summary>
    /// <param name="sequence">The sequence number the request was issued with.</param>
    /// <returns><see langword="true"/> when nothing newer has been scheduled since.</returns>
    public bool IsCurrent(long sequence) => !_isDisposed && LatestSequence == sequence;

    /// <summary>
    /// Schedules work to run once typing has paused, cancelling whatever was scheduled before it.
    /// </summary>
    /// <typeparam name="TResult">What the work produces.</typeparam>
    /// <param name="work">The work to run once the pause has elapsed.</param>
    /// <param name="apply">
    /// Called with the result, and only when the request is still the newest one. It runs on
    /// whichever thread the work completed on, so a caller that touches the user interface has to
    /// marshal from here.
    /// </param>
    /// <param name="cancellationToken">Cancels the request along with whatever owns it.</param>
    /// <returns>
    /// A task that completes when the request has run, been superseded or been cancelled. It never
    /// faults on cancellation, so a caller that discards it is not leaving an exception unobserved.
    /// </returns>
    public Task ScheduleAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        Action<TResult> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(apply);

        CancellationTokenSource source;
        long sequence;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            CancelPending();

            source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pending = source;
            sequence = ++_issuedSequence;
        }

        return RunAsync(work, apply, sequence, source);
    }

    /// <summary>Cancels anything in flight and refuses further requests.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            CancelPending();
        }
    }

    private async Task RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        Action<TResult> apply,
        long sequence,
        CancellationTokenSource source)
    {
        var token = source.Token;

        try
        {
            await Task.Delay(delay, timeProvider, token).ConfigureAwait(false);

            var result = await work(token).ConfigureAwait(false);

            if (IsCurrent(sequence))
            {
                apply(result);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            Release(source);
        }
    }

    /// <summary>
    /// Cancels the request in flight without disposing its source. Disposal is left to the task
    /// that owns it, because that task still reads the token after every await and would fault on a
    /// source pulled out from under it.
    /// </summary>
    private void CancelPending()
    {
        var pending = _pending;
        _pending = null;
        pending?.Cancel();
    }

    private void Release(CancellationTokenSource source)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_pending, source))
            {
                _pending = null;
            }
        }

        source.Dispose();
    }
}
