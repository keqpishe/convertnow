using System.Globalization;

namespace ConvertNow.Core.Tests.FileUtils;

public sealed class FileSizeFormatterTests
{
    [Theory]
    [InlineData(-500, "0 B")]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1_572_864, "1.5 MB")]
    [InlineData(4_509_715_661, "4.2 GB")]
    [InlineData(1_099_511_627_776, "1 TB")]
    public void Format_FormatsExpectedHumanReadableStrings(long bytes, string expected)
    {
        Assert.Equal(expected, FileSizeFormatter.Format(bytes));
    }

    [Fact]
    public void Format_AlwaysUsesInvariantCultureDecimalSeparator()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            Assert.Equal("1.5 MB", FileSizeFormatter.Format(1_572_864));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
