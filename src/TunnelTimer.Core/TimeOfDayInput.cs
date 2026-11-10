using System.Globalization;

namespace TunnelTimer.Core;

/// <summary>
/// Reads a time of day typed as plain digits, so a start time off the five minute grid takes
/// four key presses: "1656" is 16:56, "420" is 4:20, "9" is 9:00. A colon or a dot between
/// hours and minutes is accepted as well. Hours are always read on the 24 hour clock.
/// </summary>
public static class TimeOfDayInput
{
    private const int MinuteDigits = 2;
    private const int MaxHourDigits = 2;
    private const int MaxHour = 23;
    private const int MaxMinute = 59;

    private static readonly char[] Separators = [':', '.'];

    public static bool TryParse(string? text, out TimeSpan timeOfDay)
    {
        timeOfDay = default;
        var input = text?.Trim() ?? string.Empty;

        string hourPart;
        string minutePart;
        var separatorIndex = input.IndexOfAny(Separators);
        if (separatorIndex >= 0)
        {
            hourPart = input[..separatorIndex];
            minutePart = input[(separatorIndex + 1)..];
            if (minutePart.Length != MinuteDigits)
            {
                return false;
            }
        }
        else if (input.Length <= MaxHourDigits)
        {
            hourPart = input;
            minutePart = "00";
        }
        else
        {
            hourPart = input[..^MinuteDigits];
            minutePart = input[^MinuteDigits..];
        }

        if (hourPart.Length is 0 or > MaxHourDigits || !IsDigits(hourPart) || !IsDigits(minutePart))
        {
            return false;
        }

        var hour = int.Parse(hourPart, CultureInfo.InvariantCulture);
        var minute = int.Parse(minutePart, CultureInfo.InvariantCulture);
        if (hour > MaxHour || minute > MaxMinute)
        {
            return false;
        }

        timeOfDay = new TimeSpan(hour, minute, 0);
        return true;
    }

    private static bool IsDigits(string text)
    {
        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
