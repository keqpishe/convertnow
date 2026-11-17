using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ConvertNow.Core;

/// <summary>
/// Abstraction for probing media duration and executing FFmpeg conversion jobs.
/// </summary>
public interface IFFmpegRunner
{
    /// <summary>
    /// Probes the total media duration of an input file using <c>ffprobe</c>.
    /// </summary>
    /// <param name="inputFilePath">Path to the input media file.</param>
    /// <param name="cancellationToken">Token to cancel the probe process.</param>
    /// <returns>The detected duration, or <see langword="null"/> if unavailable.</returns>
    Task<TimeSpan?> ProbeDurationAsync(string inputFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an FFmpeg conversion job asynchronously with real-time progress reporting and cancellation cleanup.
    /// </summary>
    /// <param name="job">The conversion job definition.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Token to cancel the running conversion.</param>
    /// <returns>A <see cref="ConversionResult"/> describing success, cancellation, or failure.</returns>
    Task<ConversionResult> ConvertAsync(
        ConversionJob job,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs <c>ffmpeg</c> and <c>ffprobe</c> child processes cleanly without shell execution or stream deadlocks.
/// </summary>
public sealed class FFmpegRunner : IFFmpegRunner
{
    private const int MaxStderrCharacters = 64 * 1024;
    private readonly string? _overrideDirectory;

    /// <summary>
    /// Initializes a new instance of <see cref="FFmpegRunner"/> using standard executable discovery.
    /// </summary>
    public FFmpegRunner()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="FFmpegRunner"/> with an optional directory override for FFmpeg binaries.
    /// </summary>
    /// <param name="overrideDirectory">Optional directory checked before standard FFmpeg discovery locations.</param>
    public FFmpegRunner(string? overrideDirectory)
    {
        _overrideDirectory = overrideDirectory;
    }

    /// <inheritdoc />
    public async Task<TimeSpan?> ProbeDurationAsync(string inputFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        cancellationToken.ThrowIfCancellationRequested();

        string? ffprobePath = FFmpegLocator.LocateFFprobe(_overrideDirectory);
        if (ffprobePath is null || !File.Exists(inputFilePath))
        {
            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("format=duration");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        startInfo.ArgumentList.Add(inputFilePath);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return null;
        }

        try
        {
            process.StandardInput.Close();
        }
        catch (Exception)
        {
            // Ignore closed stdin handle errors.
        }

        try
        {
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string stdout = await stdoutTask.ConfigureAwait(false);
            await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return null;
            }

            string trimmed = stdout.Trim();
            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) &&
                double.IsFinite(seconds) &&
                seconds > 0 &&
                seconds < TimeSpan.MaxValue.TotalSeconds)
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            await WaitForExitSafelyAsync(process).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ConversionResult> ConvertAsync(
        ConversionJob job,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var stopwatch = Stopwatch.StartNew();

        if (cancellationToken.IsCancellationRequested)
        {
            return CreateCancelledResult(job.OutputFilePath, stopwatch.Elapsed);
        }

        string? ffmpegPath = FFmpegLocator.LocateFFmpeg(_overrideDirectory);
        if (ffmpegPath is null)
        {
            return new ConversionResult(
                Success: false,
                IsCancelled: false,
                OutputFilePath: job.OutputFilePath,
                OutputFileSizeBytes: 0,
                Elapsed: stopwatch.Elapsed,
                ErrorMessage: "FFmpeg executable (ffmpeg.exe) could not be located.");
        }

        IReadOnlyList<string> arguments;
        try
        {
            arguments = FFmpegArgumentBuilder.Build(job);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            return new ConversionResult(
                Success: false,
                IsCancelled: false,
                OutputFilePath: job.OutputFilePath,
                OutputFileSizeBytes: 0,
                Elapsed: stopwatch.Elapsed,
                ErrorMessage: ex.Message);
        }

        TimeSpan? probedDuration = null;
        if (job.SourceCategory != FormatCategory.Image)
        {
            try
            {
                probedDuration = await ProbeDurationAsync(job.InputFilePath, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return CreateCancelledResult(job.OutputFilePath, stopwatch.Elapsed);
            }
            catch (Exception)
            {
                // Non-fatal: FFmpegProgressParser will still detect Duration from FFmpeg stderr if present.
                probedDuration = null;
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CreateCancelledResult(job.OutputFilePath, stopwatch.Elapsed);
        }

        try
        {
            string? outputDirectory = Path.GetDirectoryName(job.OutputFilePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }
        catch (Exception ex)
        {
            return new ConversionResult(
                Success: false,
                IsCancelled: false,
                OutputFilePath: job.OutputFilePath,
                OutputFileSizeBytes: 0,
                Elapsed: stopwatch.Elapsed,
                ErrorMessage: $"Could not create output directory: {ex.Message}");
        }

        progress?.Report(new ConversionProgress(
            Percentage: 0,
            ProcessedTime: TimeSpan.Zero,
            TotalDuration: probedDuration,
            EstimatedRemaining: null,
            SpeedText: null,
            IsIndeterminate: probedDuration is null));

        var parser = new FFmpegProgressParser(probedDuration);
        var stderrBuffer = new StringBuilder();

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        for (int i = 0; i < arguments.Count; i++)
        {
            startInfo.ArgumentList.Add(arguments[i]);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return new ConversionResult(
                    Success: false,
                    IsCancelled: false,
                    OutputFilePath: job.OutputFilePath,
                    OutputFileSizeBytes: 0,
                    Elapsed: stopwatch.Elapsed,
                    ErrorMessage: "Failed to start FFmpeg process.");
            }
        }
        catch (Exception ex)
        {
            return new ConversionResult(
                Success: false,
                IsCancelled: false,
                OutputFilePath: job.OutputFilePath,
                OutputFileSizeBytes: 0,
                Elapsed: stopwatch.Elapsed,
                ErrorMessage: $"Failed to start FFmpeg process: {ex.Message}");
        }

        try
        {
            process.StandardInput.Close();
        }
        catch (Exception)
        {
            // Ignore closed stdin handle errors.
        }

        Task stdoutTask = ReadStdoutProgressAsync(process.StandardOutput, parser, progress, stopwatch);
        Task stderrTask = ReadStderrAsync(process.StandardError, parser, stderrBuffer, progress, stopwatch);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            await WaitForExitSafelyAsync(process).ConfigureAwait(false);
            await DrainStreamTasksSafelyAsync(stdoutTask, stderrTask).ConfigureAwait(false);
            TryDeletePartialOutput(job.InputFilePath, job.OutputFilePath);

            return CreateCancelledResult(job.OutputFilePath, stopwatch.Elapsed);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            TryDeletePartialOutput(job.InputFilePath, job.OutputFilePath);
            return CreateCancelledResult(job.OutputFilePath, stopwatch.Elapsed);
        }

        if (process.ExitCode != 0)
        {
            TryDeletePartialOutput(job.InputFilePath, job.OutputFilePath);
            string errorMessage = FFmpegProgressParser.ExtractErrorMessage(stderrBuffer.ToString());
            return new ConversionResult(
                Success: false,
                IsCancelled: false,
                OutputFilePath: job.OutputFilePath,
                OutputFileSizeBytes: 0,
                Elapsed: stopwatch.Elapsed,
                ErrorMessage: errorMessage);
        }

        var outputInfo = new FileInfo(job.OutputFilePath);
        if (!outputInfo.Exists)
        {
            return new ConversionResult(
                Success: false,
                IsCancelled: false,
                OutputFilePath: job.OutputFilePath,
                OutputFileSizeBytes: 0,
                Elapsed: stopwatch.Elapsed,
                ErrorMessage: "FFmpeg completed without producing the expected output file.");
        }

        TimeSpan? finalDuration = parser.TotalDuration ?? probedDuration;
        progress?.Report(new ConversionProgress(
            Percentage: 100.0,
            ProcessedTime: finalDuration ?? TimeSpan.Zero,
            TotalDuration: finalDuration,
            EstimatedRemaining: TimeSpan.Zero,
            SpeedText: null,
            IsIndeterminate: false));

        return new ConversionResult(
            Success: true,
            IsCancelled: false,
            OutputFilePath: job.OutputFilePath,
            OutputFileSizeBytes: outputInfo.Length,
            Elapsed: stopwatch.Elapsed,
            ErrorMessage: null);
    }

    private static async Task ReadStdoutProgressAsync(
        StreamReader reader,
        FFmpegProgressParser parser,
        IProgress<ConversionProgress>? progress,
        Stopwatch stopwatch)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                ConversionProgress? update = parser.ParseProgressLine(line, stopwatch.Elapsed);
                if (update is not null)
                {
                    progress?.Report(update);
                }
            }
        }
        catch (IOException)
        {
            // Stream closed on process termination.
        }
        catch (ObjectDisposedException)
        {
            // Stream disposed on process termination.
        }
    }

