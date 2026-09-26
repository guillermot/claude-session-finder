using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.IndexStatus;

/// <summary>
/// The answer to <see cref="GetIndexStatusQuery"/>.
/// </summary>
/// <param name="Index">
/// The measured state of the index, reported as the domain snapshot rather than copied field by
/// field into a second shape that could drift from it.
/// </param>
public sealed record IndexStatusResult(IndexStatistics Index);
