namespace TunnelTimer.Core.Tests;

public sealed class TimeOfDayInputTests
{
    [Theory]
    [InlineData("1656", 16, 56)]
    [InlineData("0905", 9, 5)]
    [InlineData("420", 4, 20)]
    [InlineData("905", 9, 5)]
    [InlineData("0000", 0, 0)]
    [InlineData("2359", 23, 59)]
    [InlineData("  1656  ", 16, 56)]
    public void Three_or_four_digits_are_hours_then_two_minute_digits(string text, int hour, int minute)
    {
        var isValid = TimeOfDayInput.TryParse(text, out var timeOfDay);

        Assert.True(isValid);
        Assert.Equal(new TimeSpan(hour, minute, 0), timeOfDay);
    }

    [Theory]
    [InlineData("9", 9)]
    [InlineData("16", 16)]
    [InlineData("0", 0)]
    public void One_or_two_digits_are_a_full_hour(string text, int hour)
    {
        var isValid = TimeOfDayInput.TryParse(text, out var timeOfDay);

        Assert.True(isValid);
        Assert.Equal(new TimeSpan(hour, 0, 0), timeOfDay);
    }

    [Theory]
    [InlineData("16:56", 16, 56)]
    [InlineData("4:20", 4, 20)]
    [InlineData("16.56", 16, 56)]
    public void A_colon_or_dot_between_hours_and_minutes_is_accepted(string text, int hour, int minute)
    {
        var isValid = TimeOfDayInput.TryParse(text, out var timeOfDay);

        Assert.True(isValid);
        Assert.Equal(new TimeSpan(hour, minute, 0), timeOfDay);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2400")]
    [InlineData("24")]
    [InlineData("1660")]
    [InlineData("12345")]
    [InlineData("16:5")]
    [InlineData("16:561")]
    [InlineData(":56")]
    [InlineData("1:2:3")]
    [InlineData("16 56")]
    [InlineData("-420")]
    [InlineData("4pm")]
    [InlineData("١٦٥٦")]
    public void Anything_else_is_rejected(string? text)
    {
        var isValid = TimeOfDayInput.TryParse(text, out var timeOfDay);

        Assert.False(isValid);
        Assert.Equal(TimeSpan.Zero, timeOfDay);
    }
}
