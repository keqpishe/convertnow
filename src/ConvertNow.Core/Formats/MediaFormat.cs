namespace ConvertNow.Core;

/// <summary>
/// Describes a media format supported by Convert.Now.
/// </summary>
/// <param name="Extension">Normalized lowercase file extension without a leading dot (for example, <c>"mp4"</c>).</param>
/// <param name="DisplayName">Uppercase display name suitable for UI presentation (for example, <c>"MP4"</c>).</param>
/// <param name="Category">The media category of the format.</param>
public sealed record MediaFormat(string Extension, string DisplayName, FormatCategory Category)
{
    /// <summary>
    /// Gets the extension prefixed with a leading dot (for example, <c>".mp4"</c>).
    /// </summary>
    public string ExtensionWithDot => $".{Extension}";

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}
