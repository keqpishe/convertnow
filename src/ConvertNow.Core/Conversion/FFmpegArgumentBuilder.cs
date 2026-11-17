namespace ConvertNow.Core;

/// <summary>
/// Builds FFmpeg CLI argument lists with sensible defaults for every supported target format.
/// </summary>
public static class FFmpegArgumentBuilder
{
    private const string EvenDimensionsScaleFilter = "scale=trunc(iw/2)*2:trunc(ih/2)*2";
    private const string IcoMaxDimensionsScaleFilter = "scale='min(256,iw)':'min(256,ih)':force_original_aspect_ratio=decrease";

    /// <summary>
    /// Builds an argument list suitable for <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>.
    /// </summary>
    /// <param name="job">The conversion job specification.</param>
    /// <returns>An ordered read-only list of CLI arguments (without the executable path).</returns>
    public static IReadOnlyList<string> Build(ConversionJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(job.InputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(job.OutputFilePath);
        ArgumentNullException.ThrowIfNull(job.TargetFormat);

        string targetExt = FormatRegistry.NormalizeExtension(job.TargetFormat.Extension);
        if (targetExt.Length == 0)
        {
            throw new ArgumentException("Target format extension cannot be empty.", nameof(job));
        }

        var args = new List<string>(24)
        {
            "-hide_banner",
            "-nostdin",
            "-y",
            "-i",
            job.InputFilePath,
            "-progress",
            "pipe:1",
        };

        AppendTargetArguments(args, targetExt);

        args.Add(job.OutputFilePath);
        return args;
    }

    private static void AppendTargetArguments(List<string> args, string targetExt)
    {
        switch (targetExt)
        {
            // Video targets (10)
            case "mp4":
            case "mov":
                AppendCommonVideoSafety(args);
                args.AddRange([
                    "-c:v", "libx264",
                    "-preset", "medium",
                    "-crf", "23",
                    "-pix_fmt", "yuv420p",
                    "-c:a", "aac",
                    "-b:a", "192k",
                    "-movflags", "+faststart",
                ]);
                break;

            case "m4v":
                AppendCommonVideoSafety(args);
                args.AddRange([
                    "-f", "mp4",
                    "-c:v", "libx264",
                    "-preset", "medium",
                    "-crf", "23",
                    "-pix_fmt", "yuv420p",
                    "-c:a", "aac",
                    "-b:a", "192k",
                    "-movflags", "+faststart",
                ]);
                break;

            case "mkv":
            case "ts":
                AppendCommonVideoSafety(args);
                args.AddRange([
                    "-c:v", "libx264",
                    "-preset", "medium",
                    "-crf", "23",
                    "-pix_fmt", "yuv420p",
                    "-c:a", "aac",
                    "-b:a", "192k",
                ]);
                break;

            case "avi":
                AppendCommonVideoSafety(args);
                args.AddRange([
                    "-c:v", "mpeg4",
                    "-q:v", "4",
                    "-pix_fmt", "yuv420p",
                    "-c:a", "libmp3lame",
                    "-ac", "2",
                    "-b:a", "192k",
                ]);
                break;

            case "webm":
                AppendCommonVideoSafety(args);
                args.AddRange([
                    "-c:v", "libvpx-vp9",
                    "-crf", "30",
                    "-b:v", "0",
                    "-deadline", "good",
                    "-cpu-used", "4",
                    "-row-mt", "1",
                    "-pix_fmt", "yuv420p",
                    "-c:a", "libopus",
                    "-b:a", "128k",
                ]);
                break;

            case "flv":
                AppendCommonVideoSafety(args);
                args.AddRange([
                    "-c:v", "libx264",
                    "-preset", "medium",
                    "-crf", "23",
                    "-pix_fmt", "yuv420p",
                    "-c:a", "aac",
                    "-ar", "44100",
                    "-ac", "2",
                    "-b:a", "128k",
                ]);
                break;

            case "wmv":
                AppendCommonVideoSafety(args);
                args.AddRange([
                    "-c:v", "wmv2",
                    "-q:v", "4",
                    "-pix_fmt", "yuv420p",
                    "-c:a", "wmav2",
                    "-ac", "2",
                    "-ar", "48000",
                    "-b:a", "192k",
                ]);
                break;

            case "3gp":
                AppendCommonVideoSafety(args);
                args.AddRange([
                    "-c:v", "libx264",
                    "-preset", "medium",
                    "-crf", "23",
                    "-pix_fmt", "yuv420p",
                    "-c:a", "aac",
                    "-ac", "2",
                    "-b:a", "128k",
                ]);
                break;

            // Audio targets (8)
            case "mp3":
                args.AddRange(["-vn", "-sn", "-dn", "-c:a", "libmp3lame", "-q:a", "2", "-ac", "2"]);
                break;

            case "wav":
                args.AddRange(["-vn", "-sn", "-dn", "-c:a", "pcm_s16le"]);
                break;

            case "flac":
                args.AddRange(["-vn", "-sn", "-dn", "-c:a", "flac"]);
                break;

            case "aac":
                args.AddRange(["-vn", "-sn", "-dn", "-c:a", "aac", "-b:a", "192k"]);
                break;

            case "m4a":
                args.AddRange(["-vn", "-sn", "-dn", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"]);
                break;

            case "ogg":
                args.AddRange(["-vn", "-sn", "-dn", "-c:a", "libvorbis", "-q:a", "5"]);
                break;

            case "opus":
                args.AddRange(["-vn", "-sn", "-dn", "-c:a", "libopus", "-b:a", "128k"]);
                break;

            case "wma":
                args.AddRange(["-vn", "-sn", "-dn", "-c:a", "wmav2", "-ac", "2", "-ar", "48000", "-b:a", "192k"]);
                break;

            // Image targets (7)
            case "jpg":
                args.AddRange(["-an", "-sn", "-dn", "-frames:v", "1", "-update", "1", "-q:v", "2", "-pix_fmt", "yuvj420p"]);
                break;

            case "png":
            case "bmp":
            case "tiff":
                args.AddRange(["-an", "-sn", "-dn", "-frames:v", "1", "-update", "1"]);
                break;

            case "webp":
                args.AddRange(["-an", "-sn", "-dn", "-frames:v", "1", "-c:v", "libwebp", "-quality", "85"]);
                break;

            case "ico":
                args.AddRange(["-an", "-sn", "-dn", "-frames:v", "1", "-vf", IcoMaxDimensionsScaleFilter]);
                break;

            case "avif":
                args.AddRange([
                    "-an", "-sn", "-dn",
                    "-frames:v", "1",
                    "-c:v", "libaom-av1",
                    "-still-picture", "1",
                    "-crf", "30",
                    "-cpu-used", "4",
                    "-pix_fmt", "yuv420p",
                ]);
                break;

            default:
                throw new NotSupportedException($"Target format '{targetExt}' is not supported.");
        }
    }

    private static void AppendCommonVideoSafety(List<string> args)
    {
        args.Add("-sn");
        args.Add("-dn");
        args.Add("-vf");
        args.Add(EvenDimensionsScaleFilter);
    }
}
