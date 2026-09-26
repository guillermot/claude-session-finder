using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Features.SessionActions;

/// <summary>
/// A clipboard that keeps the text instead of putting it anywhere.
/// </summary>
internal sealed class RecordingClipboardService : IClipboardService
{
    private Result _answer = Result.Success();

    public List<string> Copied { get; } = [];

    public string? Last => Copied.Count == 0 ? null : Copied[^1];

    public void Fails(AppError error) => _answer = Result.Failure(error);

    public Task<Result> SetTextAsync(string text, CancellationToken cancellationToken)
    {
        Copied.Add(text);

        return Task.FromResult(_answer);
    }
}
