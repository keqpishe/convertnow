namespace ConvertNow.Core.Tests.Conversion;

public sealed class FFmpegProgressParserTests
{
    [Fact]
    public void ParseProgressLine_OutTimeUsAndSpeed_CalculatesPercentageAndEta()
    {
        var parser = new FFmpegProgressParser(TimeSpan.FromSeconds(100));

        Assert.Null(parser.ParseProgressLine("frame=750"));
        Assert.Null(parser.ParseProgressLine("out_time_us=25000000"));
        Assert.Null(parser.ParseProgressLine("out_time_ms=25000000"));
        Assert.Null(parser.ParseProgressLine("out_time=00:00:25.000000"));
        Assert.Null(parser.ParseProgressLine("speed=2.5x"));

        ConversionProgress? progress = parser.ParseProgressLine("progress=continue");
        Assert.NotNull(progress);
        Assert.False(progress.IsIndeterminate);
        Assert.Equal(25.0, progress.Percentage, precision: 3);
        Assert.Equal(TimeSpan.FromSeconds(25), progress.ProcessedTime);
        Assert.Equal(TimeSpan.FromSeconds(100), progress.TotalDuration);
        Assert.Equal("2.5x", progress.SpeedText);
        // 75s remaining at 2.5x speed => 30s ETA
        Assert.Equal(TimeSpan.FromSeconds(30), progress.EstimatedRemaining);
    }

    [Fact]
    public void ParseProgressLine_OutTimeMsAlone_TreatsValueAsMicrosecondsLikeFFmpeg()
    {
        var parser = new FFmpegProgressParser(TimeSpan.FromSeconds(10));

        parser.ParseProgressLine("out_time_ms=5000000");
        ConversionProgress? progress = parser.ParseProgressLine("progress=continue");

        Assert.NotNull(progress);
        Assert.Equal(TimeSpan.FromSeconds(5), progress.ProcessedTime);
        Assert.Equal(50.0, progress.Percentage, precision: 3);
    }

    [Fact]
    public void ParseProgressLine_ProgressEnd_Reaches100PercentAndZeroEta()
    {
        var parser = new FFmpegProgressParser(TimeSpan.FromSeconds(10));

        parser.ParseProgressLine("out_time_us=9900000");
        ConversionProgress? final = parser.ParseProgressLine("progress=end");

        Assert.NotNull(final);
        Assert.Equal(100.0, final.Percentage);
        Assert.Equal(TimeSpan.Zero, final.EstimatedRemaining);
        Assert.False(final.IsIndeterminate);
    }

    [Fact]
    public void ParseProgressLine_UnknownDuration_IsIndeterminateUntilEnd()
    {
        var parser = new FFmpegProgressParser(totalDuration: null);

        parser.ParseProgressLine("out_time_us=1000000");
        parser.ParseProgressLine("speed=N/A");
        ConversionProgress? mid = parser.ParseProgressLine("progress=continue");

        Assert.NotNull(mid);
        Assert.True(mid.IsIndeterminate);
        Assert.Null(mid.EstimatedRemaining);

        ConversionProgress? end = parser.ParseProgressLine("progress=end");
        Assert.NotNull(end);
        Assert.False(end.IsIndeterminate);
        Assert.Equal(100.0, end.Percentage);
    }

    [Fact]
    public void ParseStderrLine_ExtractsDurationAndFallbackTime_AndDefersOncePipeActive()
    {
        var parser = new FFmpegProgressParser();

        Assert.Null(parser.ParseStderrLine("  Duration: 00:01:40.00, start: 0.000000, bitrate: 1200 kb/s"));
        Assert.Equal(TimeSpan.FromSeconds(100), parser.TotalDuration);

        ConversionProgress? stderrProgress = parser.ParseStderrLine(
            "frame=  250 fps= 50 q=23.0 size=   512kB time=00:00:25.00 bitrate= 167.8kbits/s speed=2.0x");
        Assert.NotNull(stderrProgress);
        Assert.Equal(25.0, stderrProgress.Percentage, precision: 3);
        Assert.Equal("2.0x", stderrProgress.SpeedText);

        // Once -progress pipe:1 reports, stderr progress lines are ignored to avoid duplicate updates.
        parser.ParseProgressLine("out_time_us=50000000");
        Assert.NotNull(parser.ParseProgressLine("progress=continue"));
        Assert.Null(parser.ParseStderrLine("frame=  300 fps= 50 q=23.0 time=00:00:30.00 speed=2.0x"));
    }

    [Theory]
    [InlineData("00:05:30.50", 330.5)]
    [InlineData("25:00:01.250000", 25 * 3600 + 1.25)]
    public void TryParseFFmpegTime_ParsesStandardAndLongDurations(string raw, double expectedSeconds)
    {
        Assert.True(FFmpegProgressParser.TryParseFFmpegTime(raw, out TimeSpan parsed));
        Assert.Equal(expectedSeconds, parsed.TotalSeconds, precision: 5);
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("-00:00:01.00")]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseFFmpegTime_RejectsInvalidOrNegativeValues(string? raw)
    {
        Assert.False(FFmpegProgressParser.TryParseFFmpegTime(raw, out _));
    }

    [Fact]
    public void ExtractErrorMessage_ExtractsFriendlyReasonFromRealisticStderr()
    {
        string stderr = """
            [in#0 @ 0000020f3bf425c0] Format mov,mp4,m4a detected only with low score of 1, misdetection possible!
            [in#0 @ 0000020f3bf425c0] moov atom not found
            [in#0 @ 0000020f3bf79bc0] Error opening input: Invalid data found when processing input
            Error opening input file C:\temp\corrupt.mp4.
            Error opening input files: Invalid data found when processing input
            """;

        string extracted = FFmpegProgressParser.ExtractErrorMessage(stderr);
        Assert.Equal("Invalid data found when processing input", extracted);

        Assert.Equal(
            "FFmpeg exited with an unknown error.",
            FFmpegProgressParser.ExtractErrorMessage(string.Empty));
    }
}
