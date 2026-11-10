namespace TunnelTimer.Core.Tests;

public sealed class CountdownFormatterTests
{
    [Theory]
    [InlineData(600, "10:00")]
    [InlineData(599.4, "10:00")]
    [InlineData(599, "09:59")]
    [InlineData(61, "01:01")]
    [InlineData(1, "00:01")]
    [InlineData(0.2, "00:01")]
    [InlineData(0, "00:00")]
    [InlineData(-0.5, "00:00")]
    [InlineData(-1, "+00:01")]
    [InlineData(-83, "+01:23")]
    [InlineData(-83.9, "+01:23")]
    [InlineData(-4503, "+75:03")]
    public void Formats_seconds_until_start(double secondsUntilStart, string expected)
    {
        var text = CountdownFormatter.Format(TimeSpan.FromSeconds(secondsUntilStart));

        Assert.Equal(expected, text);
    }
}
