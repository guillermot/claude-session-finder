namespace SessionFinder.Core.Features.IndexStatus;

/// <summary>
/// Asks for a snapshot of the index. The query carries nothing: there is exactly one index.
/// </summary>
public sealed record GetIndexStatusQuery
{
    /// <summary>The only instance the query ever needs.</summary>
    public static GetIndexStatusQuery Instance { get; } = new();
}
