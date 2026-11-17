using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace ConvertNow.Core;

/// <summary>
/// Central registry of all supported input and target media formats.
/// </summary>
public static class FormatRegistry
{
    private static readonly MediaFormat[] VideoInputs =
    [
        CreateFormat("mp4", FormatCategory.Video),
        CreateFormat("avi", FormatCategory.Video),
        CreateFormat("mkv", FormatCategory.Video),
        CreateFormat("mov", FormatCategory.Video),
        CreateFormat("webm", FormatCategory.Video),
        CreateFormat("flv", FormatCategory.Video),
        CreateFormat("wmv", FormatCategory.Video),
        CreateFormat("m4v", FormatCategory.Video),
        CreateFormat("3gp", FormatCategory.Video),
        CreateFormat("ts", FormatCategory.Video),
        CreateFormat("mpg", FormatCategory.Video),
        CreateFormat("mpeg", FormatCategory.Video),
        CreateFormat("vob", FormatCategory.Video),
        CreateFormat("ogv", FormatCategory.Video),
    ];

    private static readonly MediaFormat[] AudioInputs =
    [
        CreateFormat("mp3", FormatCategory.Audio),
        CreateFormat("wav", FormatCategory.Audio),
        CreateFormat("flac", FormatCategory.Audio),
        CreateFormat("aac", FormatCategory.Audio),
        CreateFormat("ogg", FormatCategory.Audio),
        CreateFormat("wma", FormatCategory.Audio),
        CreateFormat("m4a", FormatCategory.Audio),
        CreateFormat("opus", FormatCategory.Audio),
        CreateFormat("aiff", FormatCategory.Audio),
        CreateFormat("ape", FormatCategory.Audio),
        CreateFormat("ac3", FormatCategory.Audio),
        CreateFormat("dts", FormatCategory.Audio),
    ];

    private static readonly MediaFormat[] ImageInputs =
    [
        CreateFormat("jpg", FormatCategory.Image),
        CreateFormat("jpeg", FormatCategory.Image),
        CreateFormat("png", FormatCategory.Image),
        CreateFormat("webp", FormatCategory.Image),
        CreateFormat("gif", FormatCategory.Image),
        CreateFormat("bmp", FormatCategory.Image),
        CreateFormat("tiff", FormatCategory.Image),
        CreateFormat("tif", FormatCategory.Image),
        CreateFormat("ico", FormatCategory.Image),
        CreateFormat("avif", FormatCategory.Image),
        CreateFormat("heic", FormatCategory.Image),
        CreateFormat("heif", FormatCategory.Image),
    ];

    private static readonly MediaFormat[] AllInputs =
    [
        .. VideoInputs,
        .. AudioInputs,
        .. ImageInputs,
    ];

    private static readonly FrozenDictionary<string, MediaFormat> InputLookup =
        AllInputs.ToFrozenDictionary(static f => f.Extension, StringComparer.OrdinalIgnoreCase);

    private static readonly string[] TargetExtensions =
    [
        // Video targets (10)
        "mp4", "avi", "mkv", "mov", "webm", "flv", "wmv", "m4v", "3gp", "ts",
        // Audio targets (8)
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "opus", "wma",
        // Image targets (7)
        "jpg", "png", "webp", "bmp", "tiff", "ico", "avif",
    ];

    private static readonly MediaFormat[] AllTargets =
        TargetExtensions.Select(static ext => InputLookup[ext]).ToArray();

    private static readonly FrozenDictionary<string, MediaFormat> TargetLookup =
        AllTargets.ToFrozenDictionary(static f => f.Extension, StringComparer.OrdinalIgnoreCase);

    private static MediaFormat CreateFormat(string extension, FormatCategory category) =>
        new(extension, extension.ToUpperInvariant(), category);

