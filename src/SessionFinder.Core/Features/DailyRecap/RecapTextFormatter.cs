using System.Globalization;
using System.Text;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// How copied recap text is marked up.
/// </summary>
public enum RecapTextFormat
{
    /// <summary>GitHub-flavoured Markdown, for chat tools and documents that render it.</summary>
    Markdown,

    /// <summary>Slack's own markup: single asterisks for bold and bullet characters for lists.</summary>
    Slack,

    /// <summary>No markup at all, for anywhere that shows text as it is.</summary>
    PlainText,
}

/// <summary>
/// How much of the recap the text covers.
/// </summary>
public enum RecapScope
{
    /// <summary>The focus day in full and the earlier days in brief.</summary>
    Day,

    /// <summary>The focus day and the earlier days rolled up by project.</summary>
    Week,
}

/// <summary>
/// Renders a recap as text that can be pasted into a chat or read out at a stand-up.
/// </summary>
/// <remarks>
/// <para>
/// The structure is the same in every format — a heading, then one entry per project, then what
/// it consisted of — and only the markup differs. Slack in particular does not speak Markdown: its
/// bold is a single asterisk and it does not render nested lists, so its levels are drawn with
/// bullet characters instead.
/// </para>
/// <para>
/// Quick questions are folded into a count rather than listed, here as in the window, so the text
/// someone pastes into a stand-up channel leads with the work.
/// </para>
/// <para>
/// Lines always end in a bare line feed, whatever the platform. The text goes to a clipboard and
/// into chat tools, and output that differs between the two heads would differ in tests as well.
/// </para>
/// </remarks>
public static class RecapTextFormatter
{
    private const char LineFeed = '\n';
    private const string DayFormat = "ddd, MMM d";
    private const string TimeFormat = "HH:mm";
    private const string Separator = " · ";
    private const int EarlierTitleLimit = 4;

    /// <summary>
    /// Renders the recap.
    /// </summary>
    /// <param name="recap">The recap.</param>
    /// <param name="format">The markup to use.</param>
    /// <param name="scope">Whether to render the day or the week.</param>
    /// <param name="includePrompts">Whether to list each session's key prompts under it.</param>
    /// <returns>The text.</returns>
    public static string Format(
        DailyRecapResult recap,
        RecapTextFormat format = RecapTextFormat.Markdown,
        RecapScope scope = RecapScope.Day,
        bool includePrompts = false)
    {
        ArgumentNullException.ThrowIfNull(recap);

        var writer = new Writer(Markup.For(format));

        if (recap.Focus is null)
        {
            writer.Line(writer.Markup.Muted("No Claude Code activity found before today."));
            return writer.ToString();
        }

        if (scope == RecapScope.Week)
        {
            WriteWeek(writer, recap);
        }
        else
        {
            WriteDay(writer, recap, includePrompts);
        }

        return writer.ToString();
    }

    /// <summary>
    /// Names a day the way a person would at a stand-up: today and yesterday by those words, and any
    /// other day by its date.
    /// </summary>
    /// <param name="day">The day to name.</param>
    /// <param name="today">The working day it is now.</param>
    /// <returns>The name.</returns>
    public static string DescribeDay(DateOnly day, DateOnly today)
    {
        var date = FormatDay(day);

        if (day == today)
        {
            return $"Today so far ({date})";
        }

        return day == today.AddDays(-1) ? $"Yesterday ({date})" : date;
    }

