using System.Globalization;

namespace SchichtWerk.Web.Formatting;

/// <summary>
/// German number formatting that does not depend on the browser's locale.
/// The app runs with invariant globalization, so the separators are set explicitly.
/// Values are rounded here, for display only.
/// </summary>
public static class GermanFormat
{
    /// <summary>Shown when a ratio is undefined (division by zero).</summary>
    public const string Undefined = "–";

    private const string Minus = "−";

    private static readonly NumberFormatInfo Numbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NegativeSign = Minus,
    };

    public static string Number(double value, int decimals = 0) =>
        value.ToString("N" + decimals, Numbers);

    /// <summary>Formats a ratio (0.7778) as a percentage ("77,8 %").</summary>
    public static string Percent(double? ratio, int decimals = 1) =>
        ratio is { } r ? Number(r * 100, decimals) + " %" : Undefined;

    public static string Minutes(TimeSpan duration) =>
        Number(duration.TotalMinutes) + " Min.";

    public static string Pieces(int count) => Number(count) + " Stück";

    /// <summary>"+10", "−18" or "±0".</summary>
    public static string Signed(double value, int decimals = 0)
    {
        var rounded = Math.Round(value, decimals);
        if (rounded == 0)
            return "±" + Number(0, decimals);
        return (rounded > 0 ? "+" : "") + Number(rounded, decimals);
    }

    /// <summary>A ratio change as percentage points, e.g. "−2,0 Pp.".</summary>
    public static string PercentagePoints(double? change, int decimals = 1) =>
        change is { } c ? Signed(c * 100, decimals) + " Pp." : Undefined;

    public static string SignedMinutes(TimeSpan change) => Signed(change.TotalMinutes) + " Min.";

    /// <summary>Clock time of an offset from the start of the shift, e.g. "11:30".</summary>
    public static string Clock(TimeOnly shiftStart, TimeSpan offset) =>
        shiftStart.Add(offset).ToString("HH:mm", CultureInfo.InvariantCulture);
}
