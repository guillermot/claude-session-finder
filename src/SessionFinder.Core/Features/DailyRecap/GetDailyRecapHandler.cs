using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Turns the index, and optionally git, into an account of a working day.
/// </summary>
/// <remarks>
/// <para>
/// A day counts as active only when the person typed something in it. A session that was merely
/// resumed, or that only received harness envelopes, did not do any work worth reporting, and
/// letting it count would make "the last active day" a day the person does not remember.
/// </para>
/// <para>
/// The search for active days widens in steps rather than reading the whole history at once. The
/// common case — yesterday, or Friday — is answered from the last two weeks; only someone back
/// from a long break pays for a wider read, and the widest one is still bounded.
/// </para>
/// <para>
/// Assistant text is read for the focus day alone. It is most of the bytes in a transcript, the
/// structured recap never shows it, and the summary only describes the focus day in detail.
/// </para>
/// </remarks>
public sealed class GetDailyRecapHandler(
    ISessionActivityReader activityReader,
    IGitActivityReader gitReader,
    IOptionsMonitor<RecapOptions> options,
    TimeProvider timeProvider,
    ILogger<GetDailyRecapHandler> logger) : IGetDailyRecapHandler
{
    private const int KeyPromptCount = 3;
    private const int KeyPromptLength = 160;
    private const int OutcomeLength = 400;
    private const int SmallestUsefulLookback = 1;
    private const string MessageSeparator = "\n";

    private static readonly int[] ScanWindowDays = [14, 60, 365, 3650];

    /// <inheritdoc />
    public async Task<DailyRecapResult> HandleAsync(GetDailyRecapQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = options.CurrentValue;
        var calendar = new RecapCalendar(timeProvider.LocalTimeZone, settings.DayStartHour);
        var today = calendar.DayOf(timeProvider.GetUtcNow());
        var lookback = Math.Max(0, query.LookbackDays ?? settings.LookbackDays);
        var includeGit = query.IncludeGit ?? settings.IncludeGit;

        var scan = await ScanAsync(calendar, today, query.Day, lookback, cancellationToken).ConfigureAwait(false);

        if (scan.Focus is not { } focus)
        {
            DailyRecapLog.NothingToRecap(logger, today);

            return new DailyRecapResult { Today = today };
        }

        var earlierDays = scan.Days.Keys.Where(day => day < focus).OrderDescending().ToList();

        var days = new List<(DateOnly Day, IReadOnlyList<SessionActivity> Sessions)>
        {
            (focus, await ReadFocusDayAsync(calendar, focus, scan, cancellationToken).ConfigureAwait(false)),
        };

        days.AddRange(earlierDays.Take(lookback).Select(day => (day, (IReadOnlyList<SessionActivity>)scan.Days[day])));

        var directory = await ProjectDirectory
            .BuildAsync(gitReader, calendar, days, includeGit, cancellationToken)
            .ConfigureAwait(false);

        var recaps = days.Select(entry => BuildDay(entry.Day, entry.Sessions, calendar, directory)).ToList();

        var result = new DailyRecapResult
        {
            Today = today,
            Focus = recaps[0],
            Earlier = recaps[1..],
            PreviousActiveDay = earlierDays.Count > 0 ? earlierDays[0] : null,
            NextActiveDay = await FindNextActiveDayAsync(calendar, focus, today, cancellationToken).ConfigureAwait(false),
        };

        LogBuilt(result, focus);

        return result;
    }

    /// <summary>
    /// Finds the focus day and enough active days before it, reading a wider window only when the
    /// narrower one did not hold them.
    /// </summary>
    private async Task<ActivityScan> ScanAsync(
        RecapCalendar calendar,
        DateOnly today,
        DateOnly? requested,
        int lookback,
        CancellationToken cancellationToken)
    {
        var endDay = requested is { } day ? day.AddDays(1) : today;
        var end = calendar.StartOf(endDay);
        var wanted = Math.Max(SmallestUsefulLookback, lookback);

        var days = new Dictionary<DateOnly, List<SessionActivity>>();
        var focus = requested;

        foreach (var windowDays in ScanWindowDays)
        {
            var from = calendar.StartOf(endDay.AddDays(-windowDays));
            var sessions = await activityReader
                .GetActivityAsync(from, end, includeAssistantText: false, cancellationToken)
                .ConfigureAwait(false);

            days = SplitByDay(sessions, calendar);
            focus = requested ?? (days.Count == 0 ? null : days.Keys.Max());

            if (focus is { } found && days.Keys.Count(candidate => candidate < found) >= wanted)
            {
                break;
            }
        }

        return new ActivityScan(focus, days);
    }

    private async Task<IReadOnlyList<SessionActivity>> ReadFocusDayAsync(
        RecapCalendar calendar,
        DateOnly focus,
        ActivityScan scan,
        CancellationToken cancellationToken)
    {
        if (!scan.Days.ContainsKey(focus))
        {
            return [];
        }

        var sessions = await activityReader
            .GetActivityAsync(
                calendar.StartOf(focus),
                calendar.StartOf(focus.AddDays(1)),
                includeAssistantText: true,
                cancellationToken)
            .ConfigureAwait(false);

        return SplitByDay(sessions, calendar).TryGetValue(focus, out var found) ? found : [];
    }

    private async Task<DateOnly?> FindNextActiveDayAsync(
        RecapCalendar calendar,
        DateOnly focus,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (focus >= today)
        {
            return null;
        }

        var sessions = await activityReader
            .GetActivityAsync(
                calendar.StartOf(focus.AddDays(1)),
                calendar.StartOf(today.AddDays(1)),
                includeAssistantText: false,
                cancellationToken)
            .ConfigureAwait(false);

        var days = SplitByDay(sessions, calendar);

        return days.Count == 0 ? null : days.Keys.Min();
    }

    /// <summary>
    /// Cuts each session's messages at day boundaries, keeping only the days on which the person
    /// actually typed something.
    /// </summary>
    private static Dictionary<DateOnly, List<SessionActivity>> SplitByDay(
        IReadOnlyList<SessionActivity> sessions,
        RecapCalendar calendar)
    {
        var days = new Dictionary<DateOnly, List<SessionActivity>>();

        foreach (var session in sessions)
        {
            var byDay = session.Chunks
                .Where(chunk => chunk.Timestamp is not null)
                .GroupBy(chunk => calendar.DayOf(chunk.Timestamp!.Value));

            foreach (var group in byDay)
            {
                var chunks = group.ToList();

                if (!Merge(chunks).Any(IsHumanPrompt))
                {
                    continue;
                }

                if (!days.TryGetValue(group.Key, out var list))
                {
                    list = [];
                    days[group.Key] = list;
                }

                list.Add(session with { Chunks = chunks });
            }
        }

        return days;
    }

    private static DayRecap BuildDay(
        DateOnly day,
        IReadOnlyList<SessionActivity> sessions,
        RecapCalendar calendar,
        ProjectDirectory directory)
    {
        var projects = sessions
            .Select(session => (session.Folder, Recap: BuildSession(session, calendar)))
            .Where(entry => entry.Recap is not null)
            .GroupBy(entry => directory.KeyOf(entry.Folder))
            .Select(group => BuildProject(
                day,
                group.First().Folder,
                [.. group.Select(entry => entry.Recap!).OrderBy(recap => recap.FirstActivity)],
                directory))
            .OrderByDescending(project => project.PromptCount)
            .ThenByDescending(project => project.LastActivity)
            .ToList();

        return new DayRecap(day, projects);
    }

    private static ProjectRecap BuildProject(
        DateOnly day,
        WorkingFolder folder,
        IReadOnlyList<SessionRecap> sessions,
        ProjectDirectory directory)
    {
        var path = directory.PathOf(folder);

        return new ProjectRecap
        {
            Name = ProjectDirectory.NameOf(path),
            Path = path,
            Branches = [.. sessions.Select(session => session.Branch).OfType<string>().Distinct(StringComparer.Ordinal)],
            Sessions = sessions,
            Commits = directory.CommitsOn(folder, day),
            FirstActivity = sessions.Min(session => session.FirstActivity),
            LastActivity = sessions.Max(session => session.LastActivity),
        };
    }

    private static SessionRecap? BuildSession(SessionActivity session, RecapCalendar calendar)
    {
        var messages = Merge(session.Chunks);
        var prompts = messages
            .Where(message => message.Kind == ChunkKind.UserPrompt)
            .Select(message => (message.Timestamp, Text: PromptText.Clean(message.Text)))
            .Where(prompt => prompt.Text is not null)
            .Select(prompt => (prompt.Timestamp, Text: prompt.Text!))
            .ToList();

        if (prompts.Count == 0)
        {
            return null;
        }

        return new SessionRecap
        {
            SessionId = session.SessionId,
            Title = session.Title.Text,
            Branch = ProjectDirectory.RealBranch(session.GitBranch),
            PromptCount = prompts.Count,
            FirstActivity = calendar.ToLocal(messages.Min(message => message.Timestamp)),
            LastActivity = calendar.ToLocal(messages.Max(message => message.Timestamp)),
            KeyPrompts = SelectKeyPrompts(prompts),
            Outcome = DescribeOutcome(messages),
        };
    }

    /// <summary>
    /// Picks the longest distinct prompts and puts them back in the order they were typed, which is
    /// the order the story of the session is told in.
    /// </summary>
    private static List<string> SelectKeyPrompts(List<(DateTimeOffset Timestamp, string Text)> prompts) =>
    [
        .. prompts
            .DistinctBy(prompt => prompt.Text, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(prompt => prompt.Text.Length)
            .Take(KeyPromptCount)
            .OrderBy(prompt => prompt.Timestamp)
            .Select(prompt => PromptText.Truncate(prompt.Text, KeyPromptLength)),
    ];

    private static string? DescribeOutcome(List<Message> messages)
    {
        var last = messages.LastOrDefault(message => message.Kind == ChunkKind.AssistantText);

        if (last is null)
        {
            return null;
        }

        var text = PromptText.Collapse(last.Text);

        return text.Length == 0 ? null : PromptText.Truncate(text, OutcomeLength);
    }

    /// <summary>
    /// Joins the chunks a long message was split into back into one message. The pieces of one
    /// message are consecutive and share its kind and timestamp; so do several text blocks of one
    /// turn, which belong together just as much.
    /// </summary>
    private static List<Message> Merge(IEnumerable<SearchChunk> chunks)
    {
        var messages = new List<Message>();

        foreach (var chunk in chunks)
        {
            if (chunk.Timestamp is not { } timestamp)
            {
                continue;
            }

            if (messages.Count > 0 && messages[^1] is var last && last.Kind == chunk.Kind && last.Timestamp == timestamp)
            {
                messages[^1] = last with { Text = last.Text + MessageSeparator + chunk.Text };
                continue;
            }

            messages.Add(new Message(chunk.Kind, chunk.Text, timestamp));
        }

        return messages;
    }

    private static bool IsHumanPrompt(Message message) =>
        message.Kind == ChunkKind.UserPrompt && PromptText.Clean(message.Text) is not null;

    private void LogBuilt(DailyRecapResult result, DateOnly focus)
    {
        var projects = result.Focus?.Projects ?? [];

        DailyRecapLog.RecapBuilt(
            logger,
            focus,
            projects.Count,
            projects.Sum(project => project.Sessions.Count),
            projects.Sum(project => project.Commits.Count),
            result.Earlier.Count);
    }

    private sealed record Message(ChunkKind Kind, string Text, DateTimeOffset Timestamp);

    private sealed record ActivityScan(DateOnly? Focus, Dictionary<DateOnly, List<SessionActivity>> Days);

    /// <summary>
    /// Knows which project a folder belongs to and what was committed there. Built once per recap,
    /// so every folder is resolved and every repository is asked for its log exactly once.
    /// </summary>
    private sealed class ProjectDirectory
    {
        private const string DetachedHead = "HEAD";
        private const string UnknownFolderName = "(folder unknown)";
        private static readonly char[] Separators = ['/', '\\'];

        private readonly Dictionary<string, string> _roots;
        private readonly Dictionary<string, IReadOnlyList<GitCommit>> _commitsByRootAndDay;

        private ProjectDirectory(
            Dictionary<string, string> roots,
            Dictionary<string, IReadOnlyList<GitCommit>> commitsByRootAndDay)
        {
            _roots = roots;
            _commitsByRootAndDay = commitsByRootAndDay;
        }

        public static async Task<ProjectDirectory> BuildAsync(
            IGitActivityReader git,
            RecapCalendar calendar,
            List<(DateOnly Day, IReadOnlyList<SessionActivity> Sessions)> days,
            bool includeGit,
            CancellationToken cancellationToken)
        {
            var roots = new Dictionary<string, string>(StringComparer.Ordinal);
            var commits = new Dictionary<string, IReadOnlyList<GitCommit>>(StringComparer.Ordinal);

            if (!includeGit)
            {
                return new ProjectDirectory(roots, commits);
            }

            var folders = days
                .SelectMany(entry => entry.Sessions)
                .Select(session => session.Folder)
                .Where(folder => folder.IsKnown)
                .DistinctBy(folder => folder.Key);

            foreach (var folder in folders)
            {
                var root = await git.FindRepositoryRootAsync(folder.Display, cancellationToken).ConfigureAwait(false);

                if (root is not null)
                {
                    roots[folder.Key] = root;
                }
            }

            var from = calendar.StartOf(days.Min(entry => entry.Day));
            var to = calendar.StartOf(days.Max(entry => entry.Day).AddDays(1));

            foreach (var root in roots.Values.DistinctBy(WorkingFolder.NormalizeKey))
            {
                var log = await git.GetCommitsAsync(root, from, to, cancellationToken).ConfigureAwait(false);

                foreach (var group in log.GroupBy(commit => calendar.DayOf(commit.Timestamp)))
                {
                    commits[CommitKey(root, group.Key)] =
                    [
                        .. group
                            .OrderBy(commit => commit.Timestamp)
                            .Select(commit => commit with { Timestamp = calendar.ToLocal(commit.Timestamp) }),
                    ];
                }
            }

            return new ProjectDirectory(roots, commits);
        }

        public static string NameOf(string path)
        {
            if (path.Length == 0)
            {
                return UnknownFolderName;
            }

            var trimmed = path.TrimEnd(Separators);
            var lastSeparator = trimmed.LastIndexOfAny(Separators);

            return lastSeparator < 0 || lastSeparator == trimmed.Length - 1 ? path : trimmed[(lastSeparator + 1)..];
        }

        /// <summary>
        /// A detached checkout records its branch as <c>HEAD</c>, which names nothing a listener
        /// would recognise, so it is treated as no branch at all.
        /// </summary>
        public static string? RealBranch(string? branch) =>
            string.IsNullOrWhiteSpace(branch) || string.Equals(branch, DetachedHead, StringComparison.Ordinal)
                ? null
                : branch;

        public string KeyOf(WorkingFolder folder) => WorkingFolder.NormalizeKey(PathOf(folder));

        public string PathOf(WorkingFolder folder) =>
            _roots.TryGetValue(folder.Key, out var root) ? root : folder.Display;

        public IReadOnlyList<GitCommit> CommitsOn(WorkingFolder folder, DateOnly day) =>
            _roots.TryGetValue(folder.Key, out var root)
            && _commitsByRootAndDay.TryGetValue(CommitKey(root, day), out var commits)
                ? commits
                : [];

        private static string CommitKey(string root, DateOnly day) =>
            string.Concat(WorkingFolder.NormalizeKey(root), "|", day.DayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
