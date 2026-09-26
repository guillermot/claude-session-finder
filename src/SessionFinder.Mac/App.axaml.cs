using Avalonia;
using Avalonia.Markup.Xaml;

namespace SessionFinder.Mac;

/// <summary>
/// The Avalonia application object, which carries the resource dictionary and nothing else.
/// </summary>
internal sealed partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
