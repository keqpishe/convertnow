namespace ConvertNow.App.ViewModels;

/// <summary>
/// Represents the active visual state of the Convert.Now window.
/// </summary>
public enum ConversionViewState
{
    /// <summary>Launched without an input file path; offers a file browse prompt.</summary>
    NoFile,

    /// <summary>Input file is valid and supported; allows selecting target format and save path.</summary>
    FormatSelection,

    /// <summary>FFmpeg conversion is running with progress and ETA updates.</summary>
    Converting,

    /// <summary>Conversion succeeded; displays output file details and folder/close actions.</summary>
    Completed,

    /// <summary>Conversion or file loading failed; displays error details and Retry/Close actions.</summary>
    Error,

    /// <summary>Selected input file format is unsupported.</summary>
    Unsupported,
}
