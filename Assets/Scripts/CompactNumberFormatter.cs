using System;
using System.Globalization;

public static class CompactNumberFormatter
{
    private static readonly string[] Suffixes =
    {
        "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc"
    };

    public static string Format(double value)
    {
        if (double.IsNaN(value)) return "0";
        if (double.IsPositiveInfinity(value)) return "∞";
        if (double.IsNegativeInfinity(value)) return "-∞";

        bool negative = value < 0d;
        double number = Math.Abs(value);
        if (number < 1000d)
        {
            long rounded = (long)Math.Round(number, MidpointRounding.AwayFromZero);
            if (number > 0d && rounded == 0) rounded = 1;
            if (rounded < 1000)
                return (negative ? "-" : "") + rounded.ToString(CultureInfo.InvariantCulture);
            number = rounded;
        }

        int suffix = 0;
        while (number >= 1000d && suffix < Suffixes.Length - 1)
        {
            number /= 1000d;
            suffix++;
        }

        int decimals = number < 100d ? 1 : 0;
        double compact = Math.Round(number, decimals, MidpointRounding.AwayFromZero);
        if (compact >= 1000d && suffix < Suffixes.Length - 1)
        {
            compact /= 1000d;
            suffix++;
            decimals = compact < 100d ? 1 : 0;
        }

        string pattern = decimals > 0 ? "0.#" : "0";
        return (negative ? "-" : "")
            + compact.ToString(pattern, CultureInfo.InvariantCulture)
            + Suffixes[suffix];
    }
}
