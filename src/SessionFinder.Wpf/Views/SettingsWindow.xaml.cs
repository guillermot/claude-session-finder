using System.Windows;
using SessionFinder.Presentation.Settings;

namespace SessionFinder.Wpf.Views;

/// <summary>
/// The settings form: an ordinary window with a title bar, unlike the search box.
/// </summary>
/// <remarks>
/// <para>
/// It is created when it is asked for and destroyed when it is closed, which is the opposite of the
/// search box. The search box is opened dozens of times a day and its cost is the time between the
/// chord and the caret; this is opened rarely and its cost is nothing anybody notices, so keeping
/// it alive would be keeping a form with stale values in it.
/// </para>
/// <para>
/// The form is filled when the window loads rather than in the constructor, so that a value changed
/// in the file by hand since the last time is shown rather than overwritten.
/// </para>
/// <para>
/// The title bar and the controls are the Fluent theme's, which follow the system appearance by
/// themselves; the cards take their colours from the same palette as the search box.
/// </para>
/// </remarks>
internal partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    /// <summary>
    /// Builds the window and binds it to the settings view model.
    /// </summary>
    /// <param name="viewModel">What the window shows.</param>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;

        viewModel.Saved += OnSaved;
    }

    /// <summary>
    /// Fills the form. Written as an event handler, which is the one place an asynchronous void is
    /// the right shape; anything it throws is caught by the process-wide handlers.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await _viewModel.LoadAsync(CancellationToken.None);

    private void OnSaved(object? sender, EventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Saved -= OnSaved;

        Loaded -= OnLoaded;
        Closed -= OnClosed;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
