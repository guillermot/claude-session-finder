using System.Text;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// One program to run, with its arguments kept apart from one another rather than pre-joined.
/// </summary>
/// <remarks>
/// <para>
/// Arguments are a list because that is the only form in which "a folder whose name contains a
/// space" and "a folder whose name contains a quote" are the same problem. Joining them is a single
/// tested operation on this type instead of a piece of string handling repeated at every call site.
/// </para>
/// <para>
/// There is nothing platform-specific here beyond the quoting convention, which is why the type
/// lives in the slice rather than in the Windows adapter: the decision of <em>which</em> arguments
/// a resume needs is the part worth testing, and it must not be reachable only by clicking.
/// </para>
/// </remarks>
public sealed record ShellCommand
{
    private const char Quote = '"';
    private const char Backslash = '\\';
    private const char Separator = ' ';

    /// <summary>The program to run, as an absolute path or a name the operating system resolves.</summary>
    public required string Executable { get; init; }

    /// <summary>The arguments, one element per argument, unquoted.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>
    /// The directory the program starts in, or <see langword="null"/> to inherit the caller's.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>What should happen to the console window the command would create.</summary>
    public ConsoleWindowMode ConsoleWindow { get; init; } = ConsoleWindowMode.None;

    /// <summary>
    /// Whether this program's exit code says anything about whether it did what it was asked.
    /// </summary>
    /// <remarks>
    /// Off unless a command opts in, because the answer for the file manager is no: it returns a
    /// nonzero code after opening a window perfectly successfully, so a launcher that assumed
    /// otherwise would report every reveal as broken. A command sets this only where the code was
    /// measured to mean something, and even then it buys one thing — a program that dies within a
    /// moment of starting is reported instead of vanishing.
    /// </remarks>
    public bool IsExitCodeMeaningful { get; init; }

    /// <summary>
    /// Joins the arguments into the single string a process-start API takes, quoting each one so
    /// that it survives being split apart again by the program that receives it.
    /// </summary>
    /// <remarks>
    /// The rules are the ones the Windows command-line parser applies: an argument is quoted when it
    /// is empty or contains whitespace or a quote; backslashes are doubled only when they run up
    /// against a quote, which is what stops a path ending in a separator from escaping the closing
    /// quote and swallowing the rest of the line.
    /// </remarks>
    /// <returns>The joined argument string, empty when there are no arguments.</returns>
    public string ToArgumentString()
    {
        var builder = new StringBuilder();

        foreach (var argument in Arguments)
        {
            if (builder.Length > 0)
            {
                builder.Append(Separator);
            }

            AppendQuoted(builder, argument);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders the whole command the way a person would type it, which is what a "copy the command"
    /// action puts on the clipboard.
    /// </summary>
    /// <returns>The executable followed by its quoted arguments.</returns>
    public string ToCommandLine()
    {
        var builder = new StringBuilder();

        AppendQuoted(builder, Executable);

        var arguments = ToArgumentString();

        if (arguments.Length > 0)
        {
            builder.Append(Separator).Append(arguments);
        }

        return builder.ToString();
    }

    private static void AppendQuoted(StringBuilder builder, string argument)
    {
        if (!NeedsQuoting(argument))
        {
            builder.Append(argument);
            return;
        }

        builder.Append(Quote);

        var index = 0;

        while (index < argument.Length)
        {
            var current = argument[index++];

            if (current == Backslash)
            {
                index = AppendBackslashRun(builder, argument, index);
                continue;
            }

            if (current == Quote)
            {
                builder.Append(Backslash);
            }

            builder.Append(current);
        }

        builder.Append(Quote);
    }

    /// <summary>
    /// Emits a run of backslashes, doubling it only when the run is about to meet a quote — either
    /// one that was already in the argument or the closing one this type adds.
    /// </summary>
    private static int AppendBackslashRun(StringBuilder builder, string argument, int index)
    {
        var count = 1;

        while (index < argument.Length && argument[index] == Backslash)
        {
            index++;
            count++;
        }

        var meetsQuote = index == argument.Length || argument[index] == Quote;

        builder.Append(Backslash, meetsQuote ? count * 2 : count);

        return index;
    }

    private static bool NeedsQuoting(string argument) =>
        argument.Length == 0 || argument.AsSpan().ContainsAny(" \t\n\v\"");
}
