namespace SessionFinder.Core.Domain;

/// <summary>
/// What to do with one transcript file on this pass. A closed union: the private constructor means
/// the only cases are the three declared here, so a <c>switch</c> over them is exhaustive.
/// </summary>
public abstract record ParsePlan
{
    private ParsePlan()
    {
    }

    /// <summary>The file has not changed since the last pass and must not be opened.</summary>
    public sealed record Skip : ParsePlan
    {
        /// <summary>The single instance; the case carries no data.</summary>
        public static Skip Instance { get; } = new();
    }

    /// <summary>The file grew, so only the bytes after the watermark need reading.</summary>
    /// <param name="Offset">Byte offset to seek to before reading.</param>
    public sealed record Resume(long Offset) : ParsePlan;

    /// <summary>The stored state cannot be trusted; the file is read from offset zero.</summary>
    /// <param name="Reason">Why the incremental path was abandoned.</param>
    public sealed record FullReparse(FullReparseReason Reason) : ParsePlan;
}
