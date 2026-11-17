namespace ConvertNow.Core;

/// <summary>
/// Represents a single media conversion request.
/// </summary>
/// <param name="InputFilePath">Full path to the source media file.</param>
/// <param name="OutputFilePath">Full path where the converted output file should be written.</param>
/// <param name="TargetFormat">Desired output format.</param>
/// <param name="SourceCategory">Category of the input media file.</param>
public sealed record ConversionJob(
    string InputFilePath,
    string OutputFilePath,
    MediaFormat TargetFormat,
    FormatCategory SourceCategory);
