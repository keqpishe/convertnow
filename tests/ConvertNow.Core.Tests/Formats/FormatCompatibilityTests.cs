namespace ConvertNow.Core.Tests.Formats;

public sealed class FormatCompatibilityTests
{
    [Fact]
    public void VideoCategory_ReturnsExact17CompatibleOutputs()
    {
        IReadOnlyList<MediaFormat> outputs = FormatCompatibility.GetCompatibleOutputFormats(FormatCategory.Video);
        string[] expected =
        [
            "mp4", "avi", "mkv", "mov", "webm", "flv", "wmv", "m4v", "3gp", "ts",
            "mp3", "wav", "flac", "aac", "ogg", "m4a", "opus",
        ];

        Assert.Equal(expected, outputs.Select(f => f.Extension));
        Assert.DoesNotContain(outputs, f => f.Extension == "wma");
        Assert.DoesNotContain(outputs, f => f.Category == FormatCategory.Image);
    }

    [Fact]
    public void AudioCategory_ReturnsExact8CompatibleOutputs()
    {
        IReadOnlyList<MediaFormat> outputs = FormatCompatibility.GetCompatibleOutputFormats(FormatCategory.Audio);
        string[] expected = ["mp3", "wav", "flac", "aac", "ogg", "m4a", "opus", "wma"];

        Assert.Equal(expected, outputs.Select(f => f.Extension));
    }

    [Fact]
    public void ImageCategory_ReturnsExact7CompatibleOutputs()
    {
        IReadOnlyList<MediaFormat> outputs = FormatCompatibility.GetCompatibleOutputFormats(FormatCategory.Image);
        string[] expected = ["jpg", "png", "webp", "bmp", "tiff", "ico", "avif"];

        Assert.Equal(expected, outputs.Select(f => f.Extension));
    }

    [Fact]
    public void GetCompatibleOutputFormats_ByPath_HandlesUnsupportedInputs()
    {
        Assert.Empty(FormatCompatibility.GetCompatibleOutputFormats("C:\\app.exe"));
        Assert.Empty(FormatCompatibility.GetCompatibleOutputFormats(string.Empty));
        Assert.Empty(FormatCompatibility.GetCompatibleOutputFormats((string?)null));
        Assert.Equal(17, FormatCompatibility.GetCompatibleOutputFormats("C:\\Видео\\клип.OGV").Count);
    }

    [Theory]
    [InlineData("video.mp4", "mp4", true)]
    [InlineData("video.mp4", ".AVI", true)]
    [InlineData("video.mkv", "mp3", true)]
    [InlineData("video.mp4", "wma", false)]
    [InlineData("video.mp4", "jpg", false)]
    [InlineData("audio.flac", "wma", true)]
    [InlineData("audio.mp3", "mp4", false)]
    [InlineData("audio.mp3", "png", false)]
    [InlineData("image.heic", "avif", true)]
    [InlineData("image.png", "gif", false)]
    [InlineData("image.png", "mp3", false)]
    [InlineData("app.exe", "mp4", false)]
    [InlineData("video.mp4", "", false)]
    public void IsCompatible_MatchesCompatibilityMatrix(string input, string target, bool expected)
    {
        Assert.Equal(expected, FormatCompatibility.IsCompatible(input, target));
    }

    [Theory]
    [InlineData("video.mp4", "avi")]
    [InlineData("video.mkv", "mp4")]
    [InlineData("video.vob", "mp4")]
    [InlineData("audio.mp3", "wav")]
    [InlineData("audio.flac", "mp3")]
    [InlineData("photo.jpg", "png")]
    [InlineData("photo.png", "jpg")]
    [InlineData("photo.heic", "jpg")]
    public void GetDefaultOutputFormat_PrefersDifferentExtension(string input, string expectedDefaultExt)
    {
        MediaFormat? result = FormatCompatibility.GetDefaultOutputFormat(input);
        Assert.NotNull(result);
        Assert.Equal(expectedDefaultExt, result.Extension);
    }

    [Fact]
    public void GetDefaultOutputFormat_UnsupportedInput_ReturnsNull()
    {
        Assert.Null(FormatCompatibility.GetDefaultOutputFormat("app.exe"));
        Assert.Null(FormatCompatibility.GetDefaultOutputFormat(null));
    }
}
