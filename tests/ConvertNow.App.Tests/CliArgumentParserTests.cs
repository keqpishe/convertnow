using ConvertNow.App.Services;

namespace ConvertNow.App.Tests;

public class CliArgumentParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(new object[] { new string[0] })]
    [InlineData(new object[] { new[] { "", "   " } })]
    public void Parse_NullOrEmptyArgs_ReturnsNull(string[]? args)
    {
        string? result = CliArgumentParser.Parse(args);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_FileFlagWithSeparateArgument_ReturnsPath()
    {
        string? result = CliArgumentParser.Parse(["--file", @"C:\Videos\clip.mp4"]);

        Assert.Equal(@"C:\Videos\clip.mp4", result);
    }

    [Fact]
    public void Parse_FileFlagIsCaseInsensitive_ReturnsPath()
    {
        string? result = CliArgumentParser.Parse(["--FILE", @"C:\Videos\clip.mp4"]);

        Assert.Equal(@"C:\Videos\clip.mp4", result);
    }

    [Fact]
    public void Parse_FileFlagWithEqualsSyntax_ReturnsPath()
    {
        string? result = CliArgumentParser.Parse([@"--file=C:\Videos\clip.mp4"]);

        Assert.Equal(@"C:\Videos\clip.mp4", result);
    }

    [Fact]
    public void Parse_FileFlagWithQuotedPath_TrimsQuotes()
    {
        string? result = CliArgumentParser.Parse(["--file=\"C:\\My Videos\\clip 01.mp4\""]);

        Assert.Equal(@"C:\My Videos\clip 01.mp4", result);
    }

    [Theory]
    [InlineData("--file")]
    [InlineData("--file", "   ")]
    [InlineData("--file=")]
    [InlineData("--file=\"\"")]
    public void Parse_FileFlagMissingOrEmptyValue_ReturnsNull(params string[] args)
    {
        string? result = CliArgumentParser.Parse(args);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_PositionalPath_ReturnsPath()
    {
        string? result = CliArgumentParser.Parse([@"C:\Audio\song.flac"]);

        Assert.Equal(@"C:\Audio\song.flac", result);
    }

    [Fact]
    public void Parse_PositionalAfterUnknownFlag_SkipsFlagAndReturnsPath()
    {
        string? result = CliArgumentParser.Parse(["--silent", @"C:\Images\photo.png"]);

        Assert.Equal(@"C:\Images\photo.png", result);
    }

    [Fact]
    public void Parse_FileFlagTakesPrecedenceOverPositionalArgument()
    {
        string? result = CliArgumentParser.Parse([@"C:\first.mp4", "--file", @"C:\second.mp4"]);

        Assert.Equal(@"C:\second.mp4", result);
    }

    [Fact]
    public void ParseProcessArgs_SkipsExecutablePathAtIndexZero()
    {
        Assert.Null(CliArgumentParser.ParseProcessArgs([@"C:\Program Files\ConvertNow\ConvertNow.exe"]));
        Assert.Equal(
            @"C:\Videos\movie.mkv",
            CliArgumentParser.ParseProcessArgs([@"C:\App\ConvertNow.exe", "--file", @"C:\Videos\movie.mkv"]));
        Assert.Equal(
            @"C:\Videos\movie.mkv",
            CliArgumentParser.ParseProcessArgs([@"C:\App\ConvertNow.exe", @"C:\Videos\movie.mkv"]));
    }
}
