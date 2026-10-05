using System.Globalization;
using System.Text;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Writes the instructions and the material a model is given to turn a recap into stand-up
/// bullets.
/// </summary>
/// <remarks>
/// The material is the structured recap plus what it leaves out on screen: each session's key
/// prompts and the assistant's last word on it. Those are what let a summary say what was done
/// rather than repeat the session titles back.
/// </remarks>
public static class RecapSummaryPrompt
{
    private const char LineFeed = '\n';
    private const string TimeFormat = "HH:mm";

    private const string Instructions =
        """
        You are helping a software engineer prepare what to say at their daily scrum stand-up.
        Below is a log of their Claude Code sessions and their own git commits. Write what they can
        say, in the first person, as short Markdown bullets under these headings:

        **{0}** — 2 to 5 bullets about the focus day. Lead with outcomes, merge related sessions,
        and mention what was shipped or committed.
        **Before that** — at most 3 bullets, only for notable work on the earlier days. Omit the
        heading if there is nothing notable.
        **Next** — 1 to 3 bullets on the threads that look unfinished, phrased as likely next steps.
        **Blockers** — only if the log clearly shows one. Otherwise omit the heading.

        Stay under 150 words. No preamble and no closing remarks. Do not invent work that is not in
        the log, and do not mention Claude, prompts or sessions: describe the work itself.
        """;

    /// <summary>
    /// Builds the prompt for a recap.
    /// </summary>
    /// <param name="recap">A recap with a focus day.</param>
    /// <returns>The full prompt text.</returns>
    public static string Build(DailyRecapResult recap)
    {
        ArgumentNullException.ThrowIfNull(recap);

        var builder = new StringBuilder();
        var focusHeading = recap.Focus is { } focus ? HeadingFor(focus.Day, recap.Today) : "Yesterday";

        builder.Append(string.Format(CultureInfo.InvariantCulture, Instructions, focusHeading)).Append(LineFeed);
        Line(builder, "<activity>");

        if (recap.Focus is { } day)
        {
            Line(builder, $"Focus day: {RecapMarkdownFormatter.DescribeDay(day.Day, recap.Today)}");
            AppendFocusDay(builder, day);
        }

        foreach (var earlier in recap.Earlier)
        {
            Line(builder, string.Empty);
            Line(builder, $"Earlier day: {RecapMarkdownFormatter.FormatDay(earlier.Day)}");
            AppendEarlierDay(builder, earlier);
        }

        Line(builder, "</activity>");

        return builder.ToString();
    }

    /// <summary>
    /// "Yesterday" is only right when it was. On a Monday the focus day is Friday, and a stand-up
    /// that says "yesterday" about it is wrong in a way everyone notices.
    /// </summary>
    private static string HeadingFor(DateOnly day, DateOnly today)
    {
        if (day == today)
        {
            return "Today so far";
        }

        return day == today.AddDays(-1)
            ? "Yesterday"
            : "On " + day.ToString("dddd", CultureInfo.InvariantCulture);
    }

    private static void AppendFocusDay(StringBuilder builder, DayRecap day)
    {
        foreach (var project in day.Projects)
        {
            var branches = project.Branches.Count > 0 ? $" (branches: {string.Join(", ", project.Branches)})" : string.Empty;

            Line(builder, $"Project {project.Name}{branches}");

            foreach (var session in project.Sessions)
            {
                Line(builder, $"- Session \"{session.Title}\", {session.PromptCount} prompt(s), {Time(session.FirstActivity)}–{Time(session.LastActivity)}");

                foreach (var prompt in session.KeyPrompts)
                {
                    Line(builder, $"  - Asked: {prompt}");
                }

                if (session.Outcome is { } outcome)
                {
                    Line(builder, $"  - Assistant's last reply: {outcome}");
                }
            }

            foreach (var commit in project.Commits)
            {
                Line(builder, $"- Commit {commit.Sha}: {commit.Subject}");
            }
        }
    }

    private static void AppendEarlierDay(StringBuilder builder, DayRecap day)
    {
        foreach (var project in day.Projects)
        {
            var titles = string.Join("; ", project.Sessions.Select(session => session.Title).Distinct(StringComparer.Ordinal));

            Line(builder, $"- Project {project.Name}: {titles}");

            foreach (var commit in project.Commits)
            {
                Line(builder, $"  - Commit {commit.Sha}: {commit.Subject}");
            }
        }
    }

    private static string Time(DateTimeOffset value) => value.ToString(TimeFormat, CultureInfo.InvariantCulture);

    private static void Line(StringBuilder builder, string text) => builder.Append(text).Append(LineFeed);
}
