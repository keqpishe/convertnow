namespace ConvertNow.Core.Tests.FileUtils;

public sealed class OutputNameResolverTests
{
    [Fact]
    public void ResolveUniqueOutputPath_DifferentExtension_NoConflict_ReturnsBaseNameWithNewExtension()
    {
        string result = OutputNameResolver.ResolveUniqueOutputPath(
            @"C:\Videos\video.mp4",
            "avi",
            fileExists: _ => false);

        Assert.Equal(@"C:\Videos\video.avi", result);
    }

    [Fact]
    public void ResolveUniqueOutputPath_SequentialConflicts_AppendsIncrementingNumericSuffix()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\Videos\video.avi",
            @"C:\Videos\video (1).avi",
            @"C:\Videos\video (2).avi",
        };

        string result = OutputNameResolver.ResolveUniqueOutputPath(
            @"C:\Videos\video.mp4",
            ".AVI",
            fileExists: existing.Contains);

        Assert.Equal(@"C:\Videos\video (3).avi", result);
    }

    [Fact]
    public void ResolveUniqueOutputPath_SameExtension_NeverClobbersInputEvenWhenFileExistsReturnsFalse()
    {
        string result = OutputNameResolver.ResolveUniqueOutputPath(
            @"C:\Videos\Video.MP4",
            "mp4",
            fileExists: _ => false);

        Assert.Equal(@"C:\Videos\Video (1).mp4", result);
    }

    [Fact]
    public void ResolveUniqueOutputPath_SameExtension_DifferentDirectory_AllowsBaseFileName()
    {
        string result = OutputNameResolver.ResolveUniqueOutputPath(
            @"C:\Videos\video.mp4",
            "mp4",
            customOutputDirectory: @"D:\Converted",
            fileExists: _ => false);

        Assert.Equal(@"D:\Converted\video.mp4", result);
    }

    [Fact]
    public void ResolveUniqueOutputPath_CustomDirectory_WithConflicts_AppendsSuffix()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"D:\Converted\мой фильм 🎬.mp3",
        };

        string result = OutputNameResolver.ResolveUniqueOutputPath(
            @"C:\Видео\мой фильм 🎬.mp4",
            "mp3",
            customOutputDirectory: @"D:\Converted",
            fileExists: existing.Contains);

        Assert.Equal(@"D:\Converted\мой фильм 🎬 (1).mp3", result);
    }

    [Fact]
    public void ResolveUniqueOutputPath_RealFileSystem_DetectsExistingFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"cn_nametest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string inputPath = Path.Combine(tempDir, "sample.mp4");
            string conflictPath = Path.Combine(tempDir, "sample.mkv");
            File.WriteAllBytes(inputPath, [1]);
            File.WriteAllBytes(conflictPath, [2]);

            string resolved = OutputNameResolver.ResolveUniqueOutputPath(inputPath, "mkv");
            Assert.Equal(Path.Combine(tempDir, "sample (1).mkv"), resolved);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("", "mp4")]
    [InlineData("   ", "mp4")]
    [InlineData("C:\\Videos\\video.mp4", "")]
    [InlineData("C:\\Videos\\video.mp4", ".")]
    [InlineData("C:\\Videos\\", "mp4")]
    public void ResolveUniqueOutputPath_InvalidArguments_ThrowArgumentException(string input, string target)
    {
        Assert.Throws<ArgumentException>(() => OutputNameResolver.ResolveUniqueOutputPath(input, target));
    }
}
