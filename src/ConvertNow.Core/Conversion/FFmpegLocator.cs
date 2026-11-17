using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ConvertNow.Core.Tests")]

namespace ConvertNow.Core;

/// <summary>
/// Locates bundled or system-installed <c>ffmpeg</c> and <c>ffprobe</c> executables.
/// </summary>
public static class FFmpegLocator
{
    private const int MaxAncestorSearchDepth = 6;

    /// <summary>
    /// Locates the <c>ffmpeg</c> executable using the standard search order.
    /// </summary>
    /// <param name="overrideDirectory">Optional directory checked before standard locations.</param>
    /// <returns>The full path to the executable, or <see langword="null"/> if not found.</returns>
    public static string? LocateFFmpeg(string? overrideDirectory = null) =>
        LocateExecutable(GetExecutableName("ffmpeg"), overrideDirectory);

    /// <summary>
    /// Locates the <c>ffprobe</c> executable using the standard search order.
    /// </summary>
    /// <param name="overrideDirectory">Optional directory checked before standard locations.</param>
    /// <returns>The full path to the executable, or <see langword="null"/> if not found.</returns>
    public static string? LocateFFprobe(string? overrideDirectory = null) =>
        LocateExecutable(GetExecutableName("ffprobe"), overrideDirectory);

    private static string GetExecutableName(string baseName) =>
        OperatingSystem.IsWindows() ? $"{baseName}.exe" : baseName;

    internal static string? LocateExecutable(
        string executableName,
        string? overrideDirectory,
        string? baseDirectory = null,
        string? pathEnvironmentVariable = null,
        Func<string, bool>? fileExists = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);

        Func<string, bool> exists = fileExists ?? File.Exists;

        // 1. Explicit override directory.
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            string overrideCandidate = Path.Combine(overrideDirectory.Trim(), executableName);
            if (exists(overrideCandidate))
            {
                return Path.GetFullPath(overrideCandidate);
            }
        }

        string effectiveBaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory;

        if (!string.IsNullOrWhiteSpace(effectiveBaseDirectory))
        {
            string trimmedBase = effectiveBaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // 2. AppContext.BaseDirectory/tools/ffmpeg
            string bundledCandidate = Path.Combine(trimmedBase, "tools", "ffmpeg", executableName);
            if (exists(bundledCandidate))
            {
                return Path.GetFullPath(bundledCandidate);
            }

            // 3. AppContext.BaseDirectory
            string baseCandidate = Path.Combine(trimmedBase, executableName);
            if (exists(baseCandidate))
            {
                return Path.GetFullPath(baseCandidate);
            }

            // 4. Ancestor directories up to 6 levels for tools/ffmpeg
            DirectoryInfo? current = Directory.GetParent(trimmedBase);
            for (int level = 1; level <= MaxAncestorSearchDepth && current is not null; level++)
            {
                string ancestorCandidate = Path.Combine(current.FullName, "tools", "ffmpeg", executableName);
                if (exists(ancestorCandidate))
                {
                    return Path.GetFullPath(ancestorCandidate);
                }

                current = current.Parent;
            }
        }

        // 5. System PATH environment variable
        string? pathEnv = pathEnvironmentVariable ?? Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            string[] segments = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string segment in segments)
            {
                string cleanDir = segment.Trim().Trim('"');
                if (cleanDir.Length == 0)
                {
                    continue;
                }

                try
                {
                    string pathCandidate = Path.Combine(cleanDir, executableName);
                    if (exists(pathCandidate))
                    {
                        return Path.GetFullPath(pathCandidate);
                    }
                }
                catch (Exception)
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return null;
    }
}
