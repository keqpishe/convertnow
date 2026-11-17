using System.Diagnostics;

namespace ConvertNow.Core.Tests.Conversion;

public sealed class FFmpegIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly FFmpegRunner _runner;

    public FFmpegIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cn_ffmpeg_int_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _ffmpegPath = FFmpegLocator.LocateFFmpeg()
            ?? throw new InvalidOperationException("Bundled ffmpeg.exe must exist for integration tests.");
        _ffprobePath = FFmpegLocator.LocateFFprobe()
            ?? throw new InvalidOperationException("Bundled ffprobe.exe must exist for integration tests.");

        _runner = new FFmpegRunner();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_All10VideoTargets_SucceedAndProduceValidStreams()
    {
        string inputVideo = Path.Combine(_tempDir, "source.mp4");
        await GenerateSyntheticVideoAsync(inputVideo, durationSeconds: 0.6, width: 160, height: 120, withAudio: true);

        string[] videoTargets = ["mp4", "avi", "mkv", "mov", "webm", "flv", "wmv", "m4v", "3gp", "ts"];

        foreach (string ext in videoTargets)
        {
            Assert.True(FormatRegistry.TryGetTargetFormat(ext, out MediaFormat? targetFormat));
            string outputPath = OutputNameResolver.ResolveUniqueOutputPath(inputVideo, ext, Path.Combine(_tempDir, "video_out"));
            var job = new ConversionJob(inputVideo, outputPath, targetFormat, FormatCategory.Video);

            ConversionResult result = await _runner.ConvertAsync(job);

            Assert.True(result.Success, $"Conversion to {ext} failed: {result.ErrorMessage}");
            Assert.False(result.IsCancelled);
            Assert.True(File.Exists(result.OutputFilePath), $"Output file missing for {ext}");
            Assert.True(result.OutputFileSizeBytes > 0, $"Empty output for {ext}");

            (int videoStreams, int audioStreams) = await ProbeStreamCountsAsync(result.OutputFilePath);
            Assert.True(videoStreams >= 1, $"Expected >=1 video stream in {ext}");
            Assert.True(audioStreams >= 1, $"Expected >=1 audio stream in {ext}");
        }
    }

    [Fact]
    public async Task ConvertAsync_VideoToAll7AudioTargets_ExtractsAudioOnly()
    {
        string inputVideo = Path.Combine(_tempDir, "source_av.mp4");
        await GenerateSyntheticVideoAsync(inputVideo, durationSeconds: 0.6, width: 160, height: 120, withAudio: true);

        string[] audioTargets = ["mp3", "wav", "flac", "aac", "ogg", "m4a", "opus"];

        foreach (string ext in audioTargets)
        {
            Assert.True(FormatRegistry.TryGetTargetFormat(ext, out MediaFormat? targetFormat));
            string outputPath = OutputNameResolver.ResolveUniqueOutputPath(inputVideo, ext, Path.Combine(_tempDir, "v2a_out"));
            var job = new ConversionJob(inputVideo, outputPath, targetFormat, FormatCategory.Video);

            ConversionResult result = await _runner.ConvertAsync(job);

            Assert.True(result.Success, $"Video->{ext} failed: {result.ErrorMessage}");
            Assert.True(result.OutputFileSizeBytes > 0);

            (int videoStreams, int audioStreams) = await ProbeStreamCountsAsync(result.OutputFilePath);
            Assert.Equal(0, videoStreams);
            Assert.Equal(1, audioStreams);
        }
    }

    [Fact]
    public async Task ConvertAsync_AudioToAll8AudioTargets_IncludingAlbumArtAndSurround_Succeed()
    {
        string rawWav = Path.Combine(_tempDir, "tone.wav");
        await RunFfmpegDirectAsync(["-f", "lavfi", "-i", "sine=frequency=440:duration=0.6", rawWav]);

        // Create an MP3 containing an embedded MJPEG album cover stream to verify -vn strips it cleanly.
        string coverPng = Path.Combine(_tempDir, "cover.png");
        await GenerateSyntheticImageAsync(coverPng, 128, 128, "rgba");
        string mp3WithArt = Path.Combine(_tempDir, "with_art.mp3");
        await RunFfmpegDirectAsync([
            "-i", rawWav, "-i", coverPng,
            "-map", "0:a", "-map", "1:v",
            "-c:a", "libmp3lame", "-q:a", "4",
            "-c:v", "mjpeg", "-disposition:v:0", "attached_pic",
            mp3WithArt,
        ]);

        // Also test 5.1 surround WAV -> WMA and MP3 to verify downmixing works.
        string surroundWav = Path.Combine(_tempDir, "surround51.wav");
        await RunFfmpegDirectAsync(["-f", "lavfi", "-i", "anoisesrc=duration=0.4", "-ac", "6", surroundWav]);

        string[] audioTargets = ["mp3", "wav", "flac", "aac", "ogg", "m4a", "opus", "wma"];

        foreach (string ext in audioTargets)
        {
            Assert.True(FormatRegistry.TryGetTargetFormat(ext, out MediaFormat? targetFormat));
            string outputPath = OutputNameResolver.ResolveUniqueOutputPath(mp3WithArt, ext, Path.Combine(_tempDir, "a2a_out"));
            var job = new ConversionJob(mp3WithArt, outputPath, targetFormat, FormatCategory.Audio);

            ConversionResult result = await _runner.ConvertAsync(job);

            Assert.True(result.Success, $"Audio(with art)->{ext} failed: {result.ErrorMessage}");
            Assert.True(result.OutputFileSizeBytes > 0);

            (int videoStreams, int audioStreams) = await ProbeStreamCountsAsync(result.OutputFilePath);
            Assert.Equal(0, videoStreams);
            Assert.Equal(1, audioStreams);
        }

        // Verify 5.1 surround -> WMA and MP3
        foreach (string ext in new[] { "wma", "mp3" })
        {
            FormatRegistry.TryGetTargetFormat(ext, out MediaFormat? targetFormat);
            string outPath = Path.Combine(_tempDir, $"surround_out.{ext}");
            ConversionResult result = await _runner.ConvertAsync(
                new ConversionJob(surroundWav, outPath, targetFormat!, FormatCategory.Audio));
            Assert.True(result.Success, $"5.1->{ext} failed: {result.ErrorMessage}");
        }
    }

    [Fact]
    public async Task ConvertAsync_ImageToAll7ImageTargets_HandlesRgbaLargeAndAnimatedGif()
    {
        // 400x300 RGBA PNG with alpha transparency tests:
        // - PNG RGBA -> JPG
        // - >256px -> ICO downscaling
        // - Odd dimensions -> AVIF
        string rgbaPng = Path.Combine(_tempDir, "alpha_401x301.png");
        await GenerateSyntheticImageAsync(rgbaPng, 401, 301, "rgba");

        string animGif = Path.Combine(_tempDir, "animated.gif");
        await RunFfmpegDirectAsync(["-f", "lavfi", "-i", "testsrc=duration=0.4:size=120x90:rate=10", animGif]);

        string[] imageTargets = ["jpg", "png", "webp", "bmp", "tiff", "ico", "avif"];

        foreach (string ext in imageTargets)
        {
            Assert.True(FormatRegistry.TryGetTargetFormat(ext, out MediaFormat? targetFormat));

            string outFromRgba = OutputNameResolver.ResolveUniqueOutputPath(rgbaPng, ext, Path.Combine(_tempDir, "img_rgba"));
            ConversionResult rgbaResult = await _runner.ConvertAsync(
                new ConversionJob(rgbaPng, outFromRgba, targetFormat, FormatCategory.Image));

            Assert.True(rgbaResult.Success, $"RGBA PNG->{ext} failed: {rgbaResult.ErrorMessage}");
            Assert.True(rgbaResult.OutputFileSizeBytes > 0);

            string outFromGif = OutputNameResolver.ResolveUniqueOutputPath(animGif, ext, Path.Combine(_tempDir, "img_gif"));
            ConversionResult gifResult = await _runner.ConvertAsync(
                new ConversionJob(animGif, outFromGif, targetFormat, FormatCategory.Image));

            Assert.True(gifResult.Success, $"Animated GIF->{ext} failed: {gifResult.ErrorMessage}");
            Assert.True(gifResult.OutputFileSizeBytes > 0);
        }

        // Verify ICO dimensions were scaled down to fit within 256x256
        string icoPath = Path.Combine(_tempDir, "img_rgba", "alpha_401x301.ico");
        (int icoWidth, int icoHeight) = await ProbeVideoDimensionsAsync(icoPath);
        Assert.InRange(icoWidth, 1, 256);
        Assert.InRange(icoHeight, 1, 256);
    }

    [Fact]
    public async Task ConvertAsync_OddDimensionAndSilentVideos_ConvertCleanly()
    {
        string oddSilentMkv = Path.Combine(_tempDir, "odd_silent.mkv");
        await RunFfmpegDirectAsync([
            "-f", "lavfi", "-i", "testsrc=duration=0.4:size=161x121:rate=25",
            "-c:v", "libx264rgb",
            oddSilentMkv,
        ]);

        foreach (string ext in new[] { "mp4", "flv", "wmv", "avi" })
        {
            FormatRegistry.TryGetTargetFormat(ext, out MediaFormat? targetFormat);
            string outPath = Path.Combine(_tempDir, $"odd_out.{ext}");
            ConversionResult result = await _runner.ConvertAsync(
                new ConversionJob(oddSilentMkv, outPath, targetFormat!, FormatCategory.Video));

            Assert.True(result.Success, $"Odd/silent video->{ext} failed: {result.ErrorMessage}");
            Assert.True(result.OutputFileSizeBytes > 0);
        }
    }

    [Fact]
    public async Task ConvertAsync_UnicodePathsAndPercentInImageName_Succeed()
    {
        string unicodeDir = Path.Combine(_tempDir, "Видео Тест 日本語 🎬");
        Directory.CreateDirectory(unicodeDir);

        string inputVideo = Path.Combine(unicodeDir, "Клип тест 🎵.mp4");
        await GenerateSyntheticVideoAsync(inputVideo, durationSeconds: 0.5, width: 160, height: 120, withAudio: true);

        FormatRegistry.TryGetTargetFormat("avi", out MediaFormat? avi);
        string outputVideo = OutputNameResolver.ResolveUniqueOutputPath(inputVideo, "avi");
        ConversionResult videoResult = await _runner.ConvertAsync(
            new ConversionJob(inputVideo, outputVideo, avi!, FormatCategory.Video));

        Assert.True(videoResult.Success, videoResult.ErrorMessage);
        Assert.True(File.Exists(outputVideo));

        string pngPath = Path.Combine(unicodeDir, "фото%20тест.png");
        await GenerateSyntheticImageAsync(pngPath, 100, 100, "rgba");
        FormatRegistry.TryGetTargetFormat("jpg", out MediaFormat? jpg);
        string jpgOut = OutputNameResolver.ResolveUniqueOutputPath(pngPath, "jpg");
        ConversionResult imageResult = await _runner.ConvertAsync(
            new ConversionJob(pngPath, jpgOut, jpg!, FormatCategory.Image));

        Assert.True(imageResult.Success, imageResult.ErrorMessage);
        Assert.True(File.Exists(jpgOut));
    }

    [Fact]
    public async Task ProbeDurationAsync_And_ProgressReporting_ReportAccurateProgress()
    {
        string inputVideo = Path.Combine(_tempDir, "progress_src.mp4");
        await GenerateSyntheticVideoAsync(inputVideo, durationSeconds: 2.0, width: 320, height: 240, withAudio: true);

        TimeSpan? duration = await _runner.ProbeDurationAsync(inputVideo);
        Assert.NotNull(duration);
        Assert.InRange(duration.Value.TotalSeconds, 1.8, 2.2);

        Assert.Null(await _runner.ProbeDurationAsync(Path.Combine(_tempDir, "missing.mp4")));

        var reports = new List<ConversionProgress>();
        var progressLock = new Lock();
        var progress = new SynchronousProgress<ConversionProgress>(p =>
        {
            lock (progressLock)
            {
                reports.Add(p);
            }
        });

        FormatRegistry.TryGetTargetFormat("mkv", out MediaFormat? mkv);
        string outPath = Path.Combine(_tempDir, "progress_out.mkv");
        ConversionResult result = await _runner.ConvertAsync(
            new ConversionJob(inputVideo, outPath, mkv!, FormatCategory.Video),
            progress);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(reports.Count >= 2, $"Expected multiple progress updates, got {reports.Count}");
        Assert.Equal(100.0, reports[^1].Percentage);
        Assert.False(reports[^1].IsIndeterminate);
        Assert.All(reports, r => Assert.InRange(r.Percentage, 0.0, 100.0));
    }

    [Fact]
    public async Task ConvertAsync_Cancellation_KillsProcessAndDeletesPartialOutput()
    {
        // Generate a ~10-second 640x480 video and convert to VP9 WebM so cancellation reliably catches it mid-flight.
        string inputVideo = Path.Combine(_tempDir, "cancel_src.mp4");
        await GenerateSyntheticVideoAsync(inputVideo, durationSeconds: 10.0, width: 640, height: 480, withAudio: true);

        FormatRegistry.TryGetTargetFormat("webm", out MediaFormat? webm);
        string outPath = Path.Combine(_tempDir, "cancelled_out.webm");

        using var cts = new CancellationTokenSource();
        int updateCount = 0;
        var progress = new SynchronousProgress<ConversionProgress>(_ =>
        {
            if (Interlocked.Increment(ref updateCount) >= 2)
            {
                cts.Cancel();
            }
        });

        // Safety timeout in case conversion were unexpectedly faster than 2 progress blocks.
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        ConversionResult result = await _runner.ConvertAsync(
            new ConversionJob(inputVideo, outPath, webm!, FormatCategory.Video),
            progress,
            cts.Token);

        Assert.False(result.Success);
        Assert.True(result.IsCancelled);
        Assert.Equal(0, result.OutputFileSizeBytes);
        Assert.False(File.Exists(outPath), "Partial output file should be deleted after cancellation.");
        Assert.True(File.Exists(inputVideo), "Input file must never be deleted.");
    }

    [Fact]
    public async Task ConvertAsync_CorruptedInputFile_ReturnsFriendlyErrorAndNoPartialOutput()
    {
        string corruptPath = Path.Combine(_tempDir, "corrupt.mp4");
        await File.WriteAllBytesAsync(corruptPath, Enumerable.Range(1, 200).Select(i => (byte)i).ToArray());

        FormatRegistry.TryGetTargetFormat("avi", out MediaFormat? avi);
        string outPath = Path.Combine(_tempDir, "corrupt_out.avi");

        ConversionResult result = await _runner.ConvertAsync(
            new ConversionJob(corruptPath, outPath, avi!, FormatCategory.Video));

        Assert.False(result.Success);
        Assert.False(result.IsCancelled);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.False(File.Exists(outPath));
    }

    private async Task GenerateSyntheticVideoAsync(string outputPath, double durationSeconds, int width, int height, bool withAudio)
    {
        var args = new List<string>
        {
            "-f", "lavfi",
            "-i", $"testsrc=duration={durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}:size={width}x{height}:rate=25",
        };

        if (withAudio)
        {
            args.AddRange([
                "-f", "lavfi",
                "-i", $"sine=frequency=440:duration={durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            ]);
        }

        args.AddRange(["-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p"]);
        if (withAudio)
        {
            args.AddRange(["-c:a", "aac"]);
        }

        args.Add(outputPath);
        await RunFfmpegDirectAsync(args);
    }

    private async Task GenerateSyntheticImageAsync(string outputPath, int width, int height, string pixelFormat)
    {
        await RunFfmpegDirectAsync([
            "-f", "lavfi",
            "-i", $"color=c=red@0.5:size={width}x{height}",
            "-vf", $"format={pixelFormat}",
            "-frames:v", "1",
            "-update", "1",
            outputPath,
        ]);
    }

    private async Task RunFfmpegDirectAsync(IReadOnlyList<string> extraArgs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-nostdin");
        psi.ArgumentList.Add("-y");
        foreach (string arg in extraArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start ffmpeg.exe");
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Synthetic media generation failed (exit {process.ExitCode}): {stderr}");
        }
    }

    private async Task<(int VideoStreams, int AudioStreams)> ProbeStreamCountsAsync(string filePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-show_entries");
        psi.ArgumentList.Add("stream=codec_type");
        psi.ArgumentList.Add("-of");
        psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        psi.ArgumentList.Add(filePath);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start ffprobe.exe");
        string stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        string[] lines = stdout.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (
            lines.Count(l => string.Equals(l, "video", StringComparison.OrdinalIgnoreCase)),
            lines.Count(l => string.Equals(l, "audio", StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<(int Width, int Height)> ProbeVideoDimensionsAsync(string filePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-select_streams");
        psi.ArgumentList.Add("v:0");
        psi.ArgumentList.Add("-show_entries");
        psi.ArgumentList.Add("stream=width,height");
        psi.ArgumentList.Add("-of");
        psi.ArgumentList.Add("csv=s=x:p=0");
        psi.ArgumentList.Add(filePath);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start ffprobe.exe");
        string stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        string[] parts = stdout.Trim().Split('x');
        Assert.Equal(2, parts.Length);
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
