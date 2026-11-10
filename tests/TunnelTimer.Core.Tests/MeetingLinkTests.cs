namespace TunnelTimer.Core.Tests;

public sealed class MeetingLinkTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_is_valid_and_has_no_link(string? text)
    {
        var isValid = MeetingLink.TryParse(text, out var link);

        Assert.True(isValid);
        Assert.Null(link);
    }

    [Theory]
    [InlineData("https://us02web.zoom.us/j/1234567890?pwd=abc")]
    [InlineData("  https://meet.example.com/room  ")]
    [InlineData("http://intranet.example.com/call")]
    public void Web_addresses_are_accepted(string text)
    {
        var isValid = MeetingLink.TryParse(text, out var link);

        Assert.True(isValid);
        Assert.Equal(new Uri(text.Trim()), link);
    }

    [Theory]
    [InlineData("zoom.us/j/1234567890")]
    [InlineData("not a link")]
    [InlineData("zoommtg://zoom.us/join?confno=1234567890")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    public void Anything_else_is_rejected(string text)
    {
        var isValid = MeetingLink.TryParse(text, out var link);

        Assert.False(isValid);
        Assert.Null(link);
    }
}
