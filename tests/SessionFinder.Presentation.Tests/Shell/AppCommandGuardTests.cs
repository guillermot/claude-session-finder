using SessionFinder.Core.Results;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Tests.Fakes;

namespace SessionFinder.Presentation.Tests.Shell;

public sealed class AppCommandGuardTests
{
    private const string Action = "open folder";
    private const string FailureTitle = "Could not open the folder";

    private readonly RecordingUserNotifier _notifier = new();

    [Fact]
    public async Task RunAsync_TheWorkSucceeds_ReportsSuccess()
    {
        var guard = TestGuard.Over(_notifier);

        var succeeded = await guard.RunAsync(Action, FailureTitle, () => Task.FromResult(Result.Success()));

        succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_TheWorkSucceeds_SaysNothingToTheUser()
    {
        var guard = TestGuard.Over(_notifier);

        await guard.RunAsync(Action, FailureTitle, () => Task.FromResult(Result.Success()));

        _notifier.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_TheWorkIsRefused_ShowsTheReasonAsAWarning()
    {
        var guard = TestGuard.Over(_notifier);

        await guard.RunAsync(Action, FailureTitle, () => Task.FromResult(Result.Failure(AppError.FolderUnknown)));

        _notifier.Notifications.Should().ContainSingle()
            .Which.Severity.Should().Be(NotificationSeverity.Warning);
    }

    [Fact]
    public async Task RunAsync_TheWorkIsRefused_UsesTheErrorsOwnWording()
    {
        var guard = TestGuard.Over(_notifier);

        await guard.RunAsync(Action, FailureTitle, () => Task.FromResult(Result.Failure(AppError.FolderUnknown)));

        _notifier.Notifications[0].Message.Should().Be(AppError.FolderUnknown.Message);
    }

    [Fact]
    public async Task RunAsync_TheWorkThrows_ShowsAnErrorInsteadOfPropagating()
    {
        var guard = TestGuard.Over(_notifier);

        await guard.RunAsync(Action, FailureTitle, () => throw new InvalidOperationException("boom"));

        _notifier.Notifications.Should().ContainSingle()
            .Which.Severity.Should().Be(NotificationSeverity.Error);
    }

    [Fact]
    public async Task RunAsync_TheWorkThrows_KeepsTheExceptionOutOfTheMessage()
    {
        var guard = TestGuard.Over(_notifier);

        await guard.RunAsync(Action, FailureTitle, () => throw new InvalidOperationException("boom"));

        _notifier.Notifications[0].Message.Should().NotContain("boom");
    }

    [Fact]
    public async Task RunAsync_TheWorkIsCancelled_SaysNothingToTheUser()
    {
        var guard = TestGuard.Over(_notifier);

        await guard.RunAsync(Action, FailureTitle, () => throw new OperationCanceledException());

        _notifier.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_TheWorkIsCancelled_ReportsThatItDidNotComplete()
    {
        var guard = TestGuard.Over(_notifier);

        var succeeded = await guard.RunAsync(Action, FailureTitle, () => throw new OperationCanceledException());

        succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WorkWithNoResult_ReportsSuccessWhenItCompletes()
    {
        var guard = TestGuard.Over(_notifier);

        var succeeded = await guard.RunAsync(Action, FailureTitle, () => Task.CompletedTask);

        succeeded.Should().BeTrue();
    }
}
