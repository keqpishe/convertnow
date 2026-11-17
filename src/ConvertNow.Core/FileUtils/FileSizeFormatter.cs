using System.Globalization;

namespace ConvertNow.Core;

/// <summary>
/// Formats byte counts as human-readable strings using invariant formatting.
/// </summary>
public static class FileSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Formats a byte count into a human-readable size string (for example, <c>"0 B"</c>, <c>"1.5 MB"</c>, <c>"4.2 GB"</c>).
    /// </summary>
    /// <param name="bytes">Size in bytes. Negative values are treated as zero.</param>
    /// <returns>An invariant-culture formatted size string.</returns>
    public static string Format(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes.ToString(CultureInfo.InvariantCulture)} B"
            : $"{value.ToString("0.#", CultureInfo.InvariantCulture)} {Units[unitIndex]}";
    }
}
