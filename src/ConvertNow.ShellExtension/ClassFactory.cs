using System.Runtime.InteropServices;

namespace ConvertNow.ShellExtension;

/// <summary>
/// Implements <see cref="IClassFactory"/> for <see cref="ConvertNowCommand"/> and provides
/// path resolution helpers for the Convert.Now executable and context menu icon.
/// </summary>
public sealed class ClassFactory : IClassFactory
{
    private const string ExecutableFileName = "ConvertNow.exe";
    private const string IconRelativePath = @"Assets\context-menu-icon.ico";

    private int _lockCount;

    /// <summary>
    /// Gets the active COM server lock count tracked by <see cref="LockServer"/>.
    /// </summary>
    public int LockCount => Volatile.Read(ref _lockCount);

    /// <inheritdoc />
    public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;

        if (pUnkOuter != IntPtr.Zero)
        {
            return HResult.CLASS_E_NOAGGREGATION;
        }

        var command = new ConvertNowCommand();
        IntPtr unknown = Marshal.GetIUnknownForObject(command);
        try
        {
            return Marshal.QueryInterface(unknown, in riid, out ppvObject);
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    /// <inheritdoc />
    public int LockServer(bool fLock)
    {
        if (fLock)
        {
            Interlocked.Increment(ref _lockCount);
        }
        else
        {
            int current;
            do
            {
                current = Volatile.Read(ref _lockCount);
                if (current <= 0)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref _lockCount, current - 1, current) != current);
        }

        return HResult.S_OK;
    }

    /// <summary>
    /// Resolves the path to <c>ConvertNow.exe</c> relative to the Shell Extension assembly directory.
    /// </summary>
    public static string? ResolveExecutablePath()
        => ResolveExecutablePath(GetAssemblyDirectory());

    /// <summary>
    /// Resolves the path to <c>ConvertNow.exe</c> relative to the supplied base directory.
    /// </summary>
    public static string? ResolveExecutablePath(string? assemblyDirectory)
    {
        if (string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            return null;
        }

        string sideBySide = Path.Combine(assemblyDirectory, ExecutableFileName);
        if (File.Exists(sideBySide))
        {
            return Path.GetFullPath(sideBySide);
        }

        string parentCandidate = Path.Combine(assemblyDirectory, "..", ExecutableFileName);
        if (File.Exists(parentCandidate))
        {
            return Path.GetFullPath(parentCandidate);
        }

        string[] configs = GetPreferredConfigurations(assemblyDirectory);

        foreach (string ancestor in EnumerateAncestors(assemblyDirectory, maxLevels: 8))
        {
            foreach (string appRoot in EnumerateAppProjectRoots(ancestor))
            {
                string? devExe = FindExecutableInAppBin(appRoot, configs);
                if (devExe is not null)
                {
                    return devExe;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the icon location string for File Explorer (<c>Assets\context-menu-icon.ico</c>
    /// or <c>ConvertNow.exe,0</c>) relative to the Shell Extension assembly directory.
    /// </summary>
    public static string? ResolveIconPath()
        => ResolveIconPath(GetAssemblyDirectory());

    /// <summary>
    /// Resolves the icon location string for File Explorer relative to the supplied base directory.
    /// </summary>
    public static string? ResolveIconPath(string? assemblyDirectory)
    {
        if (string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            return null;
        }

        string localIcon = Path.Combine(assemblyDirectory, IconRelativePath);
        if (File.Exists(localIcon))
        {
            return Path.GetFullPath(localIcon);
        }

        string? exePath = ResolveExecutablePath(assemblyDirectory);
        if (exePath is not null)
        {
            string? exeDir = Path.GetDirectoryName(exePath);
            if (!string.IsNullOrWhiteSpace(exeDir))
            {
                string exeAssetIcon = Path.Combine(exeDir, IconRelativePath);
                if (File.Exists(exeAssetIcon))
                {
                    return Path.GetFullPath(exeAssetIcon);
                }
            }
        }

        foreach (string ancestor in EnumerateAncestors(assemblyDirectory, maxLevels: 8))
        {
            string[] candidates =
            [
                Path.Combine(ancestor, "ConvertNow.Package", IconRelativePath),
                Path.Combine(ancestor, "src", "ConvertNow.Package", IconRelativePath),
                Path.Combine(ancestor, "ConvertNow.App", IconRelativePath),
                Path.Combine(ancestor, "src", "ConvertNow.App", IconRelativePath),
            ];

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return exePath is not null ? $"{exePath},0" : null;
    }

    private static string? GetAssemblyDirectory()
    {
        string location = typeof(ClassFactory).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(location))
        {
            string? dir = Path.GetDirectoryName(location);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                return dir;
            }
        }

        return string.IsNullOrWhiteSpace(AppContext.BaseDirectory)
            ? null
            : Path.GetFullPath(AppContext.BaseDirectory);
    }

    private static string[] GetPreferredConfigurations(string assemblyDirectory)
    {
        bool isRelease = assemblyDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase)
            || assemblyDirectory.EndsWith($"{Path.DirectorySeparatorChar}Release", StringComparison.OrdinalIgnoreCase);

        return isRelease ? ["Release", "Debug"] : ["Debug", "Release"];
    }

    private static IEnumerable<string> EnumerateAncestors(string startDirectory, int maxLevels)
    {
        string? current = Path.GetFullPath(startDirectory);
        for (int i = 0; i <= maxLevels && !string.IsNullOrWhiteSpace(current); i++)
        {
            yield return current;
            current = Path.GetDirectoryName(current);
        }
    }

    private static IEnumerable<string> EnumerateAppProjectRoots(string ancestor)
    {
        string direct = Path.Combine(ancestor, "ConvertNow.App");
        if (Directory.Exists(direct))
        {
            yield return direct;
        }

        string underSrc = Path.Combine(ancestor, "src", "ConvertNow.App");
        if (Directory.Exists(underSrc))
        {
            yield return underSrc;
        }
    }

    private static string? FindExecutableInAppBin(string appRoot, string[] configs)
    {
        foreach (string config in configs)
        {
            string[] configDirs =
            [
                Path.Combine(appRoot, "bin", "x64", config),
                Path.Combine(appRoot, "bin", config),
            ];

            foreach (string configDir in configDirs)
            {
                if (!Directory.Exists(configDir))
                {
                    continue;
                }

                string[] preferredRelative =
                [
                    Path.Combine("net10.0-windows10.0.22621.0", "win-x64", ExecutableFileName),
                    Path.Combine("net10.0-windows10.0.22621.0", ExecutableFileName),
                ];

                foreach (string rel in preferredRelative)
                {
                    string candidate = Path.Combine(configDir, rel);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }

                foreach (string tfmDir in Directory.EnumerateDirectories(configDir))
                {
                    string ridCandidate = Path.Combine(tfmDir, "win-x64", ExecutableFileName);
                    if (File.Exists(ridCandidate))
                    {
                        return Path.GetFullPath(ridCandidate);
                    }

                    string directCandidate = Path.Combine(tfmDir, ExecutableFileName);
                    if (File.Exists(directCandidate))
                    {
                        return Path.GetFullPath(directCandidate);
                    }
                }
            }
        }

        return null;
    }
}
