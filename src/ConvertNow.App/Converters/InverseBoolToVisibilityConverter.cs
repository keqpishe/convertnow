using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ConvertNow.App.Converters;

/// <summary>
/// Converts a <see cref="bool"/> value to <see cref="Visibility.Collapsed"/> when <see langword="true"/>
/// and <see cref="Visibility.Visible"/> otherwise.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is not Visibility.Visible;
}
