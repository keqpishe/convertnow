namespace ConvertNow.Core;

/// <summary>
/// Resolves conflict-free output file paths using <c>(N)</c> numeric suffixes.
/// </summary>
public static class OutputNameResolver
{
    /// <summary>
    /// Computes a unique output file path for converting <paramref name="inputFilePath"/> to <paramref name="targetExtension"/>.
    /// </summary>
    /// <param name="inputFilePath">Full or relative path to the source file.</param>
    /// <param name="targetExtension">Target extension (with or without leading dot).</param>
    /// <param name="customOutputDirectory">Optional directory override; defaults to the input file's directory.</param>
    /// <param name="fileExists">Optional existence check delegate used for testing; defaults to <see cref="File.Exists(string?)"/>.</param>
    /// <returns>A non-conflicting output file path that will not overwrite the input file or existing files.</returns>
    public static string ResolveUniqueOutputPath(
        string inputFilePath,
        string targetExtension,
        string? customOutputDirectory = null,
        Func<string, bool>? fileExists = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExtension);

        string normalizedExtension = FormatRegistry.NormalizeExtension(targetExtension);
        if (normalizedExtension.Length == 0)
        {
            throw new ArgumentException("Target extension cannot be empty.", nameof(targetExtension));
        }

        string fileName = Path.GetFileName(inputFilePath);
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException("Input path must refer to a file.", nameof(inputFilePath));
        }

        string baseName = Path.GetFileNameWithoutExtension(inputFilePath);
        string outputDirectory = !string.IsNullOrWhiteSpace(customOutputDirectory)
            ? customOutputDirectory.Trim()
            : Path.GetDirectoryName(inputFilePath) ?? string.Empty;

        Func<string, bool> existsCheck = fileExists ?? File.Exists;
        string normalizedInputPath = NormalizePathForComparison(inputFilePath);
        string extensionWithDot = $".{normalizedExtension}";

        string initialCandidate = Path.Combine(outputDirectory, $"{baseName}{extensionWithDot}");
        if (!IsConflictingPath(initialCandidate, normalizedInputPath, existsCheck))
        {
            return initialCandidate;
        }

        for (int index = 1; ; index++)
        {
            string candidate = Path.Combine(outputDirectory, $"{baseName} ({index}){extensionWithDot}");
            if (!IsConflictingPath(candidate, normalizedInputPath, existsCheck))
            {
                return candidate;
            }

            if (index == int.MaxValue)
            {
                throw new IOException($"Could not resolve a unique output path for '{inputFilePath}' in '{outputDirectory}'.");
            }
        }
    }

    private static bool IsConflictingPath(string candidatePath, string normalizedInputPath, Func<string, bool> existsCheck)
    {
        string normalizedCandidate = NormalizePathForComparison(candidatePath);
        if (string.Equals(normalizedCandidate, normalizedInputPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return existsCheck(candidatePath);
    }

    private static string NormalizePathForComparison(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception)
        {
            return path.Replace('/', Path.DirectorySeparatorChar)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
