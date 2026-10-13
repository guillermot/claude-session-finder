using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;
using SessionFinder.Mac.Platform;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Search;

namespace SessionFinder.Mac.Views;

/// <summary>
/// The search box: a borderless window that appears over whatever the user was doing, takes the
/// keyboard, and goes away again.
/// </summary>
/// <remarks>
/// <para>
/// It is never closed, only hidden. Building the visual tree and the bindings costs enough to be
/// visible at the moment the chord is pressed, so the window is made once at startup and kept.
/// Closing is intercepted for the same reason: the shell has no legitimate reason to destroy it.
/// </para>
/// <para>
/// Losing focus hides it by default, which is how a launcher behaves. That is a setting rather than
/// a constant because it makes the window impossible to inspect while it is being worked on: a
/// debugger breaking in, a log window opening, or a screenshot tool taking focus all take the thing
/// under examination off the screen.
/// </para>
/// </remarks>
internal sealed partial class SearchWindow : Window, IAppWindow
{
    private const double TopOfScreenFraction = 0.2;
    private const int SelectionStep = 1;

    private readonly SearchViewModel _viewModel;
    private readonly IOptionsMonitor<ShellOptions> _options;

    /// <summary>
    /// Builds the window and binds it to the search view model.
    /// </summary>
    /// <param name="viewModel">What the window shows.</param>
    /// <param name="options">Whether losing focus dismisses the window.</param>
    public SearchWindow(SearchViewModel viewModel, IOptionsMonitor<ShellOptions> options)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(options);

        _viewModel = viewModel;
        _options = options;

        InitializeComponent();

        DataContext = viewModel;

        viewModel.Actions.Succeeded += OnActionSucceeded;

        // Tunnelling, so that these keys are decided before the text box has a chance to treat them
        // as editing. Bubbling would give the text box first refusal on every one of them.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        Deactivated += OnWindowDeactivated;
    }

    /// <summary>
    /// Shows the window on the screen the pointer is on, takes the foreground, refreshes the
    /// results and puts the caret in the search box with the previous query selected.
    /// </summary>
    public new void Show()
    {
        base.Show();

        PositionOnCursorScreen();

        Activate();
        QueryBox.Focus();
        QueryBox.SelectAll();

        _viewModel.Refresh();
    }

    /// <summary>Hides the window and empties the search box, so the next chord starts clean.</summary>
    public new void Hide()
    {
        base.Hide();

        _viewModel.Query = string.Empty;
    }

    /// <summary>
    /// Turns a close request into a hide. The application outlives its window.
    /// </summary>
    /// <param name="e">The cancellable close request.</param>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        e.Cancel = true;

        Hide();
    }

    /// <summary>
    /// Handles the keys the search box owns before the text box sees them.
    /// </summary>
    /// <remarks>
    /// A shortcut that lands on an unavailable action is still swallowed. Letting it through would
    /// mean the copy shortcuts fell back to the text box's own copy, so pressing the key for "copy
    /// this session's folder" on a session that has none would silently copy the query instead.
    /// </remarks>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = TryHandle(e.Key, e.KeyModifiers);
    }

    /// <remarks>
    /// The Command key arrives as <see cref="KeyModifiers.Meta"/>, and it stands where Control does
    /// on Windows: the chords are the Mac spelling of the same six actions.
    /// </remarks>
    private bool TryHandle(Key key, KeyModifiers modifiers) => key switch
    {
        Key.Escape => Dismiss(),
        Key.Down when modifiers == KeyModifiers.None => MoveSelection(SelectionStep),
        Key.Up when modifiers == KeyModifiers.None => MoveSelection(-SelectionStep),
        Key.Enter => Run(OpenShortcutFor(modifiers)),
        Key.C when modifiers.HasFlag(KeyModifiers.Meta) => Run(CopyShortcutFor(modifiers)),
        Key.R when modifiers == KeyModifiers.Meta => Run(_viewModel.ShowRecapCommand),
        _ => false,
    };

    /// <summary>
    /// Pressing the key for an action the selected session cannot take does nothing, on purpose:
    /// the reason is already on screen under the shortcuts, and a second way of saying it would
    /// interrupt a launcher the user is about to dismiss anyway.
    /// </summary>
    private static bool Run(ICommand? command)
    {
        if (command is null)
        {
            return false;
        }

        if (command.CanExecute(parameter: null))
        {
            command.Execute(parameter: null);
        }

        return true;
    }

    private ICommand? OpenShortcutFor(KeyModifiers modifiers)
    {
        var actions = _viewModel.Actions;

        return modifiers switch
        {
            KeyModifiers.None => actions.OpenInEditorCommand,
            KeyModifiers.Meta => actions.ResumeSessionCommand,
            KeyModifiers.Meta | KeyModifiers.Shift => actions.RevealInFileManagerCommand,
            _ => null,
        };
    }

    private ICommand? CopyShortcutFor(KeyModifiers modifiers)
    {
        var actions = _viewModel.Actions;

        return modifiers switch
        {
            KeyModifiers.Meta => actions.CopyFolderPathCommand,
            KeyModifiers.Meta | KeyModifiers.Shift => actions.CopySessionIdCommand,
            KeyModifiers.Meta | KeyModifiers.Alt => actions.CopyResumeCommandLineCommand,
            _ => null,
        };
    }

    private bool Dismiss()
    {
        Hide();

        return true;
    }

    private bool MoveSelection(int offset)
    {
        _viewModel.MoveSelection(offset);
        ScrollSelectionIntoView();

        return true;
    }

    private void OnActionSucceeded(object? sender, EventArgs e) => Hide();

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_options.CurrentValue.HideOnDeactivate && IsVisible)
        {
            Hide();
        }
    }

    private void ScrollSelectionIntoView()
    {
        if (_viewModel.SelectedResult is { } selected)
        {
            ResultList.ScrollIntoView(selected);
        }
    }

    /// <summary>
    /// Puts the window on the screen the pointer is on, horizontally centred and high up, which is
    /// where a launcher is looked for and above where most windows put content.
    /// </summary>
    /// <remarks>
    /// Avalonia positions a window in physical pixels, which is what the screen's working area is
    /// already expressed in, so — unlike the Windows head — there is no conversion to do. The
    /// window's own width does need scaling, because that side of it is in device-independent
    /// units. This runs after the window is shown, because before that it has no screen to be
    /// scaled by.
    /// </remarks>
    private void PositionOnCursorScreen()
    {
        if (CursorScreen.ForPointer(Screens) is not { } screen)
        {
            return;
        }

        var area = screen.WorkingArea;
        var width = (int)(Width * screen.Scaling);

        Position = new Avalonia.PixelPoint(
            area.X + ((area.Width - width) / 2),
            area.Y + (int)(area.Height * TopOfScreenFraction));
    }

    /// <inheritdoc />
    bool IAppWindow.IsVisible => IsVisible;

    /// <inheritdoc />
    void IAppWindow.Show() => Show();

    /// <inheritdoc />
    void IAppWindow.Hide() => Hide();
}
