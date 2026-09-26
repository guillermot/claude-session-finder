using Microsoft.Extensions.Time.Testing;
using SessionFinder.Infrastructure.Indexing;

namespace SessionFinder.Infrastructure.Tests.Indexing;

public sealed class PendingChangeBufferTests
{
    private const string FirstTranscript = @"C:\projects\example\11111111-1111-4111-8111-111111111111.jsonl";
    private const string SecondTranscript = @"C:\projects\example\22222222-2222-4222-8222-222222222222.jsonl";
    private const int WritesPerSecond = 2;

    private static readonly TimeSpan SettleFor = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WaitAtMost = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan BetweenWrites = TimeSpan.FromSeconds(1) / WritesPerSecond;

    private readonly FakeTimeProvider _time = new();
    private readonly PendingChangeBuffer _buffer;

    public PendingChangeBufferTests() => _buffer = new PendingChangeBuffer(_time);

    [Fact]
    public void TakeReady_PathReportedOnce_IsNotReadyBeforeTheSettleWindowElapses()
    {
        _buffer.Record(FirstTranscript);

        _time.Advance(SettleFor - TimeSpan.FromMilliseconds(1));

        _buffer.TakeReady(SettleFor, WaitAtMost).Should().BeEmpty();
    }

    [Fact]
    public void TakeReady_PathQuietForLongerThanTheSettleWindow_IsReady()
    {
        _buffer.Record(FirstTranscript);

        _time.Advance(SettleFor + TimeSpan.FromMilliseconds(1));

        _buffer.TakeReady(SettleFor, WaitAtMost).Should().ContainSingle().Which.Should().Be(FirstTranscript);
    }

    [Fact]
    public void TakeReady_PathStillBeingWritten_IsNotReadyOnTheSettleEdgeAlone()
    {
        var takenBeforeTheMaximumWait = WriteContinuouslyFor(WaitAtMost - BetweenWrites);

        takenBeforeTheMaximumWait.Should().BeEmpty(
            "a file that is never quiet never reaches the edge that waits for quiet");
    }

    [Fact]
    public void TakeReady_PathStillBeingWrittenPastTheMaximumWait_IsReadyAnyway()
    {
        var takenWhileStillBeingWritten = WriteContinuouslyFor(WaitAtMost + BetweenWrites);

        takenWhileStillBeingWritten.Should().ContainSingle().Which.Should().Be(FirstTranscript);
    }

    [Fact]
    public void TakeReady_PathAlreadyHandedOver_IsNotHandedOverAgain()
    {
        _buffer.Record(FirstTranscript);
        _time.Advance(SettleFor + TimeSpan.FromMilliseconds(1));
        _buffer.TakeReady(SettleFor, WaitAtMost);

        _time.Advance(WaitAtMost);

        _buffer.TakeReady(SettleFor, WaitAtMost).Should().BeEmpty();
    }

    [Fact]
    public void Record_SamePathReportedRepeatedly_CollapsesOntoOneEntry()
    {
        for (var write = 0; write < 100; write++)
        {
            _buffer.Record(FirstTranscript);
            _time.Advance(BetweenWrites);
        }

        _buffer.Count.Should().Be(1);
    }

    [Fact]
    public void Record_DistinctPaths_AreHeldSeparately()
    {
        _buffer.Record(FirstTranscript);
        _buffer.Record(SecondTranscript);

        _buffer.Count.Should().Be(2);
    }

    [Fact]
    public void TakeReady_OnePathSettledAndAnotherStillBeingWritten_HandsOverOnlyTheSettledOne()
    {
        _buffer.Record(FirstTranscript);
        _time.Advance(SettleFor + TimeSpan.FromMilliseconds(1));
        _buffer.Record(SecondTranscript);

        var ready = _buffer.TakeReady(SettleFor, WaitAtMost);

        ready.Should().ContainSingle().Which.Should().Be(FirstTranscript);
    }

    [Fact]
    public void TakeAll_PathsThatHaveNotSettled_ReturnsEveryOneOfThem()
    {
        _buffer.Record(FirstTranscript);
        _buffer.Record(SecondTranscript);

        var all = _buffer.TakeAll();

        all.Should().BeEquivalentTo(new[] { FirstTranscript, SecondTranscript });
    }

    [Fact]
    public void TakeAll_AfterEverythingWasTaken_LeavesTheBufferEmpty()
    {
        _buffer.Record(FirstTranscript);
        _buffer.TakeAll();

        _buffer.Count.Should().Be(0);
    }

    /// <summary>
    /// Reproduces a session that is being worked in: a notification every half second, with the
    /// indexer looking for settled paths just as often.
    /// </summary>
    /// <param name="duration">How long the writing goes on for.</param>
    /// <returns>Every path the buffer handed over during that time.</returns>
    private List<string> WriteContinuouslyFor(TimeSpan duration)
    {
        var taken = new List<string>();

        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += BetweenWrites)
        {
            _buffer.Record(FirstTranscript);
            _time.Advance(BetweenWrites);
            taken.AddRange(_buffer.TakeReady(SettleFor, WaitAtMost));
        }

        return taken;
    }
}
