using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Actions;
using SessionFinder.Presentation.Search;
using SessionFinder.Presentation.Tests.Fakes;

namespace SessionFinder.Presentation.Tests.Actions;

public sealed class SessionActionsViewModelTests
{
    private readonly FakeSessionActionHandlers _handlers = new();
    private readonly RecordingUserNotifier _notifier = new();

    [Fact]
    public void Target_ASessionWithAFolder_EnablesTheFolderBoundCommands()
    {
        var viewModel = CreateViewModel();

        viewModel.Target = Row(@"C:\git\my repo");

        viewModel.OpenInEditorCommand.CanExecute(null).Should().BeTrue();
        viewModel.ResumeSessionCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Target_ASessionWithNoFolder_DisablesTheFolderBoundCommands()
    {
        var viewModel = CreateViewModel();

        viewModel.Target = Row(folder: null);

        viewModel.OpenInEditorCommand.CanExecute(null).Should().BeFalse();
        viewModel.RevealInFileManagerCommand.CanExecute(null).Should().BeFalse();
        viewModel.ResumeSessionCommand.CanExecute(null).Should().BeFalse();
        viewModel.CopyFolderPathCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Target_ASessionWithNoFolder_LeavesCopyingTheIdentifierAvailable()
    {
        var viewModel = CreateViewModel();

        viewModel.Target = Row(folder: null);

        viewModel.CopySessionIdCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Target_ASessionWithNoFolder_ExplainsWhyTheActionsAreUnavailable()
    {
        var viewModel = CreateViewModel();

        viewModel.Target = Row(folder: null);

        viewModel.DisabledReason.Should().Be(AppError.FolderUnknown.Message);
    }

    [Fact]
    public void Target_ASessionWithAFolder_HasNothingToExplain()
    {
        var viewModel = CreateViewModel();

        viewModel.Target = Row(@"C:\git\my repo");

        viewModel.DisabledReason.Should().BeNull();
    }

    [Fact]
    public void Target_NoSelection_DisablesEveryCommand()
    {
        var viewModel = CreateViewModel();

        viewModel.Target = null;

        viewModel.CopySessionIdCommand.CanExecute(null).Should().BeFalse();
        viewModel.OpenInEditorCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task OpenInEditor_ASessionWithAFolder_AsksTheHandlerToOpenIt()
    {
        var viewModel = CreateViewModel();
        viewModel.Target = Row(@"C:\git\my repo");

        await viewModel.OpenInEditorCommand.ExecuteAsync(null);

        _handlers.Calls.Should().Equal(nameof(OpenFolderInEditorCommand));
    }

    [Fact]
    public async Task OpenInEditor_AnActionThatWorked_ReportsThatTheLauncherCanGetOutOfTheWay()
    {
        var viewModel = CreateViewModel();
        viewModel.Target = Row(@"C:\git\my repo");
        var succeeded = 0;
        viewModel.Succeeded += (_, _) => succeeded++;

        await viewModel.OpenInEditorCommand.ExecuteAsync(null);

        succeeded.Should().Be(1);
    }

    [Fact]
    public async Task ResumeSession_AHandlerThatRefused_TellsTheUserWhy()
    {
        _handlers.Refuses(AppError.TerminalNotFound);
        var viewModel = CreateViewModel();
        viewModel.Target = Row(@"C:\git\my repo");

        await viewModel.ResumeSessionCommand.ExecuteAsync(null);

        _notifier.Notifications.Should().ContainSingle()
            .Which.Message.Should().Be(AppError.TerminalNotFound.Message);
    }

    [Fact]
    public async Task ResumeSession_AHandlerThatRefused_DoesNotReportSuccess()
    {
        _handlers.Refuses(AppError.TerminalNotFound);
        var viewModel = CreateViewModel();
        viewModel.Target = Row(@"C:\git\my repo");
        var succeeded = 0;
        viewModel.Succeeded += (_, _) => succeeded++;

        await viewModel.ResumeSessionCommand.ExecuteAsync(null);

        succeeded.Should().Be(0);
    }

    [Fact]
    public async Task RevealInFileManager_AHandlerThatThrew_NotifiesWithoutLettingTheExceptionEscape()
    {
        _handlers.Throws(new InvalidOperationException("the shell gave up"));
        var viewModel = CreateViewModel();
        viewModel.Target = Row(@"C:\git\my repo");

        await viewModel.RevealInFileManagerCommand.ExecuteAsync(null);

        _notifier.Notifications.Should().ContainSingle()
            .Which.Severity.Should().Be(NotificationSeverity.Error);
    }

    [Fact]
    public async Task CopySessionId_ASessionWithNoFolder_StillReachesTheHandler()
    {
        var viewModel = CreateViewModel();
        viewModel.Target = Row(folder: null);

        await viewModel.CopySessionIdCommand.ExecuteAsync(null);

        _handlers.Calls.Should().Equal($"{nameof(CopySessionDetailCommand)}:{SessionDetail.SessionId}");
    }

    [Fact]
    public async Task CopyResumeCommandLine_ASessionWithAFolder_AsksForTheWholeCommandLine()
    {
        var viewModel = CreateViewModel();
        viewModel.Target = Row(@"C:\git\my repo");

        await viewModel.CopyResumeCommandLineCommand.ExecuteAsync(null);

        _handlers.Calls.Should()
            .Equal($"{nameof(CopySessionDetailCommand)}:{SessionDetail.ResumeCommandLine}");
    }

    [Fact]
    public async Task OpenInEditor_NoSelection_DoesNothingAtAll()
    {
        var viewModel = CreateViewModel();

        await viewModel.OpenInEditorCommand.ExecuteAsync(null);

        _handlers.Calls.Should().BeEmpty();
    }

    private static SessionResultViewModel Row(string? folder) => new()
    {
        SessionId = new SessionId(Guid.NewGuid()),
        FilePath = @"C:\transcripts\session.jsonl",
        Title = "A session",
        TitleSource = TitleSource.CustomTitle,
        LastActivity = "just now",
        WorkingFolder = WorkingFolder.FromTranscriptCwd(folder),
    };

    private SessionActionsViewModel CreateViewModel() => new(
        _handlers,
        _handlers,
        _handlers,
        _handlers,
        TestGuard.Over(_notifier));
}
