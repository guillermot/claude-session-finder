using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.DailyRecap;
using SessionFinder.Core.Results;
using static SessionFinder.Core.Tests.Features.DailyRecap.RecapFixtures;

namespace SessionFinder.Core.Tests.Features.DailyRecap;

public sealed class SummarizeRecapHandlerTests
{
    private readonly IRecapSummarizer _summarizer = Substitute.For<IRecapSummarizer>();

    [Fact]
    public async Task HandleAsync_NoActivity_RefusesWithoutAskingTheModel()
    {
        var result = await Handler().HandleAsync(
            new SummarizeRecapCommand(new DailyRecapResult { Today = Day(10, 5) }),
            CancellationToken.None);

        result.Error.Should().Be(AppError.NothingToSummarize);
        await _summarizer.DidNotReceiveWithAnyArgs().SummarizeAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_FridayOnAMonday_AsksForTheDayByNameRatherThanYesterday()
    {
        _summarizer.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("- did things"));

        var result = await Handler().HandleAsync(new SummarizeRecapCommand(FridayRecap()), CancellationToken.None);

        result.Value.Should().Be("- did things");
        await _summarizer.Received(1).SummarizeAsync(
            Arg.Is<string>(prompt => prompt.Contains("**On Friday**") && prompt.Contains("Asked: make the tests neutral")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TheModelFails_PassesTheReasonOn()
    {
        var failure = AppError.SummaryFailed("not signed in");
        _summarizer.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(failure));

        var result = await Handler().HandleAsync(new SummarizeRecapCommand(FridayRecap()), CancellationToken.None);

        result.Error.Should().Be(failure);
    }

    private SummarizeRecapHandler Handler() => new(_summarizer, NullLogger<SummarizeRecapHandler>.Instance);

    private static DailyRecapResult FridayRecap()
    {
        var session = new SessionRecap
        {
            SessionId = new SessionId(Guid.NewGuid()),
            Title = "Path tests",
            PromptCount = 3,
            FirstActivity = At(10, 2, 10),
            LastActivity = At(10, 2, 11),
            KeyPrompts = ["make the tests neutral"],
        };

        return new DailyRecapResult
        {
            Today = Day(10, 5),
            Focus = new DayRecap(Day(10, 2),
            [
                new ProjectRecap
                {
                    Name = "finder",
                    Path = "/work/finder",
                    Sessions = [session],
                    FirstActivity = session.FirstActivity,
                    LastActivity = session.LastActivity,
                },
            ]),
        };
    }
}
