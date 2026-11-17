using System.Runtime.InteropServices;

namespace ConvertNow.Core;

/// <summary>
/// Represents the outcome of checking available disk space prior to conversion.
/// </summary>
/// <param name="HasCriticalLowSpace"><see langword="true"/> when available space is less than the input file size (or zero).</param>
/// <param name="ShowLowSpaceWarning"><see langword="true"/> when available space is below the recommended threshold (1.5x input size).</param>
/// <param name="AvailableBytes">Available free bytes on the target volume, or <c>-1</c> if free space could not be queried.</param>
/// <param name="RecommendedBytes">Recommended free bytes (1.5x the input file size).</param>
/// <param name="InputFileSizeBytes">Size of the input file in bytes.</param>
/// <param name="WarningMessage">User-facing warning text, or <see langword="null"/> when sufficient disk space is available.</param>
public sealed record DiskSpaceCheckResult(
    bool HasCriticalLowSpace,
    bool ShowLowSpaceWarning,
    long AvailableBytes,
    long RecommendedBytes,
    long InputFileSizeBytes,
    string? WarningMessage);

/// <summary>
/// Checks free disk space on local drives and UNC network shares prior to running a conversion.
/// </summary>
public static class DiskSpaceChecker
{
    /// <summary>
    /// Checks whether <paramref name="targetDirectory"/> has enough free space to convert <paramref name="inputFilePath"/>.
    /// </summary>
    /// <param name="inputFilePath">Path to the existing source file.</param>
    /// <param name="targetDirectory">Destination directory where the output file will be written.</param>
    /// <returns>A <see cref="DiskSpaceCheckResult"/> summarizing available and recommended space.</returns>
    public static DiskSpaceCheckResult CheckDiskSpace(string inputFilePath, string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        var inputInfo = new FileInfo(inputFilePath);
        if (!inputInfo.Exists)
        {
            throw new FileNotFoundException("Input file not found for disk space check.", inputFilePath);
        }

        long inputFileSizeBytes = inputInfo.Length;
        long? availableBytes = TryGetAvailableFreeBytes(targetDirectory);

        return availableBytes is { } free
            ? Evaluate(inputFileSizeBytes, free)
            : new DiskSpaceCheckResult(
                HasCriticalLowSpace: false,
                ShowLowSpaceWarning: false,
                AvailableBytes: -1,
                RecommendedBytes: CalculateRecommendedBytes(inputFileSizeBytes),
                InputFileSizeBytes: inputFileSizeBytes,
                WarningMessage: null);
    }

    /// <summary>
    /// Evaluates disk space thresholds for a known input file size and free byte count.
    /// </summary>
    /// <param name="inputFileSizeBytes">Size of the input file in bytes.</param>
    /// <param name="availableBytes">Available free bytes on the target volume (or negative if unknown).</param>
    /// <returns>A <see cref="DiskSpaceCheckResult"/> describing warning and critical thresholds.</returns>
    public static DiskSpaceCheckResult Evaluate(long inputFileSizeBytes, long availableBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputFileSizeBytes);

        long recommendedBytes = CalculateRecommendedBytes(inputFileSizeBytes);

        if (availableBytes < 0)
        {
            return new DiskSpaceCheckResult(
                HasCriticalLowSpace: false,
                ShowLowSpaceWarning: false,
                AvailableBytes: availableBytes,
                RecommendedBytes: recommendedBytes,
                InputFileSizeBytes: inputFileSizeBytes,
                WarningMessage: null);
        }

        bool hasCriticalLowSpace = availableBytes == 0 || availableBytes < inputFileSizeBytes;
        bool showLowSpaceWarning = hasCriticalLowSpace || availableBytes < recommendedBytes;

        string? warningMessage = null;
        if (hasCriticalLowSpace)
        {
            warningMessage =
                $"Critically low disk space: {FileSizeFormatter.Format(availableBytes)} available, " +
                $"which is less than the input file size ({FileSizeFormatter.Format(inputFileSizeBytes)}).";
        }
        else if (showLowSpaceWarning)
        {
            warningMessage =
                $"Low disk space: {FileSizeFormatter.Format(availableBytes)} available; " +
                $"at least {FileSizeFormatter.Format(recommendedBytes)} recommended.";
        }

        return new DiskSpaceCheckResult(
            HasCriticalLowSpace: hasCriticalLowSpace,
            ShowLowSpaceWarning: showLowSpaceWarning,
            AvailableBytes: availableBytes,
            RecommendedBytes: recommendedBytes,
            InputFileSizeBytes: inputFileSizeBytes,
            WarningMessage: warningMessage);
    }

    private static long CalculateRecommendedBytes(long inputFileSizeBytes)
    {
        if (inputFileSizeBytes <= 0)
        {
            return 0;
        }

        decimal scaled = Math.Ceiling(inputFileSizeBytes * 1.5m);
        return scaled >= long.MaxValue ? long.MaxValue : (long)scaled;
    }

    private static long? TryGetAvailableFreeBytes(string targetDirectory)
    {
        string fullTargetPath;
        try
        {
            fullTargetPath = Path.GetFullPath(targetDirectory.Trim());
        }
        catch (Exception)
        {
            return null;
        }

        if (OperatingSystem.IsWindows())
        {
            long? win32Space = TryGetWin32FreeBytes(fullTargetPath);
            if (win32Space.HasValue)
            {
                return win32Space.Value;
            }
        }

        return TryGetDriveInfoFreeBytes(fullTargetPath);
    }

    private static long? TryGetWin32FreeBytes(string fullTargetPath)
    {
        const int ErrorFileNotFound = 2;
        const int ErrorPathNotFound = 3;

        // GetDiskFreeSpaceExW supports both local drive paths and UNC network shares (\\server\share\).
        // If a nested target directory does not exist yet, walk up to the nearest existing parent or root.
        string? candidate = fullTargetPath;
        while (!string.IsNullOrEmpty(candidate))
        {
            string queryPath = NormalizeForGetDiskFreeSpaceEx(candidate);
            if (GetDiskFreeSpaceExW(queryPath, out ulong freeBytesAvailable, out _, out _))
            {
                return freeBytesAvailable >= (ulong)long.MaxValue
                    ? long.MaxValue
                    : (long)freeBytesAvailable;
            }

            int error = Marshal.GetLastWin32Error();
            if (error is not (ErrorFileNotFound or ErrorPathNotFound))
            {
                return null;
            }

            DirectoryInfo? parent = Directory.GetParent(candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            candidate = parent?.FullName;
        }

        return null;
    }

    private static string NormalizeForGetDiskFreeSpaceEx(string path)
    {
        // GetDiskFreeSpaceExW requires a trailing backslash on UNC roots like \\server\share\ and drive roots like C:\.
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? root = Path.GetPathRoot(trimmed);
            if (!string.IsNullOrEmpty(root) &&
                string.Equals(trimmed, root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                return trimmed + Path.DirectorySeparatorChar;
            }
        }

        return path;
    }

    private static long? TryGetDriveInfoFreeBytes(string fullTargetPath)
    {
        try
        {
            string? root = Path.GetPathRoot(fullTargetPath);
            if (string.IsNullOrWhiteSpace(root) || root.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return null;
            }

            var driveInfo = new DriveInfo(root);
            return driveInfo.IsReady ? driveInfo.AvailableFreeSpace : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceExW(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailableToCaller,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);
}
