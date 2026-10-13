using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.DailyRecap;

namespace SessionFinder.Core.Tests.Features.DailyRecap;

public sealed class SessionDescriptionTests
{
    [Theory]
    [InlineData("Done. The payment sheet is behind the applePay flag and tested.", "The payment sheet is behind the applePay flag and tested.")]
    [InlineData("All set! Invoices now stream straight to the response.", "Invoices now stream straight to the response.")]
    [InlineData("Great, retries now back off exponentially with jitter.", "Retries now back off exponentially with jitter.")]
    [InlineData("Here's what changed: the guide is linked from the sidebar now.", "The guide is linked from the sidebar now.")]
    public void For_AReplyThatOpensWithFiller_KeepsOnlyTheOutcome(string reply, string expected)
    {
        SessionDescription.For(Session(reply)).Should().Be(expected);
    }

    [Fact]
    public void For_AReplyThatEndsByOfferingMoreWork_DropsTheOffer()
    {
        var description = SessionDescription.For(Session(
            "Totals are now computed in integer cents and 50 runs passed. Want me to open a PR as well?"));

        description.Should().Be("Totals are now computed in integer cents and 50 runs passed.");
    }

    [Fact]
    public void For_MarkdownInTheReply_IsShownAsPlainText()
    {
        SessionDescription.For(Session("The new **`/payments/session`** endpoint validates merchants."))
            .Should().Be("The new /payments/session endpoint validates merchants.");
    }

    [Theory]
    [InlineData("Done.")]
    [InlineData("Which branch should I use for this?")]
    [InlineData(null)]
    public void For_AReplyWithNothingToSay_FallsBackToWhatWasAsked(string? reply)
    {
        SessionDescription.For(Session(reply, "add Apple Pay to the checkout page"))
            .Should().Be("Add Apple Pay to the checkout page");
    }

    [Fact]
    public void For_NoReplyAndNoPrompt_IsNothing()
    {
        SessionDescription.For(Session(null)).Should().BeNull();
    }

    [Fact]
    public void For_AVeryLongReply_IsShortenedToAFewLines()
    {
        var reply = string.Join(' ', Enumerable.Repeat("The importer now deduplicates rows by invoice number.", 20));

        SessionDescription.For(Session(reply))!.Length.Should().BeLessThanOrEqualTo(SessionDescription.MaximumLength + 1);
    }

    private static SessionRecap Session(string? outcome, string? prompt = null) => new()
    {
        SessionId = new SessionId(Guid.NewGuid()),
        Title = "A session",
        PromptCount = 4,
        FirstActivity = DateTimeOffset.UnixEpoch,
        LastActivity = DateTimeOffset.UnixEpoch,
        Outcome = outcome,
        KeyPrompts = prompt is null ? [] : [prompt],
    };
}
