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
/// The daily recap: one working day, laid out as a short document, ready to copy into a stand-up.
/// </summary>
/// <remarks>
/// <para>
/// Created when it is asked for and destroyed when it is closed, like the settings window: it is
/// opened once a day, and what it shows has to be the index as it is at that moment.
/// </para>
/// <para>
/// The view model speaks Markdown, because that is what is copied. The window lays each line out by
/// the role it plays — a section divider, a day, a project, a session or commit — through the same
/// parser the Windows head uses, so the two draw the same recap the same way.
/// </para>
/// <para>
/// The arrow keys step between days. They are taken in the tunnelling phase so that a focused
/// button or a text selection never gets to treat them as navigation of its own.
/// </para>
/// </remarks>
internal sealed partial class RecapWindow : Window
{
    private const double ItemIndent = 16;
    private const double BulletWidth = 14;
    private const double BodySize = 13;
    private const double SmallSize = 12;
    private const double HeadingSize = 15;
    private const double ProjectSize = 14;
    private const double SectionSize = 11;
    private static readonly FontFamily CodeFont = new("Menlo, Monaco, monospace");

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

        Render();
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
        if (e.PropertyName == nameof(RecapViewModel.DisplayBody))
        {
            Render();
        }
    }

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

    private Control BuildLine(MarkdownLine line, bool isFirst) => line.Role switch
    {
        RecapLineRole.Section => BuildSection(line, isFirst),
        RecapLineRole.Heading => Text(line, HeadingSize, FontWeight.SemiBold, top: isFirst ? 0 : 14, bottom: 4),
        RecapLineRole.Project => BuildProject(line, isFirst),
        RecapLineRole.Item => BuildItem(line),
        _ => Text(line, BodySize, FontWeight.Normal, top: 2, bottom: 2, foreground: "SecondaryTextBrush"),
    };

    /// <summary>A small capitalised label over a hairline, which is what separates the earlier days.</summary>
    private StackPanel BuildSection(MarkdownLine line, bool isFirst)
    {
        var label = new TextBlock
        {
            Text = string.Concat(line.Spans.Select(span => span.Text)).ToUpperInvariant(),
            FontSize = SectionSize,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 1.2,
            Foreground = Brush("SecondaryTextBrush"),
            Margin = new Thickness(0, 10, 0, 0),
        };

        return new StackPanel
        {
            Margin = new Thickness(0, isFirst ? 0 : 18, 0, 2),
            Children =
            {
                new Border { Height = 1, Background = Brush("SurfaceEdgeBrush") },
                label,
            },
        };
    }

    /// <summary>
    /// The project name in its own weight, with the branch, session count and time span after it in
    /// the secondary colour: the name is what a listener catches, the rest is for whoever reads.
    /// </summary>
    private SelectableTextBlock BuildProject(MarkdownLine line, bool isFirst)
    {
        var text = NewBlock(top: isFirst ? 0 : 14, bottom: 4);

        for (var index = 0; index < line.Spans.Count; index++)
        {
            var span = line.Spans[index];
            var run = BuildRun(span);

            if (index == 0)
            {
                run.FontSize = ProjectSize;
                run.FontWeight = FontWeight.SemiBold;
            }
            else if (!span.IsCode)
            {
                run.FontSize = SmallSize;
                run.Foreground = Brush("SecondaryTextBrush");
            }

            text.Inlines!.Add(run);
        }

        return text;
    }

    private Grid BuildItem(MarkdownLine line)
    {
        var text = NewBlock(top: 2, bottom: 2);

        foreach (var span in line.Spans)
        {
            text.Inlines!.Add(BuildRun(span));
        }

        var bullet = new TextBlock
        {
            Text = "•",
            Width = BulletWidth,
            FontSize = BodySize,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = Brush("SecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Top,
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(Math.Max(0, line.Indent - 1) * ItemIndent, 0, 0, 0),
        };

        Grid.SetColumn(text, 1);
        row.Children.Add(bullet);
        row.Children.Add(text);

        return row;
    }

    private SelectableTextBlock Text(
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
            text.Inlines!.Add(BuildRun(span));
        }

        return text;
    }

    private SelectableTextBlock NewBlock(double top, double bottom) => new()
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
            run.FontWeight = FontWeight.SemiBold;
        }

        if (span.IsCode)
        {
            run.FontFamily = CodeFont;
            run.FontSize = SmallSize;
            run.Foreground = Brush("AccentTextBrush");
        }

        return run;
    }

    private IBrush? Brush(string key) =>
        this.TryFindResource(key, out var value) ? value as IBrush : null;
}
