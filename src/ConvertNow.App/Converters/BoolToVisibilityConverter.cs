using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ConvertNow.App.Converters;

/// <summary>
/// Converts a <see cref="bool"/> value to <see cref="Visibility.Visible"/> when <see langword="true"/>
/// and <see cref="Visibility.Collapsed"/> otherwise.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}
