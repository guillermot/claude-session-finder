using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Presentation.Tests.Fakes;

internal sealed class FakeAppWindow : IAppWindow
{
    public bool IsVisible { get; private set; }

    public int ShowCount { get; private set; }

    public int HideCount { get; private set; }

    public void Show()
    {
        ShowCount++;
        IsVisible = true;
    }

    public void Hide()
    {
        HideCount++;
        IsVisible = false;
    }
}
