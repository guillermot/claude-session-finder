using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Results;
using SessionFinder.Infrastructure.Processes;
using SessionFinder.Infrastructure.Recap;

namespace SessionFinder.Infrastructure.Tests.Recap;

public sealed class ClaudeCliRecapSummarizerTests
{
    private const string Prompt = "summarise this";

    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly RecapOptions _options = new() { ClaudeExecutable = typeof(ClaudeCliRecapSummarizerTests).Assembly.Location };

    [Fact]
    public void BuildRequest_SendsThePromptOnStandardInputAndNeverOnTheCommandLine()
    {
        var request = ClaudeCliRecapSummarizer.BuildRequest("/bin/claude", "haiku", Prompt);

        request.StandardInput.Should().Be(Prompt);
        request.Arguments.Should().NotContain(Prompt);
    }

    [Fact]
    public void BuildRequest_RunsWithoutToolsAndWithoutSavingASession()
    {
        var request = ClaudeCliRecapSummarizer.BuildRequest("/bin/claude", "haiku", Prompt);

        request.Arguments.Should().ContainInConsecutiveOrder("--tools", string.Empty);
        request.Arguments.Should().Contain("--no-session-persistence");
        request.Arguments.Should().ContainInConsecutiveOrder("--model", "haiku");
    }

    [Fact]
    public async Task SummarizeAsync_TheCommandAnswers_ReturnsTheTrimmedReply()
    {
        _runner.RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome { StandardOutput = "\n- shipped it\n\n" });

        var result = await Summarizer().SummarizeAsync(Prompt, CancellationToken.None);

        result.Value.Should().Be("- shipped it");
    }

    [Fact]
    public async Task SummarizeAsync_TheCommandFails_ReportsItsFirstLineOfError()
    {
        _runner.RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome { ExitCode = 1, StandardError = "Invalid API key\nmore detail" });

        var result = await Summarizer().SummarizeAsync(Prompt, CancellationToken.None);

        result.Error!.Message.Should().EndWith("Invalid API key");
    }

    [Fact]
    public async Task SummarizeAsync_TheCommandTimesOut_SaysSo()
    {
        _runner.RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>()).Returns(ProcessOutcome.Expired);

        var result = await Summarizer().SummarizeAsync(Prompt, CancellationToken.None);

        result.Error!.Message.Should().Contain("did not answer");
    }

    [Fact]
    public async Task SummarizeAsync_NoExecutable_RefusesWithoutRunningAnything()
    {
        _options.ClaudeExecutable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "claude");

        var result = await Summarizer().SummarizeAsync(Prompt, CancellationToken.None);

        result.Error.Should().Be(AppError.SummarizerNotFound);
        await _runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    private ClaudeCliRecapSummarizer Summarizer() => new(
        _runner,
        new StaticOptionsMonitor(_options),
        NullLogger<ClaudeCliRecapSummarizer>.Instance);

    private sealed class StaticOptionsMonitor(RecapOptions value) : IOptionsMonitor<RecapOptions>
    {
        public RecapOptions CurrentValue => value;

        public RecapOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<RecapOptions, string?> listener) => null;
    }
}
