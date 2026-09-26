using Avalonia.Controls;
using SessionFinder.Presentation.Settings;

namespace SessionFinder.Mac.Views;

/// <summary>
/// The settings form.
/// </summary>
/// <remarks>
/// The opposite lifecycle to the search box: this one is created when it is asked for and destroyed
/// when it is closed. It is opened rarely, and the values it shows have to be the ones on disk at
/// the moment it opens rather than the ones that were there when the process started.
/// </remarks>
internal sealed partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    /// <summary>
    /// Builds the window and binds it to the settings view model.
    /// </summary>
    /// <param name="viewModel">The settings being edited.</param>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;

        CloseButton.Click += (_, _) => Close();
        viewModel.Saved += OnSaved;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    /// <summary>
    /// Loads the settings from disk once the window is on screen. Deliberately not awaited: the
    /// window is already shown, the load is short, and any failure it raises is caught by the
    /// global handlers rather than by a caller that has nothing to do with it.
    /// </summary>
    private async void OnOpened(object? sender, EventArgs e) =>
        await _viewModel.LoadAsync(CancellationToken.None).ConfigureAwait(true);

    private void OnSaved(object? sender, EventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e) => _viewModel.Saved -= OnSaved;
}
