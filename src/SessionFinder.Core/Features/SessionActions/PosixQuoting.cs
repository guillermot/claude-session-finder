using System.Text;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Quotes an argument the way a POSIX shell reads one back.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately not a method on <see cref="ShellCommand"/>. That type quotes for the
/// Windows command-line parser, those rules are tested, and the Windows head depends on them; a
/// single routine that tried to serve both would have to be told which dialect it was in at every
/// call and would be wrong somewhere the first time someone forgot.
/// </para>
/// <para>
/// Single quotes are used rather than double because inside them a POSIX shell expands nothing at
/// all — no variable, no backtick, no backslash. That leaves exactly one character to handle, the
/// single quote itself, which cannot be escaped inside such a run and is instead written by closing
/// the run, emitting an escaped quote, and opening a new one.
/// </para>
/// </remarks>
public static class PosixQuoting
{
    private const char SingleQuote = '\'';
    private const string EscapedSingleQuote = @"'\''";

    /// <summary>
    /// Wraps an argument so that a POSIX shell hands it back as the one string it started as.
    /// </summary>
    /// <param name="argument">The unquoted argument.</param>
    /// <returns>The quoted argument.</returns>
    public static string Quote(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        var builder = new StringBuilder(argument.Length + 2);

        builder.Append(SingleQuote);

        foreach (var character in argument)
        {
            if (character == SingleQuote)
            {
                builder.Append(EscapedSingleQuote);
                continue;
            }

            builder.Append(character);
        }

        builder.Append(SingleQuote);

        return builder.ToString();
    }
}
