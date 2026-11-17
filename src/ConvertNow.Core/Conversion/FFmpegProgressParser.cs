using System.Globalization;
using System.Text.RegularExpressions;

namespace ConvertNow.Core;

/// <summary>
/// Parses FFmpeg <c>-progress pipe:1</c> stdout lines and stderr metadata/error output into <see cref="ConversionProgress"/> updates.
/// </summary>
public sealed partial class FFmpegProgressParser
{
    private readonly Lock _gate = new();

    private TimeSpan? _totalDuration;
    private TimeSpan _lastProcessedTime = TimeSpan.Zero;
    private double? _lastSpeedValue;
    private string? _lastSpeedText;
    private bool _sawPipeProgress;

    private long? _blockOutTimeUs;
    private long? _blockOutTimeMs;
    private TimeSpan? _blockOutTime;
    private double? _blockSpeedValue;
    private string? _blockSpeedText;

    /// <summary>
    /// Initializes a new instance of <see cref="FFmpegProgressParser"/>.
    /// </summary>
    /// <param name="totalDuration">Optional pre-probed total media duration.</param>
    public FFmpegProgressParser(TimeSpan? totalDuration = null)
    {
        if (totalDuration is { } duration && duration > TimeSpan.Zero)
        {
            _totalDuration = duration;
        }
    }

    /// <summary>
    /// Gets the currently known total media duration, if available.
    /// </summary>
    public TimeSpan? TotalDuration
    {
        get
        {
            lock (_gate)
            {
                return _totalDuration;
            }
        }
    }

    /// <summary>
    /// Parses a single key-value line emitted by FFmpeg <c>-progress pipe:1</c>.
    /// </summary>
    /// <param name="line">A stdout line from FFmpeg.</param>
    /// <param name="elapsed">Optional elapsed wall-clock time since conversion started, used for fallback ETA calculation.</param>
    /// <returns>A <see cref="ConversionProgress"/> snapshot when a progress block completes, otherwise <see langword="null"/>.</returns>
    public ConversionProgress? ParseProgressLine(string? line, TimeSpan? elapsed = null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        int equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return null;
        }

        string key = line[..equalsIndex].Trim();
        string value = line[(equalsIndex + 1)..].Trim();

        lock (_gate)
        {
            switch (key)
            {
                case "out_time_us":
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long us) && us >= 0)
                    {
                        _blockOutTimeUs = us;
                    }
                    return null;

