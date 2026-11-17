namespace ConvertNow.Core.Tests.Conversion;

public sealed class ConversionModelsTests
{
    [Theory]
    [InlineData(null, "--")]
    [InlineData(-5.0, "--")]
    [InlineData(0.0, "0s")]
    [InlineData(0.4, "< 1s")]
    [InlineData(34.0, "34s")]
    [InlineData(154.0, "2m 34s")]
    [InlineData(120.0, "2m 0s")]
    [InlineData(3665.0, "1h 1m 5s")]
    [InlineData(90061.0, "25h 1m 1s")]
    public void FormatEta_FormatsAllRangesPredictably(double? seconds, string expected)
    {
        TimeSpan? eta = seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value) : null;
        Assert.Equal(expected, ConversionProgress.FormatEta(eta));
    }
}
