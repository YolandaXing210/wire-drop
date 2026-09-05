using System;

namespace WireDrop.Ranking
{
    /// <summary>
    /// The label on the typed-number row: what pressing Enter is actually going to make.
    /// The value comes first because it is what was typed; the range follows because
    /// Grasshopper, not the typist, chose it. Free of Grasshopper types so it is testable
    /// without a Rhino install.
    /// </summary>
    internal static class SliderText
    {
        /// <summary>
        /// A cheap gate ahead of Grasshopper's parser: the text has to contain a digit.
        /// Grasshopper's expression parser resolves constants, so "pi" and "e" parse as
        /// numbers — harmless in its own search box, where every hit is scored and sorted,
        /// but here the top row is the one Enter takes, and someone typing "pi" is reaching
        /// for Pipe.
        /// </summary>
        public static bool HasDigit(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var c in text) if (c >= '0' && c <= '9') return true;
            return false;
        }

        public static string Describe(string value, string minimum, string maximum)
        {
            value = (value ?? string.Empty).Trim();
            minimum = (minimum ?? string.Empty).Trim();
            maximum = (maximum ?? string.Empty).Trim();

            if (value.Length == 0) return string.Empty;
            if (minimum.Length == 0 || maximum.Length == 0) return value;

            // A collapsed range says nothing the value has not already said.
            if (string.Equals(minimum, maximum, StringComparison.Ordinal)) return value;

            return value + "   " + minimum + " to " + maximum;
        }
    }
}
