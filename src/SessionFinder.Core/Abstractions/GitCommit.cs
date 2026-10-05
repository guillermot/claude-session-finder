namespace SessionFinder.Core.Abstractions;

/// <summary>
/// One commit, as much of it as a stand-up needs.
/// </summary>
/// <param name="Sha">The abbreviated commit hash.</param>
/// <param name="Subject">The first line of the commit message.</param>
/// <param name="Timestamp">When it was authored.</param>
public sealed record GitCommit(string Sha, string Subject, DateTimeOffset Timestamp);