    /// <summary>
    /// Writes a day as its short weekday and date, such as <c>Fri, Oct 2</c>.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The short form.</returns>
    public static string FormatDay(DateOnly day) => day.ToString(DayFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Names the span a week roll-up covers, such as <c>Week of Fri, Oct 2 – Wed, Oct 7</c>.
    /// </summary>
    /// <param name="recap">The recap.</param>
    /// <returns>The title, or <see langword="null"/> when there is no activity to roll up.</returns>
    public static string? DescribeWeek(DailyRecapResult recap)
    {
        var days = RecapWeek.Days(recap);

        if (days.Count == 0)
        {
            return null;
        }

        var first = days[0].Day;
        var last = days[^1].Day;

        return first == last ? $"Week of {FormatDay(first)}" : $"Week of {FormatDay(first)} – {FormatDay(last)}";
    }

    /// <summary>
    /// Writes a time span such as <c>10:05–17:40</c>, collapsed to one time when it starts and ends
    /// in the same minute.
    /// </summary>
    /// <param name="first">The first activity.</param>
    /// <param name="last">The last activity.</param>
    /// <returns>The span.</returns>
    public static string FormatSpan(DateTimeOffset first, DateTimeOffset last)
    {
        var start = first.ToString(TimeFormat, CultureInfo.InvariantCulture);
        var end = last.ToString(TimeFormat, CultureInfo.InvariantCulture);

        return start == end ? start : $"{start}–{end}";
    }

    /// <summary>
    /// Counts something in words, such as <c>1 session</c> or <c>3 commits</c>.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <param name="noun">The singular noun.</param>
    /// <returns>The phrase.</returns>
    public static string Count(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");

    private static void WriteDay(Writer writer, DailyRecapResult recap, bool includePrompts)
    {
        var focus = recap.Focus!;

        writer.Line(writer.Markup.Bold(DescribeDay(focus.Day, recap.Today)));

        if (!focus.HasActivity)
        {
            writer.Line(writer.Markup.Muted("No Claude Code activity on this day."));
        }

        foreach (var project in focus.Projects)
        {
            WriteProject(writer, project, includePrompts);
        }

        if (recap.Earlier.Count == 0)
        {
            return;
        }

        writer.Line(string.Empty);
        writer.Line(writer.Markup.Bold("Earlier"));

        foreach (var day in recap.Earlier)
        {
            writer.Item(0, writer.Markup.Bold(DescribeDay(day.Day, recap.Today)));

            foreach (var project in day.Projects)
            {
                writer.Item(1, $"{project.Name}: {DescribeBriefly(project.Sessions, project.Commits.Count)}");
            }
        }
    }

    private static void WriteProject(Writer writer, ProjectRecap project, bool includePrompts)
    {
        var markup = writer.Markup;
        var parts = new List<string> { markup.Bold(project.Name) };

        if (project.Branches.Count > 0)
        {
            parts.Add(string.Join(", ", project.Branches.Select(markup.Code)));
        }

        parts.Add(Count(project.Sessions.Count, "session"));
        parts.Add(FormatSpan(project.FirstActivity, project.LastActivity));

        writer.Item(0, string.Join(Separator, parts));

        foreach (var session in project.Sessions.Where(session => !session.IsQuick))
        {
            writer.Item(1, $"{session.Title} ({Count(session.PromptCount, "prompt")})");

            if (!includePrompts)
            {
                continue;
            }

            foreach (var prompt in session.KeyPrompts)
            {
                writer.Item(2, $"\"{prompt}\"");
            }
        }

        WriteQuickQuestions(writer, project.Sessions);

        if (project.Commits.Count == 0)
        {
            return;
        }

        writer.Item(1, "Commits:");

        foreach (var commit in project.Commits)
        {
            writer.Item(2, $"{markup.Code(commit.Sha)} {commit.Subject}");
        }
    }

    private static void WriteWeek(Writer writer, DailyRecapResult recap)
    {
        var markup = writer.Markup;

        if (DescribeWeek(recap) is not { } title)
        {
            writer.Line(markup.Muted("No Claude Code activity this week."));
            return;
        }

        writer.Line(markup.Bold(title));

        foreach (var project in RecapWeek.RollUp(recap))
        {
            var parts = new List<string> { markup.Bold(project.Name) };

            if (project.Branches.Count > 0)
            {
                parts.Add(string.Join(", ", project.Branches.Select(markup.Code)));
            }

            parts.Add($"{Count(project.Sessions.Count, "session")} over {Count(project.ActiveDays, "day")}");

            if (project.Commits.Count > 0)
            {
                parts.Add(Count(project.Commits.Count, "commit"));
            }

            writer.Item(0, string.Join(Separator, parts));

            foreach (var day in project.Sessions.Where(entry => !entry.Session.IsQuick).GroupBy(entry => entry.Day))
            {
                var titles = string.Join("; ", day.Select(entry => entry.Session.Title).Distinct(StringComparer.Ordinal));

                writer.Item(1, $"{FormatDay(day.Key)}: {titles}");
            }

            WriteQuickQuestions(writer, [.. project.Sessions.Select(entry => entry.Session)]);

            if (project.Commits.Count == 0)
            {
                continue;
            }

            writer.Item(1, "Commits:");

            foreach (var entry in project.Commits)
            {
                writer.Item(2, $"{markup.Code(entry.Commit.Sha)} {entry.Commit.Subject}");
            }
        }
    }

    private static void WriteQuickQuestions(Writer writer, IReadOnlyList<SessionRecap> sessions)
    {
        var quick = sessions.Count(session => session.IsQuick);

        if (quick == 0)
        {
            return;
        }

        var phrase = Count(quick, "quick question");
        var text = quick == sessions.Count ? phrase : $"plus {phrase}";

        writer.Item(1, writer.Markup.Muted(text));
    }

    /// <summary>
    /// One line for a project on an earlier day: its work by title, its quick questions as a count
    /// and its commits as a count.
    /// </summary>
    private static string DescribeBriefly(IReadOnlyList<SessionRecap> sessions, int commitCount)
    {
        var titles = sessions
            .Where(session => !session.IsQuick)
            .Select(session => session.Title)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var parts = new List<string>();

        if (titles.Count > 0)
        {
            var shown = string.Join("; ", titles.Take(EarlierTitleLimit));

            parts.Add(titles.Count > EarlierTitleLimit ? $"{shown}; +{titles.Count - EarlierTitleLimit} more" : shown);
        }

        var quick = sessions.Count(session => session.IsQuick);

        if (quick > 0)
        {
            parts.Add(Count(quick, "quick question"));
        }

        if (commitCount > 0)
        {
            parts.Add(Count(commitCount, "commit"));
        }

        return string.Join(Separator, parts);
    }

    /// <summary>The marks one format uses for emphasis, code, asides and list levels.</summary>
    private sealed record Markup(
        Func<string, string> Bold,
        Func<string, string> Code,
        Func<string, string> Muted,
        Func<int, string> Bullet)
    {
        private static readonly string[] SlackBullets = ["• ", "    ◦ ", "        ▪ "];

        public static Markup For(RecapTextFormat format) => format switch
        {
            RecapTextFormat.Slack => new Markup(
                text => $"*{text}*",
                text => $"`{text}`",
                text => $"_{text}_",
                level => SlackBullets[Math.Min(level, SlackBullets.Length - 1)]),
            RecapTextFormat.PlainText => new Markup(
                text => text,
                text => text,
                text => text,
                level => new string(' ', level * 2) + "- "),
            _ => new Markup(
                text => $"**{text}**",
                text => $"`{text}`",
                text => $"_{text}_",
                level => new string(' ', level * 2) + "- "),
        };
    }

    private sealed class Writer(Markup markup)
    {
        private readonly StringBuilder _builder = new();

        public Markup Markup { get; } = markup;

        public void Line(string text) => _builder.Append(text).Append(LineFeed);

        public void Item(int level, string text) => Line(Markup.Bullet(level) + text);

        public override string ToString() => _builder.ToString();
    }
}
