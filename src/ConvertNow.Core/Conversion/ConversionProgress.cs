using System.Globalization;

namespace ConvertNow.Core;

/// <summary>
/// Reports real-time progress for an active FFmpeg conversion.
/// </summary>
/// <param name="Percentage">Completion percentage clamped to <c>0..100</c>.</param>
/// <param name="ProcessedTime">Media duration processed so far.</param>
/// <param name="TotalDuration">Total expected media duration, or <see langword="null"/> when unknown.</param>
/// <param name="EstimatedRemaining">Estimated wall-clock time remaining, or <see langword="null"/> when indeterminate.</param>
/// <param name="SpeedText">FFmpeg processing speed indicator (for example, <c>"2.4x"</c>), or <see langword="null"/>.</param>
/// <param name="IsIndeterminate"><see langword="true"/> when progress percentage cannot be determined.</param>
public sealed record ConversionProgress(
    double Percentage,
    TimeSpan ProcessedTime,
    TimeSpan? TotalDuration,
    TimeSpan? EstimatedRemaining,
    string? SpeedText,
    bool IsIndeterminate)
{
    /// <summary>
    /// Formats an estimated time remaining into a concise display string (for example, <c>"2m 34s"</c>).
    /// </summary>
    /// <param name="eta">Estimated remaining duration.</param>
    /// <returns>A formatted ETA string, or <c>"--"</c> when unavailable.</returns>
    public static string FormatEta(TimeSpan? eta)
    {
        if (eta is null || eta.Value < TimeSpan.Zero)
        {
            return "--";
        }

        TimeSpan value = eta.Value;
        if (value == TimeSpan.Zero)
        {
            return "0s";
        }

        if (value < TimeSpan.FromSeconds(1))
        {
            return "< 1s";
        }

        int totalHours = (int)value.TotalHours;
        if (totalHours > 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{totalHours}h {value.Minutes}m {value.Seconds}s");
        }

        if (value.Minutes > 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{value.Minutes}m {value.Seconds}s");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value.Seconds}s");
    }
}
