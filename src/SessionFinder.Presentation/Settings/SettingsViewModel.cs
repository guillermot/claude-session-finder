using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.Settings;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Presentation.Settings;

/// <summary>
/// What the settings window shows and what saving it does.
/// </summary>
/// <remarks>
/// <para>
/// Saving writes the settings file and stops there. Nothing is applied from here, because
/// everything that depends on a setting is already watching that file: the chord, the
/// start-at-sign-in registration, the ranking weights, the editor and terminal locations and the
/// log level all follow from the write. The one exception is stated on screen rather than worked
/// around — where the index lives is read once, at start-up, so changing it needs a restart.
/// </para>
/// <para>
/// The chord that is actually in force is shown beside the one being edited, because they are
/// genuinely allowed to differ: a chord another application owns makes the shell fall forward to
/// the next candidate, and without this the user would have no way to find out which key works.
/// </para>
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private const string SaveAction = "save settings";
    private const string SaveFailureTitle = "Settings were not saved";
    private const string UnparseableChordMessage =
        "That is not a chord. Write it as Ctrl+Alt+Space: at least one of Ctrl, Alt, Shift or Win, then one key.";

    private const string NoChordRegisteredText = "none — use the tray menu";
    private const string IndexPathNoticeText =
        "Where the index lives is read once at start-up; changing it needs a restart. "
        + "Everything else on this page takes effect as soon as it is saved.";

    private readonly IGetSettingsHandler _get;
    private readonly IUpdateSettingsHandler _update;
    private readonly HotkeyRegistrar _registrar;
    private readonly AppCommandGuard _guard;

    private bool _isDisposed;

    [ObservableProperty]
    private string _hotkey = string.Empty;

    [ObservableProperty]
    private bool _startAtLogin;

    [ObservableProperty]
    private bool _hideOnDeactivate = true;

    [ObservableProperty]
    private string? _editorPath;

    [ObservableProperty]
    private string? _terminalPath;

    [ObservableProperty]
    private int _maxResults;

    [ObservableProperty]
    private double _recencyWeight;

    [ObservableProperty]
    private double _recencyHalfLifeDays;

    [ObservableProperty]
    private double _titleWeight;

    [ObservableProperty]
    private double _folderWeight;

    [ObservableProperty]
    private double _lastPromptWeight;

    [ObservableProperty]
    private double _userPromptWeight;

    [ObservableProperty]
    private double _assistantTextWeight;

    [ObservableProperty]
    private bool _verboseLogging;

    [ObservableProperty]
    private int _recapDayStartHour;

    [ObservableProperty]
    private int _recapLookbackDays;

    [ObservableProperty]
    private bool _recapIncludeGit;

    [ObservableProperty]
    private bool _recapAiSummary;

    [ObservableProperty]
    private string _settingsFilePath = string.Empty;

    [ObservableProperty]
    private string _indexFilePath = string.Empty;

    [ObservableProperty]
    private string _logFolderPath = string.Empty;

    [ObservableProperty]
    private string _registeredChord = string.Empty;

    [ObservableProperty]
    private string? _validationMessage;

    /// <summary>
    /// Builds the view model.
    /// </summary>
    /// <param name="get">Reads the settings as they currently apply.</param>
    /// <param name="update">Validates and stores the settings.</param>
    /// <param name="registrar">Knows which chord is actually held.</param>
    /// <param name="guard">The seam every user-initiated action is reported through.</param>
    public SettingsViewModel(
        IGetSettingsHandler get,
        IUpdateSettingsHandler update,
        HotkeyRegistrar registrar,
        AppCommandGuard guard)
    {
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(guard);

        _get = get;
        _update = update;
        _registrar = registrar;
        _guard = guard;

        _registrar.RegistrationChanged += OnRegistrationChanged;
    }

    /// <summary>Raised once the settings have been written, which is the window's cue to close.</summary>
    public event EventHandler? Saved;

    /// <summary>
    /// What the window says beside the index path: which of these settings needs a restart, and
    /// which do not. The distinction is invisible otherwise, and getting it wrong looks like a bug.
    /// </summary>
    public string IndexPathNotice => IndexPathNoticeText;

    /// <summary>
    /// Fills the form from the settings currently in force. Called every time the window opens, so
    /// that an edit made to the file by hand is shown rather than overwritten.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes once the form has been filled.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var result = await _get.HandleAsync(GetSettingsQuery.Instance, cancellationToken).ConfigureAwait(true);
        var settings = result.Settings;

        Hotkey = settings.Hotkey;
        StartAtLogin = settings.StartAtLogin;
        HideOnDeactivate = settings.HideOnDeactivate;
        EditorPath = settings.EditorPath;
        TerminalPath = settings.TerminalPath;
        MaxResults = settings.MaxResults;
        RecencyWeight = settings.RecencyWeight;
        RecencyHalfLifeDays = settings.RecencyHalfLifeDays;
        VerboseLogging = settings.VerboseLogging;
        RecapDayStartHour = settings.RecapDayStartHour;
        RecapLookbackDays = settings.RecapLookbackDays;
        RecapIncludeGit = settings.RecapIncludeGit;
        RecapAiSummary = settings.RecapAiSummary;

        LoadWeights(settings.ChunkWeights);

        SettingsFilePath = result.SettingsFilePath;
        IndexFilePath = result.IndexFilePath;
        LogFolderPath = result.LogFolderPath;

        ValidationMessage = null;

        RefreshRegisteredChord();
    }

    /// <summary>
    /// Validates the chord, writes the settings and announces that it is done.
    /// </summary>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The running save.</returns>
    [RelayCommand]
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!HotkeyChord.TryParse(Hotkey, out var chord) || chord is null)
        {
            ValidationMessage = UnparseableChordMessage;
            return;
        }

        Hotkey = chord.ToString();
        ValidationMessage = null;

        var command = new UpdateSettingsCommand { Settings = Collect(chord) };
        var saved = await _guard
            .RunAsync(SaveAction, SaveFailureTitle, () => _update.HandleAsync(command, cancellationToken))
            .ConfigureAwait(true);

        if (saved)
        {
            Saved?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Stops following the registration.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _registrar.RegistrationChanged -= OnRegistrationChanged;
    }

    private void LoadWeights(ChunkWeights weights)
    {
        TitleWeight = weights.Title;
        FolderWeight = weights.Folder;
        LastPromptWeight = weights.LastPrompt;
        UserPromptWeight = weights.UserPrompt;
        AssistantTextWeight = weights.AssistantText;
    }

    private FinderSettings Collect(HotkeyChord chord) => new()
    {
        Hotkey = chord.ToString(),
        StartAtLogin = StartAtLogin,
        HideOnDeactivate = HideOnDeactivate,
        EditorPath = EditorPath,
        TerminalPath = TerminalPath,
        MaxResults = MaxResults,
        RecencyWeight = RecencyWeight,
        RecencyHalfLifeDays = RecencyHalfLifeDays,
        ChunkWeights = new ChunkWeights(
            TitleWeight,
            FolderWeight,
            LastPromptWeight,
            UserPromptWeight,
            AssistantTextWeight),
        VerboseLogging = VerboseLogging,
        RecapDayStartHour = RecapDayStartHour,
        RecapLookbackDays = RecapLookbackDays,
        RecapIncludeGit = RecapIncludeGit,
        RecapAiSummary = RecapAiSummary,
    };

    private void OnRegistrationChanged(object? sender, EventArgs e) => RefreshRegisteredChord();

    private void RefreshRegisteredChord() =>
        RegisteredChord = _registrar.RegisteredChord?.ToString() ?? NoChordRegisteredText;
}
