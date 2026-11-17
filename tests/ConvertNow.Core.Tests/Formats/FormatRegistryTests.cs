namespace ConvertNow.Core.Tests.Formats;

public sealed class FormatRegistryTests
{
    [Fact]
    public void GetAllInputFormats_ContainsExact38SupportedInputs()
    {
        IReadOnlyList<MediaFormat> all = FormatRegistry.GetAllInputFormats();
        Assert.Equal(38, all.Count);

        string[] expectedVideo = ["mp4", "avi", "mkv", "mov", "webm", "flv", "wmv", "m4v", "3gp", "ts", "mpg", "mpeg", "vob", "ogv"];
        string[] expectedAudio = ["mp3", "wav", "flac", "aac", "ogg", "wma", "m4a", "opus", "aiff", "ape", "ac3", "dts"];
        string[] expectedImage = ["jpg", "jpeg", "png", "webp", "gif", "bmp", "tiff", "tif", "ico", "avif", "heic", "heif"];

        Assert.Equal(expectedVideo, FormatRegistry.GetInputFormats(FormatCategory.Video).Select(f => f.Extension));
        Assert.Equal(expectedAudio, FormatRegistry.GetInputFormats(FormatCategory.Audio).Select(f => f.Extension));
        Assert.Equal(expectedImage, FormatRegistry.GetInputFormats(FormatCategory.Image).Select(f => f.Extension));
    }

    [Fact]
    public void GetAllTargetFormats_ContainsExact25SupportedTargets()
    {
        IReadOnlyList<MediaFormat> targets = FormatRegistry.GetAllTargetFormats();
        Assert.Equal(25, targets.Count);

        string[] expectedTargets =
        [
            "mp4", "avi", "mkv", "mov", "webm", "flv", "wmv", "m4v", "3gp", "ts",
            "mp3", "wav", "flac", "aac", "ogg", "m4a", "opus", "wma",
            "jpg", "png", "webp", "bmp", "tiff", "ico", "avif",
        ];

        Assert.Equal(expectedTargets, targets.Select(f => f.Extension));
    }

    [Theory]
    [InlineData("mp4", "mp4")]
    [InlineData("MP4", "mp4")]
    [InlineData(".JPG", "jpg")]
    [InlineData("  .3gp  ", "3gp")]
    [InlineData("C:\\Videos\\movie.MKV", "mkv")]
    [InlineData("C:\\Пользователи\\Видео\\фильм 🎬.MOV", "mov")]
    [InlineData("/home/user/audio/track.FlAc", "flac")]
    [InlineData("\\\\fileserver\\media\\archive.tar.webm", "webm")]
    [InlineData("C:\\folder.mp4\\extensionless", "")]
    [InlineData("C:\\folder\\mp4", "")]
    [InlineData("file.", "")]
    [InlineData(".", "")]
    [InlineData("..", "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void NormalizeExtension_HandlesPathsUnicodeAndEdgeCases(string? input, string expected)
    {
        Assert.Equal(expected, FormatRegistry.NormalizeExtension(input));
    }

    [Theory]
    [InlineData("clip.mp4", FormatCategory.Video, "MP4")]
    [InlineData("movie.vob", FormatCategory.Video, "VOB")]
    [InlineData("song.opus", FormatCategory.Audio, "OPUS")]
    [InlineData("track.dts", FormatCategory.Audio, "DTS")]
    [InlineData("photo.jpeg", FormatCategory.Image, "JPEG")]
    [InlineData("photo.heic", FormatCategory.Image, "HEIC")]
    [InlineData("photo.heif", FormatCategory.Image, "HEIF")]
    public void TryGetInputFormat_And_GetCategory_ResolveSupportedInputs(string input, FormatCategory expectedCategory, string expectedDisplay)
    {
        Assert.True(FormatRegistry.IsSupportedInput(input));
        Assert.True(FormatRegistry.TryGetInputFormat(input, out MediaFormat? format));
        Assert.NotNull(format);
        Assert.Equal(expectedCategory, format.Category);
        Assert.Equal(expectedDisplay, format.DisplayName);
        Assert.Equal(expectedDisplay, format.ToString());
        Assert.Equal($".{format.Extension}", format.ExtensionWithDot);
        Assert.Equal(expectedCategory, FormatRegistry.GetCategory(input));
    }

    [Theory]
    [InlineData("app.exe")]
    [InlineData("document.pdf")]
    [InlineData("C:\\dir\\mp4")]
    [InlineData("")]
    [InlineData(null)]
    public void UnsupportedInputs_ReturnFalseAndNullCategory(string? input)
    {
        Assert.False(FormatRegistry.IsSupportedInput(input));
        Assert.False(FormatRegistry.TryGetInputFormat(input, out MediaFormat? format));
        Assert.Null(format);
        Assert.Null(FormatRegistry.GetCategory(input));
    }

    [Theory]
    [InlineData("mpg")]
    [InlineData("mpeg")]
    [InlineData("vob")]
    [InlineData("ogv")]
    [InlineData("aiff")]
    [InlineData("ape")]
    [InlineData("ac3")]
    [InlineData("dts")]
    [InlineData("jpeg")]
    [InlineData("gif")]
    [InlineData("tif")]
    [InlineData("heic")]
    [InlineData("heif")]
    [InlineData("exe")]
    public void TryGetTargetFormat_ReturnsFalseForInputOnlyAndUnknownFormats(string extension)
    {
        Assert.False(FormatRegistry.TryGetTargetFormat(extension, out MediaFormat? format));
        Assert.Null(format);
    }

    [Theory]
    [InlineData(".MP4", "mp4")]
    [InlineData("avif", "avif")]
    [InlineData("C:\\out\\icon.ICO", "ico")]
    public void TryGetTargetFormat_ResolvesValidTargets(string input, string expectedExt)
    {
        Assert.True(FormatRegistry.TryGetTargetFormat(input, out MediaFormat? format));
        Assert.NotNull(format);
        Assert.Equal(expectedExt, format.Extension);
    }
}
