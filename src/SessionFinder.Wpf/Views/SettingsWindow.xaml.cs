using System.Windows;
using Microsoft.Extensions.Logging;
using SessionFinder.Presentation.Settings;
using SessionFinder.Wpf.Platform;

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
/// The title bar is the one part of it this application does not draw, so it is asked for in dark
/// rather than restyled. Everything below it comes from the same palette as the search box.
/// </para>
/// </remarks>
internal partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly ILogger<SettingsWindow> _logger;

    /// <summary>
    /// Builds the window and binds it to the settings view model.
    /// </summary>
    /// <param name="viewModel">What the window shows.</param>
    /// <param name="logger">Where a title bar that could not be darkened is recorded.</param>
    public SettingsWindow(SettingsViewModel viewModel, ILogger<SettingsWindow> logger)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(logger);

        _viewModel = viewModel;
        _logger = logger;

        InitializeComponent();

        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;

        viewModel.Saved += OnSaved;
    }

    /// <summary>
    /// Darkens the native chrome at the first moment the window has a handle, which is before it is
    /// painted; asking any later shows a light bar that repaints only when the window is resized.
    /// </summary>
    /// <param name="e">Ignored.</param>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (!ImmersiveDarkTitleBar.TryApply(this))
        {
            DarkTitleBarLog.Unavailable(_logger);
        }
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
