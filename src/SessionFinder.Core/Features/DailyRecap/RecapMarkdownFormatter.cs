using System.Globalization;
using System.Text;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Renders a recap as Markdown that can be pasted into a chat or read out at a stand-up.
/// </summary>
/// <remarks>
/// <para>
/// The focus day is shown in full — project, branch, sessions, commits — and every earlier day is
/// condensed to one line per project, because that is the proportion a stand-up gives them.
/// </para>
/// <para>
/// Lines always end in a bare line feed, whatever the platform. The text goes to a clipboard and
/// into chat tools, and output that differs between the two heads would differ in tests as well.
/// </para>
/// </remarks>
public static class RecapMarkdownFormatter
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
    /// <param name="includePrompts">Whether to list each session's key prompts under it.</param>
    /// <returns>The Markdown text.</returns>
    public static string Format(DailyRecapResult recap, bool includePrompts = false)
    {
        ArgumentNullException.ThrowIfNull(recap);

        var builder = new StringBuilder();

        if (recap.Focus is not { } focus)
        {
            Line(builder, "_No Claude Code activity found before today._");
            return builder.ToString();
        }

        Line(builder, $"**{DescribeDay(focus.Day, recap.Today)}**");

        if (!focus.HasActivity)
        {
            Line(builder, "_No Claude Code activity on this day._");
        }

        foreach (var project in focus.Projects)
        {
            AppendProject(builder, project, includePrompts);
        }

        if (recap.Earlier.Count > 0)
        {
            Line(builder, string.Empty);
            Line(builder, "**Earlier**");

            foreach (var day in recap.Earlier)
            {
                AppendEarlierDay(builder, day, recap.Today);
            }
        }

        return builder.ToString();
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

    private static void AppendProject(StringBuilder builder, ProjectRecap project, bool includePrompts)
    {
        var parts = new List<string> { $"**{project.Name}**" };

        if (project.Branches.Count > 0)
        {
            parts.Add(string.Join(", ", project.Branches.Select(branch => $"`{branch}`")));
        }

        parts.Add(Count(project.Sessions.Count, "session"));
        parts.Add(FormatSpan(project.FirstActivity, project.LastActivity));

        Line(builder, "- " + string.Join(Separator, parts));

        foreach (var session in project.Sessions)
        {
            Line(builder, $"  - {session.Title} ({Count(session.PromptCount, "prompt")})");

            if (!includePrompts)
            {
                continue;
            }

            foreach (var prompt in session.KeyPrompts)
            {
                Line(builder, $"    - \"{prompt}\"");
            }
        }

        if (project.Commits.Count == 0)
        {
            return;
        }

        Line(builder, "  - Commits:");

        foreach (var commit in project.Commits)
        {
            Line(builder, $"    - `{commit.Sha}` {commit.Subject}");
        }
    }

    private static void AppendEarlierDay(StringBuilder builder, DayRecap day, DateOnly today)
    {
        Line(builder, $"- **{DescribeDay(day.Day, today)}**");

        foreach (var project in day.Projects)
        {
            var titles = project.Sessions.Select(session => session.Title).Distinct(StringComparer.Ordinal).ToList();
            var shown = string.Join("; ", titles.Take(EarlierTitleLimit));

            if (titles.Count > EarlierTitleLimit)
            {
                shown += $"; +{titles.Count - EarlierTitleLimit} more";
            }

            var commits = project.Commits.Count > 0 ? Separator + Count(project.Commits.Count, "commit") : string.Empty;

            Line(builder, $"  - {project.Name}: {shown}{commits}");
        }
    }

    private static string FormatSpan(DateTimeOffset first, DateTimeOffset last)
    {
        var start = first.ToString(TimeFormat, CultureInfo.InvariantCulture);
        var end = last.ToString(TimeFormat, CultureInfo.InvariantCulture);

        return start == end ? start : $"{start}–{end}";
    }

    private static string Count(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");

    private static void Line(StringBuilder builder, string text) => builder.Append(text).Append(LineFeed);
}
