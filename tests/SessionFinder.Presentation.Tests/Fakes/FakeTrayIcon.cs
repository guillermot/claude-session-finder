using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Presentation.Tests.Fakes;

internal sealed class FakeTrayIcon : ITrayIcon
{
    public event EventHandler? SearchRequested;

    public event EventHandler? RecapRequested;

    public event EventHandler? RebuildIndexRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? LogFolderRequested;

    public event EventHandler? ExitRequested;

    public bool IsShown { get; private set; }

    public string? Tooltip { get; private set; }

    public void Show() => IsShown = true;

    public void SetTooltip(string text) => Tooltip = text;

    public void RaiseSearchRequested() => SearchRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseRecapRequested() => RecapRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseRebuildIndexRequested() => RebuildIndexRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseSettingsRequested() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseLogFolderRequested() => LogFolderRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseExitRequested() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
