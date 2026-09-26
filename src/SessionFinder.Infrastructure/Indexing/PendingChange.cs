namespace SessionFinder.Infrastructure.Indexing;

/// <summary>
/// When a path was first reported as changed and when it was last reported, which is everything the
/// readiness rule needs.
/// </summary>
/// <param name="FirstSeenUtc">When the first notification for this path arrived.</param>
/// <param name="LastSeenUtc">When the most recent notification for this path arrived.</param>
public readonly record struct PendingChange(DateTimeOffset FirstSeenUtc, DateTimeOffset LastSeenUtc);
