namespace SessionFinder.Core.Domain;

/// <summary>
/// Identifies one Claude Code session. The transcript file name is the identifier, so this type
/// exists to stop a session identifier and a file path from being interchangeable strings.
/// </summary>
/// <param name="Value">The session identifier as stored in the transcript file name.</param>
public readonly record struct SessionId(Guid Value)
{
    private const char PosixSeparator = '/';
    private const char WindowsSeparator = '\\';
    private const char ExtensionSeparator = '.';
    private const string GuidFormat = "D";

    /// <summary>
    /// Reads a session identifier out of a transcript file name or path.
    /// </summary>
    /// <param name="fileNameOrPath">
    /// A bare identifier, a file name such as <c>&lt;uuid&gt;.jsonl</c>, or a full path ending in one.
    /// Both separator characters are recognised so the same code works on Windows and on POSIX.
    /// </param>
    /// <param name="sessionId">The parsed identifier, or <see langword="default"/> on failure.</param>
    /// <returns><see langword="true"/> when the file name carries a well-formed identifier.</returns>
    public static bool TryParseFromFileName(string? fileNameOrPath, out SessionId sessionId)
    {
        sessionId = default;

        if (string.IsNullOrWhiteSpace(fileNameOrPath))
        {
            return false;
        }

        var stem = TrimExtension(TrimDirectory(fileNameOrPath));

        if (!Guid.TryParseExact(stem, GuidFormat, out var value))
        {
            return false;
        }

        sessionId = new SessionId(value);
        return true;
    }

    /// <summary>Renders the identifier the way it appears in the transcript file name.</summary>
    /// <returns>The identifier in lowercase hyphenated form.</returns>
    public override string ToString() => Value.ToString(GuidFormat, System.Globalization.CultureInfo.InvariantCulture);

    private static ReadOnlySpan<char> TrimDirectory(string path)
    {
        var lastSeparator = path.AsSpan().LastIndexOfAny(PosixSeparator, WindowsSeparator);

        return lastSeparator < 0 ? path.AsSpan() : path.AsSpan(lastSeparator + 1);
    }

    private static ReadOnlySpan<char> TrimExtension(ReadOnlySpan<char> fileName)
    {
        var lastDot = fileName.LastIndexOf(ExtensionSeparator);

        return lastDot <= 0 ? fileName : fileName[..lastDot];
    }
}
