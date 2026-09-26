using System.Windows;

namespace SessionFinder.Wpf;

/// <summary>
/// The WPF application object. It carries the resource dictionary and nothing else: the entry
/// point, the host and the shell all live outside it.
/// </summary>
/// <remarks>
/// <c>App.xaml</c> is compiled as a Page rather than as the application definition, so nothing here
/// is generated as an entry point and <see cref="InitializeComponent"/> has to be called by hand.
/// </remarks>
internal partial class App : Application
{
}
