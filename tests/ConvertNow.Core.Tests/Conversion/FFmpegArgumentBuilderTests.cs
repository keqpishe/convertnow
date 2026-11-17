namespace ConvertNow.Core.Tests.Conversion;

public sealed class FFmpegArgumentBuilderTests
{
    [Fact]
    public void Build_All25TargetFormats_IncludeRequiredCommonFlags()
    {
        foreach (MediaFormat target in FormatRegistry.GetAllTargetFormats())
        {
            var job = new ConversionJob(
                InputFilePath: @"C:\Media\input file.mkv",
                OutputFilePath: $@"C:\Media\out.{target.Extension}",
                TargetFormat: target,
                SourceCategory: target.Category);

            IReadOnlyList<string> args = FFmpegArgumentBuilder.Build(job);

            Assert.Equal("-hide_banner", args[0]);
            Assert.Equal("-nostdin", args[1]);
            Assert.Equal("-y", args[2]);
            Assert.Equal("-i", args[3]);
            Assert.Equal(job.InputFilePath, args[4]);
            Assert.Equal("-progress", args[5]);
            Assert.Equal("pipe:1", args[6]);
            Assert.Equal(job.OutputFilePath, args[^1]);
        }
    }

    [Fact]
    public void Build_VideoToAudioAndAudioToAudio_IncludeVnFlag()
    {
        string[] audioTargets = ["mp3", "wav", "flac", "aac", "ogg", "m4a", "opus", "wma"];

        foreach (string ext in audioTargets)
        {
            Assert.True(FormatRegistry.TryGetTargetFormat(ext, out MediaFormat? format));
            var job = new ConversionJob(@"C:\in.mp4", $@"C:\out.{ext}", format, FormatCategory.Video);
            IReadOnlyList<string> args = FFmpegArgumentBuilder.Build(job);

            Assert.Contains("-vn", args);
        }
    }

    [Fact]
    public void Build_ImageTargets_IncludeFramesV1_AndFormatSpecificSafetyOptions()
    {
        string[] imageTargets = ["jpg", "png", "webp", "bmp", "tiff", "ico", "avif"];

        foreach (string ext in imageTargets)
        {
            Assert.True(FormatRegistry.TryGetTargetFormat(ext, out MediaFormat? format));
            var job = new ConversionJob(@"C:\in.gif", $@"C:\out.{ext}", format, FormatCategory.Image);
            IReadOnlyList<string> args = FFmpegArgumentBuilder.Build(job);

            int framesIndex = args.ToList().IndexOf("-frames:v");
            Assert.True(framesIndex >= 0);
            Assert.Equal("1", args[framesIndex + 1]);
        }

        // ICO scales to <= 256x256 and omits -update (rejected by ICO muxer).
        FormatRegistry.TryGetTargetFormat("ico", out MediaFormat? ico);
        IReadOnlyList<string> icoArgs = FFmpegArgumentBuilder.Build(
            new ConversionJob(@"C:\big.png", @"C:\out.ico", ico!, FormatCategory.Image));
        Assert.Contains("scale='min(256,iw)':'min(256,ih)':force_original_aspect_ratio=decrease", icoArgs);
        Assert.DoesNotContain("-update", icoArgs);

        // JPG handles RGBA inputs safely via yuvj420p.
        FormatRegistry.TryGetTargetFormat("jpg", out MediaFormat? jpg);
        IReadOnlyList<string> jpgArgs = FFmpegArgumentBuilder.Build(
            new ConversionJob(@"C:\alpha.png", @"C:\out.jpg", jpg!, FormatCategory.Image));
        Assert.Contains("-pix_fmt", jpgArgs);
        Assert.Contains("yuvj420p", jpgArgs);
        Assert.Contains("-update", jpgArgs);
    }

    [Fact]
    public void Build_M4vTarget_UsesMp4Muxer()
    {
        FormatRegistry.TryGetTargetFormat("m4v", out MediaFormat? m4v);
        IReadOnlyList<string> args = FFmpegArgumentBuilder.Build(
            new ConversionJob(@"C:\in.mp4", @"C:\out.m4v", m4v!, FormatCategory.Video));

        int fIndex = args.ToList().IndexOf("-f");
        Assert.True(fIndex >= 0);
        Assert.Equal("mp4", args[fIndex + 1]);
    }

    [Fact]
    public void Build_UnsupportedTargetFormat_ThrowsNotSupportedException()
    {
        var unsupported = new MediaFormat("gif", "GIF", FormatCategory.Image);
        var job = new ConversionJob(@"C:\in.png", @"C:\out.gif", unsupported, FormatCategory.Image);

        Assert.Throws<NotSupportedException>(() => FFmpegArgumentBuilder.Build(job));
    }
}
