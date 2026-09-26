using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Search;
using WinForms = System.Windows.Forms;

namespace SessionFinder.Wpf.Views;

/// <summary>
/// The search box: a borderless window that appears over whatever the user was doing, takes the
/// keyboard, and goes away again.
/// </summary>
/// <remarks>
/// <para>
/// It is never closed, only hidden. Building the visual tree and the bindings costs enough to be
/// visible at the moment the chord is pressed, so the window is made once at startup and kept.
/// Closing is intercepted for the same reason — the shell has no legitimate reason to destroy it,
/// and Alt+F4 is not a reason either.
/// </para>
/// <para>
/// Losing focus hides it by default, which is how a launcher behaves. That is a setting rather than
/// a constant because it makes the window impossible to inspect while it is being worked on: a
/// debugger breaking in, a log window opening, or a screenshot tool taking focus all take the thing
/// under examination off the screen.
/// </para>
/// </remarks>
internal partial class SearchWindow : Window, IAppWindow
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
    }

    /// <summary>
    /// Shows the window centred on the monitor the cursor is on, takes the foreground, refreshes
    /// the results and puts the caret in the search box with the previous query selected.
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
    protected override void OnClosing(CancelEventArgs e)
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
    /// <param name="e">The key that was pressed.</param>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        e.Handled = TryHandle(key, Keyboard.Modifiers);

        if (!e.Handled)
        {
            base.OnPreviewKeyDown(e);
        }
    }

    private bool TryHandle(Key key, ModifierKeys modifiers) => key switch
    {
        Key.Escape => Dismiss(),
        Key.Down when modifiers == ModifierKeys.None => MoveSelection(SelectionStep),
        Key.Up when modifiers == ModifierKeys.None => MoveSelection(-SelectionStep),
        Key.Enter => Run(OpenShortcutFor(modifiers)),
        Key.C when modifiers.HasFlag(ModifierKeys.Control) => Run(CopyShortcutFor(modifiers)),
        _ => false,
    };

    /// <summary>
    /// Pressing the key for an action the selected session cannot take does nothing, on purpose:
    /// the reason is already on screen under the shortcuts, and a second way of saying it would
    /// interrupt a launcher the user is about to dismiss anyway.
    /// </summary>
    private bool Run(ICommand? command)
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

    private ICommand? OpenShortcutFor(ModifierKeys modifiers)
    {
        var actions = _viewModel.Actions;

        return modifiers switch
        {
            ModifierKeys.None => actions.OpenInEditorCommand,
            ModifierKeys.Control => actions.ResumeSessionCommand,
            ModifierKeys.Control | ModifierKeys.Shift => actions.RevealInFileManagerCommand,
            _ => null,
        };
    }

    private ICommand? CopyShortcutFor(ModifierKeys modifiers)
    {
        var actions = _viewModel.Actions;

        return modifiers switch
        {
            ModifierKeys.Control => actions.CopyFolderPathCommand,
            ModifierKeys.Control | ModifierKeys.Shift => actions.CopySessionIdCommand,
            ModifierKeys.Control | ModifierKeys.Alt => actions.CopyResumeCommandLineCommand,
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

    /// <summary>
    /// Hides the window when it stops being the foreground one, unless that has been turned off.
    /// </summary>
    /// <param name="e">Ignored.</param>
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);

        if (_options.CurrentValue.HideOnDeactivate)
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
    /// Puts the window on the monitor the pointer is on, horizontally centred and high on the
    /// screen, which is where a launcher is looked for and above where most windows put content.
    /// </summary>
    /// <remarks>
    /// The working area comes back in physical pixels while the window is positioned in
    /// device-independent ones, so the rectangle is divided by the scale of the monitor the window
    /// currently reports. This runs after the window is shown, because before that it has no handle
    /// and would report the scale of the primary monitor instead.
    /// </remarks>
    private void PositionOnCursorScreen()
    {
        var workingArea = WinForms.Screen.FromPoint(WinForms.Control.MousePosition).WorkingArea;
        var scale = VisualTreeHelper.GetDpi(this);

        var left = workingArea.Left / scale.DpiScaleX;
        var top = workingArea.Top / scale.DpiScaleY;
        var width = workingArea.Width / scale.DpiScaleX;
        var height = workingArea.Height / scale.DpiScaleY;

        Left = left + ((width - Width) / 2);
        Top = top + (height * TopOfScreenFraction);
    }

    /// <inheritdoc />
    bool IAppWindow.IsVisible => IsVisible;

    /// <inheritdoc />
    void IAppWindow.Show() => Show();

    /// <inheritdoc />
    void IAppWindow.Hide() => Hide();
}
