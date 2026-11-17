namespace ConvertNow.Core;

/// <summary>
/// Represents the final outcome of a conversion job.
/// </summary>
/// <param name="Success"><see langword="true"/> if FFmpeg completed cleanly and produced a valid output file.</param>
/// <param name="IsCancelled"><see langword="true"/> if the conversion was cancelled by the caller.</param>
/// <param name="OutputFilePath">Target output file path for the job.</param>
/// <param name="OutputFileSizeBytes">Size of the resulting output file in bytes, or <c>0</c> if failed or cancelled.</param>
/// <param name="Elapsed">Total elapsed wall-clock time for the operation.</param>
/// <param name="ErrorMessage">Human-readable error or cancellation message, or <see langword="null"/> on success.</param>
public sealed record ConversionResult(
    bool Success,
    bool IsCancelled,
    string OutputFilePath,
    long OutputFileSizeBytes,
    TimeSpan Elapsed,
    string? ErrorMessage);
