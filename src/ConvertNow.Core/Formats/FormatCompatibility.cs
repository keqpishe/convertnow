namespace ConvertNow.Core;

/// <summary>
/// Defines the format conversion compatibility matrix and default output format selection rules.
/// </summary>
public static class FormatCompatibility
{
    private static readonly MediaFormat[] VideoCompatibleOutputs =
        ResolveTargets(
            "mp4", "avi", "mkv", "mov", "webm", "flv", "wmv", "m4v", "3gp", "ts",
            "mp3", "wav", "flac", "aac", "ogg", "m4a", "opus");

    private static readonly MediaFormat[] AudioCompatibleOutputs =
        ResolveTargets("mp3", "wav", "flac", "aac", "ogg", "m4a", "opus", "wma");

    private static readonly MediaFormat[] ImageCompatibleOutputs =
        ResolveTargets("jpg", "png", "webp", "bmp", "tiff", "ico", "avif");

    private static MediaFormat[] ResolveTargets(params string[] extensions)
    {
        var result = new MediaFormat[extensions.Length];
        for (int i = 0; i < extensions.Length; i++)
        {
            if (!FormatRegistry.TryGetTargetFormat(extensions[i], out MediaFormat? format))
            {
                throw new InvalidOperationException($"Unknown target format '{extensions[i]}' in compatibility table.");
            }

            result[i] = format;
        }

        return result;
    }

    /// <summary>
    /// Gets the list of compatible target formats for a given source <see cref="FormatCategory"/>.
    /// </summary>
    /// <param name="inputCategory">The source file's media category.</param>
    /// <returns>An ordered read-only list of compatible output formats.</returns>
    public static IReadOnlyList<MediaFormat> GetCompatibleOutputFormats(FormatCategory inputCategory) =>
        inputCategory switch
        {
            FormatCategory.Video => VideoCompatibleOutputs,
            FormatCategory.Audio => AudioCompatibleOutputs,
            FormatCategory.Image => ImageCompatibleOutputs,
            _ => throw new ArgumentOutOfRangeException(nameof(inputCategory), inputCategory, "Unknown format category."),
        };

    /// <summary>
    /// Gets the list of compatible target formats for the specified input path or extension.
    /// </summary>
    /// <param name="inputPathOrExtension">The source file path or extension.</param>
    /// <returns>An ordered read-only list of compatible output formats, or an empty list if the input is unsupported.</returns>
    public static IReadOnlyList<MediaFormat> GetCompatibleOutputFormats(string? inputPathOrExtension) =>
        FormatRegistry.TryGetInputFormat(inputPathOrExtension, out MediaFormat? format)
            ? GetCompatibleOutputFormats(format.Category)
            : [];

    /// <summary>
    /// Checks whether converting from <paramref name="inputPathOrExtension"/> to <paramref name="targetExtension"/> is supported.
    /// </summary>
    /// <param name="inputPathOrExtension">The source file path or extension.</param>
    /// <param name="targetExtension">The desired target format extension.</param>
    /// <returns><see langword="true"/> if compatible; otherwise <see langword="false"/>.</returns>
    public static bool IsCompatible(string? inputPathOrExtension, string? targetExtension)
    {
        string normalizedTarget = FormatRegistry.NormalizeExtension(targetExtension);
        if (normalizedTarget.Length == 0)
        {
            return false;
        }

        IReadOnlyList<MediaFormat> outputs = GetCompatibleOutputFormats(inputPathOrExtension);
        for (int i = 0; i < outputs.Count; i++)
        {
            if (string.Equals(outputs[i].Extension, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Selects a sensible default target format for the specified input path or extension.
    /// Prefers the first compatible format whose normalized extension differs from the input extension,
    /// falling back to the first compatible format when needed.
    /// </summary>
    /// <param name="inputPathOrExtension">The source file path or extension.</param>
    /// <returns>The default target format, or <see langword="null"/> if the input is unsupported.</returns>
    public static MediaFormat? GetDefaultOutputFormat(string? inputPathOrExtension)
    {
        if (!FormatRegistry.TryGetInputFormat(inputPathOrExtension, out MediaFormat? inputFormat))
        {
            return null;
        }

        IReadOnlyList<MediaFormat> compatible = GetCompatibleOutputFormats(inputFormat.Category);
        for (int i = 0; i < compatible.Count; i++)
        {
            if (!string.Equals(compatible[i].Extension, inputFormat.Extension, StringComparison.OrdinalIgnoreCase))
            {
                return compatible[i];
            }
        }

        return compatible.Count > 0 ? compatible[0] : null;
    }
}
