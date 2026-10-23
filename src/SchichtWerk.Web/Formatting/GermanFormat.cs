using System.Globalization;

namespace SchichtWerk.Web.Formatting;

/// <summary>
/// German number formatting that does not depend on the browser's locale.
/// The app runs with invariant globalization, so the separators are set explicitly.
/// </summary>
public static class GermanFormat
{
    private static readonly NumberFormatInfo Numbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
    };

    public static string Number(double value, int decimals = 0) =>
        value.ToString("N" + decimals, Numbers);

    /// <summary>Formats a ratio (0.7778) as a percentage ("77,8 %").</summary>
    public static string Percent(double ratio, int decimals = 1) =>
        Number(ratio * 100, decimals) + " %";

    public static string Minutes(TimeSpan duration) =>
        Number(duration.TotalMinutes) + " Min.";
}
