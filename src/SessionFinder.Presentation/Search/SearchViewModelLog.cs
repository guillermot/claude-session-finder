using Microsoft.Extensions.Logging;

namespace SessionFinder.Presentation.Search;

/// <summary>
/// Source-generated log messages for the search view model.
/// </summary>
/// <remarks>
/// Nothing here takes the query or any result text. The transcripts are private working material,
/// and a log line naming what was searched for would put it on disk in plain text.
/// </remarks>
internal static partial class SearchViewModelLog
{
    [LoggerMessage(
        EventId = 1400,
        Level = LogLevel.Error,
        Message = "A search failed; the search box is reporting it and staying open.")]
    public static partial void SearchFailed(ILogger logger, Exception exception);
}
