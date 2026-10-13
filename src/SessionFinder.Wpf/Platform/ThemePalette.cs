using System.Windows;
using Microsoft.Win32;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Keeps the application's own colours in step with the Windows light or dark appearance.
/// </summary>
/// <remarks>
/// <para>
/// The controls themselves follow the system through WPF's Fluent theme. What this covers is the
/// handful of colours the application draws with on its own — the cards, the label levels, the
/// selection — which live in two dictionaries, one per appearance. The right one is merged at
/// start-up and swapped whenever the user changes the setting, and the views look the colours up
/// dynamically, so every open window repaints without a restart.
/// </para>
/// <para>
/// The appearance is read from the same per-user value Windows' own settings page writes. A
/// machine where it cannot be read is treated as light, which is the Windows default.
/// </para>
/// </remarks>
internal static class ThemePalette
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightTheme = "AppsUseLightTheme";
    private const string LightPalette = "Themes/Palette.Light.xaml";
    private const string DarkPalette = "Themes/Palette.Dark.xaml";

    private static ResourceDictionary? _current;

    /// <summary>
    /// Merges the palette for the current appearance and follows later changes.
    /// </summary>
    /// <param name="application">The application whose resources carry the palette.</param>
    public static void Follow(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        Apply(application);

        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
            {
                application.Dispatcher.BeginInvoke(() => Apply(application));
            }
        };
    }

    private static void Apply(Application application)
    {
        var palette = new ResourceDictionary
        {
            Source = new Uri(IsLight() ? LightPalette : DarkPalette, UriKind.Relative),
        };

        var merged = application.Resources.MergedDictionaries;

        if (_current is not null)
        {
            merged.Remove(_current);
        }

        merged.Add(palette);
        _current = palette;
    }

    private static bool IsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);

        return key?.GetValue(AppsUseLightTheme) is not int value || value != 0;
    }
}
