using Microsoft.Extensions.Time.Testing;
using SessionFinder.Presentation.Search;

namespace SessionFinder.Presentation.Tests.Search;

public sealed class SearchDebouncerTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(150);

    [Fact]
    public void ScheduleAsync_DelayHasNotElapsed_DoesNotRunTheWork()
    {
        var time = new FakeTimeProvider();
        using var debouncer = new SearchDebouncer(time, Delay);
        var runCount = 0;

        _ = debouncer.ScheduleAsync(
            _ => { runCount++; return Task.FromResult("result"); },
            _ => { },
            CancellationToken.None);
        time.Advance(Delay - TimeSpan.FromMilliseconds(1));

        runCount.Should().Be(0);
    }

    [Fact]
    public async Task ScheduleAsync_DelayElapses_AppliesTheResult()
    {
        var time = new FakeTimeProvider();
        using var debouncer = new SearchDebouncer(time, Delay);
        var applied = new List<string>();

        var scheduled = debouncer.ScheduleAsync(
            _ => Task.FromResult("result"),
            applied.Add,
            CancellationToken.None);
        time.Advance(Delay);
        await scheduled;

        applied.Should().ContainSingle().Which.Should().Be("result");
    }

    [Fact]
    public async Task ScheduleAsync_KeystrokeArrivesBeforeTheDelayElapses_RunsOnlyTheLastRequest()
    {
        var time = new FakeTimeProvider();
        using var debouncer = new SearchDebouncer(time, Delay);
        var started = new List<string>();

        _ = debouncer.ScheduleAsync(Work("first"), _ => { }, CancellationToken.None);
        time.Advance(TimeSpan.FromMilliseconds(100));
        var second = debouncer.ScheduleAsync(Work("second"), _ => { }, CancellationToken.None);
        time.Advance(Delay);
        await second;

        started.Should().ContainSingle().Which.Should().Be("second");

        Func<CancellationToken, Task<string>> Work(string name) => _ =>
        {
            started.Add(name);
            return Task.FromResult(name);
        };
    }

    [Fact]
    public async Task ScheduleAsync_NewerRequestArrivesWhileWorkIsRunning_CancelsTheWorkInFlight()
    {
        var time = new FakeTimeProvider();
        using var debouncer = new SearchDebouncer(time, Delay);
        var inFlight = new InFlightWork();

        var first = debouncer.ScheduleAsync(inFlight.RunAsync, _ => { }, CancellationToken.None);
        time.Advance(Delay);
        await inFlight.Started;

        _ = debouncer.ScheduleAsync(_ => Task.FromResult("second"), _ => { }, CancellationToken.None);
        inFlight.Complete("first");
        await first;

        inFlight.WasCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task ScheduleAsync_StaleRequestCompletesAfterANewerOne_DropsTheStaleResult()
    {
        var time = new FakeTimeProvider();
        using var debouncer = new SearchDebouncer(time, Delay);
        var stale = new InFlightWork();
        var applied = new List<string>();

        var staleRequest = debouncer.ScheduleAsync(stale.RunAsync, applied.Add, CancellationToken.None);
        time.Advance(Delay);
        await stale.Started;

        var newerRequest = debouncer.ScheduleAsync(
            _ => Task.FromResult("newer"),
            applied.Add,
            CancellationToken.None);
        time.Advance(Delay);
        await newerRequest;

        stale.Complete("stale");
        await staleRequest;

        applied.Should().ContainSingle().Which.Should().Be("newer");
    }

    [Fact]
    public async Task ScheduleAsync_OwningTokenIsCancelled_DoesNotApplyAnything()
    {
        var time = new FakeTimeProvider();
        using var debouncer = new SearchDebouncer(time, Delay);
        using var owner = new CancellationTokenSource();
        var applied = new List<string>();

        var scheduled = debouncer.ScheduleAsync(_ => Task.FromResult("result"), applied.Add, owner.Token);
        await owner.CancelAsync();
        time.Advance(Delay);
        await scheduled;

        applied.Should().BeEmpty();
    }

    [Fact]
    public void IsCurrent_ANewerRequestHasBeenScheduled_ReportsTheOlderOneStale()
    {
        var time = new FakeTimeProvider();
        using var debouncer = new SearchDebouncer(time, Delay);

        _ = debouncer.ScheduleAsync(_ => Task.FromResult("first"), _ => { }, CancellationToken.None);
        var older = debouncer.LatestSequence;
        _ = debouncer.ScheduleAsync(_ => Task.FromResult("second"), _ => { }, CancellationToken.None);

        debouncer.IsCurrent(older).Should().BeFalse();
    }

    /// <summary>
    /// Work that starts when it is called and finishes only when the test says so, which is what
    /// lets a completion be ordered deliberately after a newer request.
    /// </summary>
    private sealed class InFlightWork
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public bool WasCancelled { get; private set; }

        public Task<string> RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => WasCancelled = true);
            _started.SetResult();

            return _completion.Task;
        }

        public void Complete(string result) => _completion.SetResult(result);
    }
}