                case "out_time_ms":
                    // FFmpeg historically writes microseconds (not milliseconds) to out_time_ms in -progress pipe:1 output.
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long msAsUs) && msAsUs >= 0)
                    {
                        _blockOutTimeMs = msAsUs;
                    }
                    return null;

                case "out_time":
                    if (TryParseFFmpegTime(value, out TimeSpan outTime))
                    {
                        _blockOutTime = outTime;
                    }
                    return null;

                case "speed":
                    if (TryParseSpeed(value, out double parsedSpeed, out string? cleanSpeed))
                    {
                        _blockSpeedValue = parsedSpeed;
                        _blockSpeedText = cleanSpeed;
                    }
                    return null;

                case "progress":
                    _sawPipeProgress = true;
                    bool isEnd = string.Equals(value, "end", StringComparison.OrdinalIgnoreCase);

                    TimeSpan processedTime = _blockOutTimeUs.HasValue
                        ? TimeSpan.FromTicks(_blockOutTimeUs.Value * 10L)
                        : _blockOutTimeMs.HasValue
                            ? TimeSpan.FromTicks(_blockOutTimeMs.Value * 10L)
                            : _blockOutTime ?? _lastProcessedTime;

                    if (processedTime < TimeSpan.Zero)
                    {
                        processedTime = TimeSpan.Zero;
                    }

                    _lastProcessedTime = processedTime;

                    if (_blockSpeedValue.HasValue)
                    {
                        _lastSpeedValue = _blockSpeedValue;
                    }

                    if (_blockSpeedText is not null)
                    {
                        _lastSpeedText = _blockSpeedText;
                    }

                    ConversionProgress snapshot = CreateProgressSnapshot(
                        processedTime,
                        _blockSpeedValue ?? _lastSpeedValue,
                        _blockSpeedText ?? _lastSpeedText,
                        elapsed,
                        isEnd);

                    ResetBlockState();
                    return snapshot;

                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Parses a single line from FFmpeg stderr for duration metadata and fallback progress updates.
    /// </summary>
    /// <param name="line">A stderr line from FFmpeg.</param>
    /// <param name="elapsed">Optional elapsed wall-clock time since conversion started.</param>
    /// <returns>A <see cref="ConversionProgress"/> snapshot if stderr progress was parsed and pipe progress is not active; otherwise <see langword="null"/>.</returns>
    public ConversionProgress? ParseStderrLine(string? line, TimeSpan? elapsed = null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        TimeSpan? duration = ParseDurationFromStderrLine(line);

        lock (_gate)
        {
            if (duration is { } validDuration && validDuration > TimeSpan.Zero && _totalDuration is null)
            {
                _totalDuration = validDuration;
            }

            if (_sawPipeProgress)
            {
                return null;
            }

            Match timeMatch = StderrTimeRegex().Match(line);
            if (!timeMatch.Success || !TryParseFFmpegTime(timeMatch.Groups[1].Value, out TimeSpan processedTime))
            {
                return null;
            }

            _lastProcessedTime = processedTime;

            double? speedValue = null;
            string? speedText = null;
            Match speedMatch = StderrSpeedRegex().Match(line);
            if (speedMatch.Success && TryParseSpeed(speedMatch.Groups[1].Value, out double parsedSpeed, out string? cleanSpeed))
            {
                speedValue = parsedSpeed;
                speedText = cleanSpeed;
                _lastSpeedValue = parsedSpeed;
                _lastSpeedText = cleanSpeed;
            }

            return CreateProgressSnapshot(
                processedTime,
                speedValue ?? _lastSpeedValue,
                speedText ?? _lastSpeedText,
                elapsed,
                isEnd: false);
        }
    }

    /// <summary>
    /// Extracts the duration from an FFmpeg stderr <c>Duration: HH:MM:SS.xx</c> line.
    /// </summary>
    /// <param name="line">A stderr line from FFmpeg.</param>
    /// <returns>The parsed duration, or <see langword="null"/> if not present or <c>N/A</c>.</returns>
    public static TimeSpan? ParseDurationFromStderrLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        Match match = StderrDurationRegex().Match(line);
        if (!match.Success)
        {
            return null;
        }

        return TryParseFFmpegTime(match.Groups[1].Value, out TimeSpan duration) && duration > TimeSpan.Zero
            ? duration
            : null;
    }

    /// <summary>
    /// Parses an FFmpeg timestamp formatted as <c>HH:MM:SS(.fff...)</c>, including hour counts >= 24.
    /// </summary>
    /// <param name="value">The raw timestamp text.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the parsed <see cref="TimeSpan"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParseFFmpegTime(string? value, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.StartsWith('-') || string.Equals(trimmed, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] parts = trimmed.Split(':');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long hours) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) ||
            !double.TryParse(parts[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds))
        {
            return false;
        }

        if (minutes is < 0 or >= 60 || seconds < 0 || seconds >= 60 || !double.IsFinite(seconds))
        {
            return false;
        }

        double totalSeconds = (hours * 3600.0) + (minutes * 60.0) + seconds;
        if (!double.IsFinite(totalSeconds) || totalSeconds >= TimeSpan.MaxValue.TotalSeconds)
        {
            return false;
        }

        result = TimeSpan.FromSeconds(totalSeconds);
        return true;
    }

    /// <summary>
    /// Extracts a concise, user-friendly error message from captured FFmpeg stderr output.
    /// </summary>
    /// <param name="stderr">Full or partial FFmpeg stderr text.</param>
    /// <returns>A readable error description.</returns>
    public static string ExtractErrorMessage(string? stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return "FFmpeg exited with an unknown error.";
        }

        string[] rawLines = stderr.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var meaningfulLines = new List<string>(rawLines.Length);

        foreach (string rawLine in rawLines)
        {
            string cleaned = ComponentPrefixRegex().Replace(rawLine, string.Empty).Trim();
            if (cleaned.Length == 0 || IsNoiseLine(cleaned))
            {
                continue;
            }

            meaningfulLines.Add(cleaned);
        }

        for (int i = meaningfulLines.Count - 1; i >= 0; i--)
        {
            string line = meaningfulLines[i];
            if (ContainsErrorKeyword(line))
            {
                return SimplifyErrorLine(line);
            }
        }

        if (meaningfulLines.Count > 0)
        {
            return SimplifyErrorLine(meaningfulLines[^1]);
        }

        return "FFmpeg exited with an unknown error.";
    }

    private ConversionProgress CreateProgressSnapshot(
        TimeSpan processedTime,
        double? speedValue,
        string? speedText,
        TimeSpan? elapsed,
        bool isEnd)
    {
        if (_totalDuration is { } total && total > TimeSpan.Zero)
        {
            if (isEnd)
            {
                TimeSpan finalProcessed = processedTime > total ? processedTime : total;
                return new ConversionProgress(
                    Percentage: 100.0,
                    ProcessedTime: finalProcessed,
                    TotalDuration: total,
                    EstimatedRemaining: TimeSpan.Zero,
                    SpeedText: speedText,
                    IsIndeterminate: false);
            }

            double percentage = Math.Clamp((processedTime.TotalSeconds / total.TotalSeconds) * 100.0, 0.0, 100.0);
            TimeSpan remainingMedia = total - processedTime;

            TimeSpan? eta = null;
            if (remainingMedia <= TimeSpan.Zero)
            {
                eta = TimeSpan.Zero;
            }
            else if (speedValue is { } speed && speed > 0.001)
            {
                double etaSeconds = remainingMedia.TotalSeconds / speed;
                if (double.IsFinite(etaSeconds) && etaSeconds >= 0 && etaSeconds < TimeSpan.MaxValue.TotalSeconds)
                {
                    eta = TimeSpan.FromSeconds(etaSeconds);
                }
            }
            else if (elapsed is { } wallElapsed && wallElapsed > TimeSpan.FromMilliseconds(200) && processedTime > TimeSpan.Zero)
            {
                double effectiveRate = processedTime.TotalSeconds / wallElapsed.TotalSeconds;
                if (effectiveRate > 0.001)
                {
                    double etaSeconds = remainingMedia.TotalSeconds / effectiveRate;
                    if (double.IsFinite(etaSeconds) && etaSeconds >= 0 && etaSeconds < TimeSpan.MaxValue.TotalSeconds)
                    {
                        eta = TimeSpan.FromSeconds(etaSeconds);
                    }
                }
            }

            return new ConversionProgress(
                Percentage: percentage,
                ProcessedTime: processedTime,
                TotalDuration: total,
                EstimatedRemaining: eta,
                SpeedText: speedText,
                IsIndeterminate: false);
        }

        if (isEnd)
        {
            return new ConversionProgress(
                Percentage: 100.0,
                ProcessedTime: processedTime,
                TotalDuration: null,
                EstimatedRemaining: TimeSpan.Zero,
                SpeedText: speedText,
                IsIndeterminate: false);
        }

        return new ConversionProgress(
            Percentage: 0.0,
            ProcessedTime: processedTime,
            TotalDuration: null,
            EstimatedRemaining: null,
            SpeedText: speedText,
            IsIndeterminate: true);
    }

    private void ResetBlockState()
    {
        _blockOutTimeUs = null;
        _blockOutTimeMs = null;
        _blockOutTime = null;
        _blockSpeedValue = null;
        _blockSpeedText = null;
    }

    private static bool TryParseSpeed(string value, out double speedValue, out string? cleanSpeedText)
    {
        speedValue = 0;
        cleanSpeedText = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (string.Equals(trimmed, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string numericPart = trimmed.EndsWith('x') || trimmed.EndsWith('X')
            ? trimmed[..^1].Trim()
            : trimmed;

        if (!double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ||
            !double.IsFinite(parsed) ||
            parsed < 0)
        {
            return false;
        }

        speedValue = parsed;
        cleanSpeedText = $"{numericPart}x";
        return true;
    }

    private static bool IsNoiseLine(string line)
    {
        return line.StartsWith("Input #", StringComparison.Ordinal) ||
               line.StartsWith("Output #", StringComparison.Ordinal) ||
               line.StartsWith("Metadata:", StringComparison.Ordinal) ||
               line.StartsWith("Duration:", StringComparison.Ordinal) ||
               line.StartsWith("Stream #", StringComparison.Ordinal) ||
               line.StartsWith("Stream mapping:", StringComparison.Ordinal) ||
               line.StartsWith("Press [q]", StringComparison.Ordinal) ||
               line.StartsWith("frame=", StringComparison.Ordinal) ||
               line.StartsWith("size=", StringComparison.Ordinal) ||
               line.StartsWith("Conversion failed!", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Error opening input file ", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Side data:", StringComparison.Ordinal) ||
               line.StartsWith("using SAR=", StringComparison.Ordinal) ||
               line.StartsWith("using cpu capabilities:", StringComparison.Ordinal) ||
               line.StartsWith("profile ", StringComparison.Ordinal) ||
               line.StartsWith("264 - core", StringComparison.Ordinal) ||
               line.StartsWith("Svt[info]:", StringComparison.Ordinal) ||
               (line.StartsWith("Format ", StringComparison.Ordinal) && line.Contains("detected only with low score", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsErrorKeyword(string line)
    {
        return line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("no such file", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("permission denied", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("unsupported", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("unknown encoder", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("unknown decoder", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("unable", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("cannot", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("could not", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("no space", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("does not contain", StringComparison.OrdinalIgnoreCase);
    }

    private static string SimplifyErrorLine(string line)
    {
        const string openInputFilesPrefix = "Error opening input files:";
        if (line.StartsWith(openInputFilesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string rest = line[openInputFilesPrefix.Length..].Trim();
            if (rest.Length > 0)
            {
                return rest;
            }
        }

        const string openInputPrefix = "Error opening input:";
        if (line.StartsWith(openInputPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string rest = line[openInputPrefix.Length..].Trim();
            if (rest.Length > 0)
            {
                return rest;
            }
        }

        return line;
    }

    [GeneratedRegex(@"Duration:\s*([^\s,]+)", RegexOptions.CultureInvariant)]
    private static partial Regex StderrDurationRegex();

    [GeneratedRegex(@"\btime=([^\s]+)", RegexOptions.CultureInvariant)]
    private static partial Regex StderrTimeRegex();

    [GeneratedRegex(@"\bspeed=\s*([^\s]+)", RegexOptions.CultureInvariant)]
    private static partial Regex StderrSpeedRegex();

    [GeneratedRegex(@"^\[[^\]]+\]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex ComponentPrefixRegex();
}
