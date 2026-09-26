using NSubstitute;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.IndexStatus;

namespace SessionFinder.Core.Tests.Features.IndexStatus;

public sealed class GetIndexStatusHandlerTests
{
    private const string DatabasePath = @"C:\cache\ClaudeSessionFinder\index.db";

    private readonly ISessionIndexReader _reader = Substitute.For<ISessionIndexReader>();

    [Fact]
    public async Task HandleAsync_IndexNeverBuilt_ReportsTheDatabaseAsAbsent()
    {
        _reader
            .GetStatisticsAsync(Arg.Any<CancellationToken>())
            .Returns(IndexStatistics.Empty(DatabasePath));

        var result = await new GetIndexStatusHandler(_reader).HandleAsync(
            GetIndexStatusQuery.Instance,
            CancellationToken.None);

        result.Index.DatabaseExists.Should().BeFalse();
        result.Index.DatabasePath.Should().Be(DatabasePath);
    }

    [Fact]
    public async Task HandleAsync_IndexBuilt_ReportsTheCountsTheReaderMeasured()
    {
        _reader.GetStatisticsAsync(Arg.Any<CancellationToken>()).Returns(BuildStatistics());

        var result = await new GetIndexStatusHandler(_reader).HandleAsync(
            GetIndexStatusQuery.Instance,
            CancellationToken.None);

        result.Index.SessionCount.Should().Be(96);
        result.Index.ChunksByKind[ChunkKind.UserPrompt].Should().Be(1_493);
    }

    private static IndexStatistics BuildStatistics() => IndexStatistics.Empty(DatabasePath) with
    {
        DatabaseExists = true,
        SessionCount = 96,
        ChunkCount = 1_493,
        ChunksByKind = new Dictionary<ChunkKind, int> { [ChunkKind.UserPrompt] = 1_493 },
    };
}
