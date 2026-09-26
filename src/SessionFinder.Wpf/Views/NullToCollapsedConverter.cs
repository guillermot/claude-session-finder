using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SessionFinder.Wpf.Views;

/// <summary>
/// Collapses an element when the value it shows is absent, so that a row with no branch and no
/// snippet takes the height of the two lines it actually has.
/// </summary>
/// <remarks>
/// A blank string counts as absent. Several of these values come out of the transcripts as empty
/// rather than missing, and an element sized for text that is not there reads as a layout fault.
/// </remarks>
[ValueConversion(typeof(object), typeof(Visibility))]
internal sealed class NullToCollapsedConverter : IValueConverter
{
    /// <summary>
    /// Maps a value to a visibility.
    /// </summary>
    /// <param name="value">The value the element displays.</param>
    /// <param name="targetType">Ignored.</param>
    /// <param name="parameter">Ignored.</param>
    /// <param name="culture">Ignored.</param>
    /// <returns><see cref="Visibility.Collapsed"/> when the value is absent or blank.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is string text && string.IsNullOrWhiteSpace(text))
            ? Visibility.Collapsed
            : Visibility.Visible;

    /// <summary>Not supported; the binding is one-way.</summary>
    /// <param name="value">Ignored.</param>
    /// <param name="targetType">Ignored.</param>
    /// <param name="parameter">Ignored.</param>
    /// <param name="culture">Ignored.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
