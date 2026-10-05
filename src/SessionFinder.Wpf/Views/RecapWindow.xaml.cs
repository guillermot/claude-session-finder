using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using SessionFinder.Presentation.Recap;
using SessionFinder.Wpf.Platform;

namespace SessionFinder.Wpf.Views;

/// <summary>
/// The daily recap: one working day as Markdown, ready to copy into a stand-up.
/// </summary>
/// <remarks>
/// Created when it is asked for and destroyed when it is closed, like the settings window, so what
/// it shows is always the index as it is at the moment it opens.
/// </remarks>
internal partial class RecapWindow : Window
{
    private const double ItemIndent = 16;
    private const double BulletWidth = 14;
    private const double BodySize = 13;
    private const double SmallSize = 12;
    private const double HeadingSize = 15;
    private const double ProjectSize = 14;
    private const double SectionSize = 11;
    private static readonly FontFamily CodeFont = new("Cascadia Mono, Consolas");

    private readonly RecapViewModel _viewModel;
    private readonly ILogger<RecapWindow> _logger;

    /// <summary>
    /// Builds the window and binds it to the recap view model.
    /// </summary>
    /// <param name="viewModel">What the window shows.</param>
    /// <param name="logger">Where a title bar that could not be darkened is recorded.</param>
    public RecapWindow(RecapViewModel viewModel, ILogger<RecapWindow> logger)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(logger);

        _viewModel = viewModel;
        _logger = logger;

        InitializeComponent();

        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelChanged;

        Render();
    }

    /// <summary>Darkens the native chrome before the first paint, as the settings window does.</summary>
    /// <param name="e">Ignored.</param>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (!ImmersiveDarkTitleBar.TryApply(this))
        {
            DarkTitleBarLog.Unavailable(_logger);
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
    /// The arrow keys step between days, Enter copies and Escape closes. Taken before any control
    /// sees them, so a focused button never turns an arrow into focus navigation of its own.
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
            Key.Enter => Run(_viewModel.CopyCommand),
            Key.Escape => CloseWindow(),
            _ => false,
        };

        if (!e.Handled)
        {
            base.OnPreviewKeyDown(e);
        }
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
        if (e.PropertyName == nameof(RecapViewModel.DisplayBody))
        {
            Render();
        }
    }

    /// <summary>
    /// Lays the recap out by the role each line plays, through the parser the macOS head uses as
    /// well. The Markdown itself is what the copy command puts on the clipboard.
    /// </summary>
    private void Render()
    {
        Document.Children.Clear();

        var isFirst = true;

        foreach (var line in MarkdownLines.Parse(_viewModel.DisplayBody))
        {
            if (line.Role == RecapLineRole.Blank)
            {
                continue;
            }

            Document.Children.Add(BuildLine(line, isFirst));
            isFirst = false;
        }
    }

    private UIElement BuildLine(MarkdownLine line, bool isFirst) => line.Role switch
    {
        RecapLineRole.Section => BuildSection(line, isFirst),
        RecapLineRole.Heading => Text(line, HeadingSize, FontWeights.SemiBold, top: isFirst ? 0 : 14, bottom: 4),
        RecapLineRole.Project => BuildProject(line, isFirst),
        RecapLineRole.Item => BuildItem(line),
        _ => Text(line, BodySize, FontWeights.Normal, top: 2, bottom: 2, foreground: "SecondaryTextBrush"),
    };

    private StackPanel BuildSection(MarkdownLine line, bool isFirst)
    {
        var panel = new StackPanel { Margin = new Thickness(0, isFirst ? 0 : 18, 0, 2) };

        panel.Children.Add(new Border { Height = 1, Background = Brush("SurfaceEdgeBrush") });
        panel.Children.Add(new TextBlock
        {
            Text = string.Concat(line.Spans.Select(span => span.Text)).ToUpperInvariant(),
            FontSize = SectionSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("SecondaryTextBrush"),
            Margin = new Thickness(0, 10, 0, 0),
        });

        return panel;
    }

    private TextBlock BuildProject(MarkdownLine line, bool isFirst)
    {
        var text = NewBlock(top: isFirst ? 0 : 14, bottom: 4);

        for (var index = 0; index < line.Spans.Count; index++)
        {
            var span = line.Spans[index];
            var run = BuildRun(span);

            if (index == 0)
            {
                run.FontSize = ProjectSize;
                run.FontWeight = FontWeights.SemiBold;
            }
            else if (!span.IsCode)
            {
                run.FontSize = SmallSize;
                run.Foreground = Brush("SecondaryTextBrush");
            }

            text.Inlines.Add(run);
        }

        return text;
    }

    private Grid BuildItem(MarkdownLine line)
    {
        var text = NewBlock(top: 2, bottom: 2);

        foreach (var span in line.Spans)
        {
            text.Inlines.Add(BuildRun(span));
        }

        var bullet = new TextBlock
        {
            Text = "•",
            Width = BulletWidth,
            FontSize = BodySize,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = Brush("SecondaryTextBrush"),
        };

        var row = new Grid { Margin = new Thickness(Math.Max(0, line.Indent - 1) * ItemIndent, 0, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(text, 1);
        row.Children.Add(bullet);
        row.Children.Add(text);

        return row;
    }

    private TextBlock Text(
        MarkdownLine line,
        double size,
        FontWeight weight,
        double top,
        double bottom,
        string foreground = "PrimaryTextBrush")
    {
        var text = NewBlock(top, bottom);
        text.FontSize = size;
        text.FontWeight = weight;
        text.Foreground = Brush(foreground);

        foreach (var span in line.Spans)
        {
            text.Inlines.Add(BuildRun(span));
        }

        return text;
    }

    private TextBlock NewBlock(double top, double bottom) => new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = BodySize,
        LineHeight = 20,
        Foreground = Brush("PrimaryTextBrush"),
        Margin = new Thickness(0, top, 0, bottom),
    };

    private Run BuildRun(MarkdownSpan span)
    {
        var run = new Run(span.Text);

        if (span.IsBold)
        {
            run.FontWeight = FontWeights.SemiBold;
        }

        if (span.IsCode)
        {
            run.FontFamily = CodeFont;
            run.FontSize = SmallSize;
            run.Foreground = Brush("AccentTextBrush");
        }

        return run;
    }

    private Brush? Brush(string key) => TryFindResource(key) as Brush;

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
