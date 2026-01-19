using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ModernSalesApp.Core;

public static partial class InputParsers
{
    private static readonly CultureInfo ViCulture = new("vi-VN");

    public static bool TryParseMoneyVnd(string input, out long value)
    {
        value = 0;
        input = (input ?? string.Empty).Trim();
        if (input.Length == 0)
        {
            return false;
        }

        input = input.Replace(".", "").Replace(",", "").Replace(" ", "");

        var match = MoneyPattern().Match(input);
        if (!match.Success)
        {
            return false;
        }

        var numberPart = match.Groups["num"].Value;
        var suffix = match.Groups["suffix"].Value.ToLowerInvariant();

        if (!decimal.TryParse(numberPart, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
        {
            return false;
        }

        var multiplier = suffix switch
        {
            "" => 1m,
            "k" => 1_000m,
            "m" => 1_000_000m,
            _ => 1m
        };

        d *= multiplier;

        if (d < 0 || d > long.MaxValue)
        {
            return false;
        }

        value = (long)Math.Round(d, MidpointRounding.AwayFromZero);
        return true;
    }

    public static string FormatMoneyVnd(long value)
    {
        return value.ToString("N0", ViCulture);
    }

    public static string NormalizeSearchText(string input)
    {
        input = (input ?? string.Empty).Trim();
        if (input.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        var inWhitespace = false;
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!inWhitespace)
                {
                    sb.Append(' ');
                    inWhitespace = true;
                }
                continue;
            }

            inWhitespace = false;
            sb.Append(ch);
        }

        return sb.ToString().Trim().ToLower(ViCulture);
    }

    [GeneratedRegex("^(?<num>\\d+(?:\\.\\d{1,2})?)(?<suffix>[kKmM]?)$")]
    private static partial Regex MoneyPattern();
}
