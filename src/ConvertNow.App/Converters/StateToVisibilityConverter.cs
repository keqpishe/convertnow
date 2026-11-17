using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ConvertNow.App.Converters;

/// <summary>
/// Converts a state enum or string value to <see cref="Visibility.Visible"/> when it matches
/// any comma-separated value specified in <c>ConverterParameter</c>.
/// </summary>
public sealed class StateToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is null || parameter is null)
        {
            return Visibility.Collapsed;
        }

        string? currentState = value.ToString();
        string? targetStates = parameter.ToString();

        if (string.IsNullOrWhiteSpace(currentState) || string.IsNullOrWhiteSpace(targetStates))
        {
            return Visibility.Collapsed;
        }

        string[] candidates = targetStates.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string candidate in candidates)
        {
            if (string.Equals(currentState, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }
        }

        return Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
