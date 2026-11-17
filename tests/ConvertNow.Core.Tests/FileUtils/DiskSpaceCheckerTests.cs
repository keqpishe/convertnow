namespace ConvertNow.Core.Tests.FileUtils;

public sealed class DiskSpaceCheckerTests : IDisposable
{
    private readonly string _tempDir;

    public DiskSpaceCheckerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cn_disktest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Evaluate_SufficientSpace_NoWarning()
    {
        DiskSpaceCheckResult result = DiskSpaceChecker.Evaluate(inputFileSizeBytes: 1_000_000, availableBytes: 1_500_000);

        Assert.False(result.HasCriticalLowSpace);
        Assert.False(result.ShowLowSpaceWarning);
        Assert.Equal(1_500_000, result.RecommendedBytes);
        Assert.Equal(1_500_000, result.AvailableBytes);
        Assert.Equal(1_000_000, result.InputFileSizeBytes);
        Assert.Null(result.WarningMessage);
    }

    [Fact]
    public void Evaluate_BelowRecommendedButAboveInputSize_ShowsWarningOnly()
    {
        DiskSpaceCheckResult result = DiskSpaceChecker.Evaluate(inputFileSizeBytes: 1_000_000, availableBytes: 1_200_000);

        Assert.False(result.HasCriticalLowSpace);
        Assert.True(result.ShowLowSpaceWarning);
        Assert.Equal(1_500_000, result.RecommendedBytes);
        Assert.NotNull(result.WarningMessage);
        Assert.Contains("Low disk space", result.WarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_BelowInputSize_ShowsCriticalLowSpace()
    {
        DiskSpaceCheckResult result = DiskSpaceChecker.Evaluate(inputFileSizeBytes: 1_000_000, availableBytes: 800_000);

        Assert.True(result.HasCriticalLowSpace);
        Assert.True(result.ShowLowSpaceWarning);
        Assert.NotNull(result.WarningMessage);
        Assert.Contains("Critically low disk space", result.WarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ZeroAvailableBytes_ShowsCriticalLowSpace()
    {
        DiskSpaceCheckResult result = DiskSpaceChecker.Evaluate(inputFileSizeBytes: 0, availableBytes: 0);

        Assert.True(result.HasCriticalLowSpace);
        Assert.True(result.ShowLowSpaceWarning);
        Assert.NotNull(result.WarningMessage);
    }

    [Fact]
    public void Evaluate_TenGigabyteFile_DoesNotOverflow()
    {
        long tenGb = 10L * 1024 * 1024 * 1024;
        long fifteenGb = 15L * 1024 * 1024 * 1024;

        DiskSpaceCheckResult result = DiskSpaceChecker.Evaluate(inputFileSizeBytes: tenGb, availableBytes: fifteenGb);

        Assert.Equal(fifteenGb, result.RecommendedBytes);
        Assert.False(result.ShowLowSpaceWarning);
    }

    [Fact]
    public void CheckDiskSpace_RealLocalFileAndNonExistentNestedSubdirectory_ReturnsAvailableSpace()
    {
        string inputFile = Path.Combine(_tempDir, "sample.bin");
        File.WriteAllBytes(inputFile, new byte[4096]);
        string futureSubDir = Path.Combine(_tempDir, "nested", "future", "output");

        DiskSpaceCheckResult result = DiskSpaceChecker.CheckDiskSpace(inputFile, futureSubDir);

        Assert.Equal(4096, result.InputFileSizeBytes);
        Assert.Equal(6144, result.RecommendedBytes);
        Assert.True(result.AvailableBytes > 0);
        Assert.False(result.HasCriticalLowSpace);
    }

    [Fact]
    public void CheckDiskSpace_UnreachableUncShare_FailsOpenWithoutThrowing()
    {
        string inputFile = Path.Combine(_tempDir, "sample.bin");
        File.WriteAllBytes(inputFile, new byte[1024]);

        DiskSpaceCheckResult result = DiskSpaceChecker.CheckDiskSpace(
            inputFile,
            @"\\127.0.0.1\nonexistent-convertnow-share\folder");

        Assert.Equal(-1, result.AvailableBytes);
        Assert.False(result.HasCriticalLowSpace);
        Assert.False(result.ShowLowSpaceWarning);
        Assert.Null(result.WarningMessage);
    }

    [Fact]
    public void CheckDiskSpace_MissingInputFile_ThrowsFileNotFoundException()
    {
        string missing = Path.Combine(_tempDir, "does_not_exist.mp4");
        Assert.Throws<FileNotFoundException>(() => DiskSpaceChecker.CheckDiskSpace(missing, _tempDir));
    }
}
