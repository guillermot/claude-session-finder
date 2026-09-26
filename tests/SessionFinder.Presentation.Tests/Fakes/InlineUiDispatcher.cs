using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Presentation.Tests.Fakes;

/// <summary>
/// Runs posted work on the calling thread, which is what makes a search assertable without a
/// message loop. It counts the posts as well, because "the view model marshalled" is itself a
/// behaviour worth asserting.
/// </summary>
internal sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool IsOnUiThread => true;

    public int PostCount { get; private set; }

    public void Post(Action action)
    {
        PostCount++;
        action();
    }
}