    private static async Task ReadStderrAsync(
        StreamReader reader,
        FFmpegProgressParser parser,
        StringBuilder stderrBuffer,
        IProgress<ConversionProgress>? progress,
        Stopwatch stopwatch)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                AppendBounded(stderrBuffer, line);

                ConversionProgress? update = parser.ParseStderrLine(line, stopwatch.Elapsed);
                if (update is not null)
                {
                    progress?.Report(update);
                }
            }
        }
        catch (IOException)
        {
            // Stream closed on process termination.
        }
        catch (ObjectDisposedException)
        {
            // Stream disposed on process termination.
        }
    }

    private static void AppendBounded(StringBuilder buffer, string line)
    {
        if (buffer.Length > MaxStderrCharacters)
        {
            int removeCount = buffer.Length - (MaxStderrCharacters / 2);
            buffer.Remove(0, removeCount);
        }

        buffer.AppendLine(line);
    }

    private static ConversionResult CreateCancelledResult(string outputFilePath, TimeSpan elapsed) =>
        new(
            Success: false,
            IsCancelled: true,
            OutputFilePath: outputFilePath,
            OutputFileSizeBytes: 0,
            Elapsed: elapsed,
            ErrorMessage: "Conversion was cancelled.");

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Process may have exited concurrently.
        }
    }

    private static async Task WaitForExitSafelyAsync(Process process)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort wait so file handles are released before cleanup.
        }
    }

    private static async Task DrainStreamTasksSafelyAsync(Task stdoutTask, Task stderrTask)
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort stream drain.
        }
    }

    private static void TryDeletePartialOutput(string inputFilePath, string outputFilePath)
    {
        try
        {
            string fullInput = Path.GetFullPath(inputFilePath);
            string fullOutput = Path.GetFullPath(outputFilePath);
            if (string.Equals(fullInput, fullOutput, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (File.Exists(fullOutput))
            {
                File.Delete(fullOutput);
            }
        }
        catch (Exception)
        {
            // Best-effort cleanup of partial output file.
        }
    }
}
