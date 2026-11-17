namespace ConvertNow.Core.Tests.Conversion;

public sealed class FFmpegLocatorTests
{
    [Fact]
    public void LocateFFmpeg_And_LocateFFprobe_FindBundledRepoBinaries()
    {
        string? ffmpeg = FFmpegLocator.LocateFFmpeg();
        string? ffprobe = FFmpegLocator.LocateFFprobe();

        Assert.NotNull(ffmpeg);
        Assert.NotNull(ffprobe);
        Assert.True(File.Exists(ffmpeg));
        Assert.True(File.Exists(ffprobe));
        Assert.Equal("ffmpeg.exe", Path.GetFileName(ffmpeg), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("ffprobe.exe", Path.GetFileName(ffprobe), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocateExecutable_PrefersOverrideOverBundledBaseAndPath()
    {
        string overrideExe = Path.GetFullPath(@"C:\Override\ffmpeg.exe");
        string bundledExe = Path.GetFullPath(@"C:\App\tools\ffmpeg\ffmpeg.exe");
        string baseExe = Path.GetFullPath(@"C:\App\ffmpeg.exe");
        string pathExe = Path.GetFullPath(@"C:\SysTools\ffmpeg.exe");

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            overrideExe,
            bundledExe,
            baseExe,
            pathExe,
        };

        string? result = FFmpegLocator.LocateExecutable(
            "ffmpeg.exe",
            overrideDirectory: @"C:\Override",
            baseDirectory: @"C:\App\",
            pathEnvironmentVariable: @"C:\SysTools",
            fileExists: p => existing.Contains(Path.GetFullPath(p)));

        Assert.Equal(overrideExe, result);
    }

    [Fact]
    public void LocateExecutable_SearchesBaseToolsThenBaseThenAncestorsUpTo6LevelsThenPath()
    {
        string baseToolsExe = Path.GetFullPath(@"C:\a\b\c\d\e\f\g\tools\ffmpeg\ffmpeg.exe");
        string baseExe = Path.GetFullPath(@"C:\a\b\c\d\e\f\g\ffmpeg.exe");
        string level6Exe = Path.GetFullPath(@"C:\a\tools\ffmpeg\ffmpeg.exe");
        string level7Exe = Path.GetFullPath(@"C:\tools\ffmpeg\ffmpeg.exe");
        string pathExe = Path.GetFullPath(@"D:\PathBin\ffmpeg.exe");

        string baseDir = @"C:\a\b\c\d\e\f\g\";

        // Tier 2: Base/tools/ffmpeg
        Assert.Equal(baseToolsExe, FFmpegLocator.LocateExecutable(
            "ffmpeg.exe", null, baseDir, @"D:\PathBin",
            p => Path.GetFullPath(p).Equals(baseToolsExe, StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFullPath(p).Equals(baseExe, StringComparison.OrdinalIgnoreCase)));

        // Tier 3: Base
        Assert.Equal(baseExe, FFmpegLocator.LocateExecutable(
            "ffmpeg.exe", null, baseDir, @"D:\PathBin",
            p => Path.GetFullPath(p).Equals(baseExe, StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFullPath(p).Equals(level6Exe, StringComparison.OrdinalIgnoreCase)));

        // Tier 4: Ancestor level 6 succeeds
        Assert.Equal(level6Exe, FFmpegLocator.LocateExecutable(
            "ffmpeg.exe", null, baseDir, @"D:\PathBin",
            p => Path.GetFullPath(p).Equals(level6Exe, StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFullPath(p).Equals(pathExe, StringComparison.OrdinalIgnoreCase)));

        // Tier 4 depth limit: Level 7 is ignored, falls back to PATH
        Assert.Equal(pathExe, FFmpegLocator.LocateExecutable(
            "ffmpeg.exe", null, baseDir, $"\"D:\\PathBin\"{Path.PathSeparator} ",
            p => Path.GetFullPath(p).Equals(level7Exe, StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFullPath(p).Equals(pathExe, StringComparison.OrdinalIgnoreCase)));

        // Not found anywhere
        Assert.Null(FFmpegLocator.LocateExecutable(
            "ffmpeg.exe", null, baseDir, @"D:\PathBin",
            _ => false));
    }
}
