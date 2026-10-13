using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SessionFinder.Presentation.Recap;

namespace SessionFinder.Mac.Views;

/// <summary>
/// The daily recap: titled cards for a day or a week, laid out the way System Settings lays out
/// grouped content, ready to copy into a stand-up.
/// </summary>
/// <remarks>
/// <para>
/// Created when it is asked for and destroyed when it is closed, like the settings window: it is
/// opened once a day, and what it shows has to be the index as it is at that moment.
/// </para>
/// <para>
/// The cards are plain data templates over the view model. The one thing drawn here in code is the
/// summary, which arrives as Markdown from Claude and is laid out through the same small parser the
/// Windows head uses.
/// </para>
/// <para>
/// The keys are taken in the tunnelling phase so that a focused button or a text selection never
/// gets to treat them as navigation of its own.
/// </para>
/// </remarks>
internal sealed partial class RecapWindow : Window
{
    private const double BulletWidth = 14;
    private const double BodySize = 13;
    private const double HeadingSize = 13;

    private readonly RecapViewModel _viewModel;

    /// <summary>
    /// Builds the window and binds it to the recap view model.
    /// </summary>
    /// <param name="viewModel">The recap being shown.</param>
    public RecapWindow(RecapViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;

        CloseButton.Click += (_, _) => Close();
        Opened += OnOpened;
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelChanged;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        RenderSummary();
    }

    /// <summary>
    /// Loads the recap once the window is on screen. Not awaited, for the reason the settings
    /// window gives: the window is already showing, and a failure is reported by the guard.
    /// </summary>
    private async void OnOpened(object? sender, EventArgs e) =>
        await _viewModel.LoadAsync(CancellationToken.None).ConfigureAwait(true);

    /// <summary>
    /// The view model outlives the window, so the window stops listening when it closes rather than
    /// being kept alive by it.
    /// </summary>
    private void OnClosed(object? sender, EventArgs e) => _viewModel.PropertyChanged -= OnViewModelChanged;

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None)
        {
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
    }

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
        if (e.PropertyName is nameof(RecapViewModel.SummaryText) or nameof(RecapViewModel.IsShowingSummary))
        {
            RenderSummary();
        }
    }

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

    private Control BuildLine(MarkdownLine line, bool isFirst)
    {
        var text = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = line.Role is RecapLineRole.Heading or RecapLineRole.Section ? HeadingSize : BodySize,
            FontWeight = line.Role is RecapLineRole.Heading or RecapLineRole.Section ? FontWeight.SemiBold : FontWeight.Normal,
            LineHeight = 19,
            Foreground = Brush("PrimaryTextBrush"),
            Margin = new Thickness(0, line.IsHeading && !isFirst ? 12 : 2, 0, 2),
        };

        foreach (var span in line.Spans)
        {
            var run = new Run(span.Text);

            if (span.IsBold)
            {
                run.FontWeight = FontWeight.SemiBold;
            }

            if (span.IsCode && this.TryFindResource("MonospaceFont", out var font) && font is FontFamily family)
            {
                run.FontFamily = family;
            }

            text.Inlines!.Add(run);
        }

        if (!line.IsBullet)
        {
            return text;
        }

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(line.Indent * BulletWidth, 0, 0, 0),
        };

        var bullet = new TextBlock
        {
            Text = "•",
            Width = BulletWidth,
            FontSize = BodySize,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = Brush("SecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Top,
        };

        Grid.SetColumn(text, 1);
        row.Children.Add(bullet);
        row.Children.Add(text);

        return row;
    }

    private IBrush? Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
}
