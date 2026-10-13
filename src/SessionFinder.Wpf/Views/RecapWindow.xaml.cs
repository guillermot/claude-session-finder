using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SessionFinder.Presentation.Recap;

namespace SessionFinder.Wpf.Views;

/// <summary>
/// The daily recap: titled cards for a day or a week, in Windows 11's settings-card layout, ready
/// to copy into a stand-up.
/// </summary>
/// <remarks>
/// <para>
/// Created when it is asked for and destroyed when it is closed, like the settings window, so what
/// it shows is always the index as it is at the moment it opens. The title bar is the Fluent
/// theme's, which follows the system appearance by itself.
/// </para>
/// <para>
/// The cards are data templates over the view model. The summary arrives as Markdown from Claude
/// and is laid out here through the same parser the macOS head uses.
/// </para>
/// </remarks>
internal partial class RecapWindow : Window
{
    private const double BulletWidth = 14;
    private const double BodySize = 14;

    private readonly RecapViewModel _viewModel;

    /// <summary>
    /// Builds the window and binds it to the recap view model.
    /// </summary>
    /// <param name="viewModel">What the window shows.</param>
    public RecapWindow(RecapViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelChanged;

        RefreshKeyHint();
        RenderSummary();
    }

    /// <summary>
    /// The arrow keys step between days, W switches to the week, Enter copies and Escape closes.
    /// Taken before any control sees them, so a focused button never turns an arrow into focus
    /// navigation of its own.
    /// </summary>
    /// <param name="e">The key.</param>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = e.Key switch
        {
            Key.Left => Run(_viewModel.PreviousDayCommand),
            Key.Right => Run(_viewModel.NextDayCommand),
            Key.W => Run(_viewModel.ToggleWeekCommand),
            Key.Enter => Run(_viewModel.CopyCommand),
            Key.Escape => CloseWindow(),
            _ => false,
        };

        if (!e.Handled)
        {
            base.OnPreviewKeyDown(e);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await _viewModel.LoadAsync(CancellationToken.None);

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
    }

    /// <summary>
    /// Opens a button's menu under it on a left click, which is how a menu button behaves; WPF only
    /// opens a context menu on the right button by itself.
    /// </summary>
    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.DataContext = button.DataContext;
            menu.IsOpen = true;
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private static bool Run(ICommand command)
    {
        if (command.CanExecute(parameter: null))
        {
            command.Execute(parameter: null);
        }

        return true;
    }

    private bool CloseWindow()
    {
        Close();
        return true;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RecapViewModel.SummaryText) or nameof(RecapViewModel.IsShowingSummary):
                RenderSummary();
                break;
            case nameof(RecapViewModel.Status):
                RefreshKeyHint();
                break;
        }
    }

    private void RefreshKeyHint() =>
        KeyHint.Visibility = string.IsNullOrEmpty(_viewModel.Status) ? Visibility.Visible : Visibility.Collapsed;

    private void RenderSummary()
    {
        SummaryDocument.Children.Clear();

        if (_viewModel.SummaryText is not { } summary)
        {
            return;
        }

        var isFirst = true;

        foreach (var line in MarkdownLines.Parse(summary))
        {
            if (line.Role == RecapLineRole.Blank)
            {
                continue;
            }

            SummaryDocument.Children.Add(BuildLine(line, isFirst));
            isFirst = false;
        }
    }

    private UIElement BuildLine(MarkdownLine line, bool isFirst)
    {
        var isHeading = line.Role is RecapLineRole.Heading or RecapLineRole.Section;
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = BodySize,
            FontWeight = isHeading ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = TryFindResource("PrimaryTextBrush") as Brush,
            Margin = new Thickness(0, isHeading && !isFirst ? 12 : 2, 0, 2),
        };

        foreach (var span in line.Spans)
        {
            var run = new Run(span.Text);

            if (span.IsBold)
            {
                run.FontWeight = FontWeights.SemiBold;
            }

            if (span.IsCode && TryFindResource("MonospaceFont") is FontFamily family)
            {
                run.FontFamily = family;
            }

            text.Inlines.Add(run);
        }

        if (!line.IsBullet)
        {
            return text;
        }

        var row = new Grid { Margin = new Thickness(line.Indent * BulletWidth, 0, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var bullet = new TextBlock
        {
            Text = "•",
            Width = BulletWidth,
            FontSize = BodySize,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = TryFindResource("SecondaryTextBrush") as Brush,
        };

        Grid.SetColumn(text, 1);
        row.Children.Add(bullet);
        row.Children.Add(text);

        return row;
    }
}