    /// <summary>
    /// Extracts and normalizes an extension from a file path or raw extension string.
    /// </summary>
    /// <param name="pathOrExtension">A full or relative file path, file name, or extension (with or without leading dot).</param>
    /// <returns>The lowercase extension without a leading dot, or <see cref="string.Empty"/> when none is present.</returns>
    public static string NormalizeExtension(string? pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
        {
            return string.Empty;
        }

        string trimmed = pathOrExtension.Trim();

        // Treat inputs containing path separators or a drive qualifier as paths so an extensionless
        // file named "mp4" inside a directory (for example, "C:\dir\mp4") does not match ".mp4".
        bool looksLikePath = trimmed.Contains('\\')
            || trimmed.Contains('/')
            || trimmed.Contains(':');

        if (looksLikePath || trimmed.Contains('.'))
        {
            string ext = Path.GetExtension(trimmed);
            return string.IsNullOrEmpty(ext)
                ? string.Empty
                : ext.TrimStart('.').ToLowerInvariant();
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Determines whether the specified file path or extension represents a supported input format.
    /// </summary>
    /// <param name="pathOrExtension">A file path or extension.</param>
    /// <returns><see langword="true"/> if supported; otherwise <see langword="false"/>.</returns>
    public static bool IsSupportedInput(string? pathOrExtension) =>
        TryGetInputFormat(pathOrExtension, out _);

    /// <summary>
    /// Attempts to resolve a supported input <see cref="MediaFormat"/> from a file path or extension.
    /// </summary>
    /// <param name="pathOrExtension">A file path or extension.</param>
    /// <param name="format">When this method returns <see langword="true"/>, contains the matching format.</param>
    /// <returns><see langword="true"/> if the input format is supported; otherwise <see langword="false"/>.</returns>
    public static bool TryGetInputFormat(string? pathOrExtension, [NotNullWhen(true)] out MediaFormat? format)
    {
        string normalized = NormalizeExtension(pathOrExtension);
        if (normalized.Length == 0)
        {
            format = null;
            return false;
        }

        return InputLookup.TryGetValue(normalized, out format);
    }

    /// <summary>
    /// Gets the <see cref="FormatCategory"/> for the specified input path or extension, or <see langword="null"/> if unsupported.
    /// </summary>
    /// <param name="pathOrExtension">A file path or extension.</param>
    /// <returns>The format category, or <see langword="null"/> when unsupported.</returns>
    public static FormatCategory? GetCategory(string? pathOrExtension) =>
        TryGetInputFormat(pathOrExtension, out MediaFormat? format) ? format.Category : null;

    /// <summary>
    /// Returns all supported input formats across all categories.
    /// </summary>
    /// <returns>An immutable read-only list of all supported input formats.</returns>
    public static IReadOnlyList<MediaFormat> GetAllInputFormats() => AllInputs;

    /// <summary>
    /// Returns all supported input formats belonging to the specified <paramref name="category"/>.
    /// </summary>
    /// <param name="category">The category to query.</param>
    /// <returns>An immutable read-only list of input formats for the category.</returns>
    public static IReadOnlyList<MediaFormat> GetInputFormats(FormatCategory category) => category switch
    {
        FormatCategory.Video => VideoInputs,
        FormatCategory.Audio => AudioInputs,
        FormatCategory.Image => ImageInputs,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown format category."),
    };

    /// <summary>
    /// Attempts to resolve a supported conversion target <see cref="MediaFormat"/> by extension.
    /// </summary>
    /// <param name="extension">The target extension or path.</param>
    /// <param name="format">When this method returns <see langword="true"/>, contains the matching target format.</param>
    /// <returns><see langword="true"/> if the extension is a valid conversion target; otherwise <see langword="false"/>.</returns>
    public static bool TryGetTargetFormat(string? extension, [NotNullWhen(true)] out MediaFormat? format)
    {
        string normalized = NormalizeExtension(extension);
        if (normalized.Length == 0)
        {
            format = null;
            return false;
        }

        return TargetLookup.TryGetValue(normalized, out format);
    }

    /// <summary>
    /// Returns all valid conversion target formats across all categories.
    /// </summary>
    /// <returns>An immutable read-only list of all supported target formats.</returns>
    public static IReadOnlyList<MediaFormat> GetAllTargetFormats() => AllTargets;
}
