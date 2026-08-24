using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace WireDrop.UI
{
    /// <summary>
    /// Every dimension in the panel is derived from Grasshopper's own UI font rather than
    /// hard-coded, so the panel tracks the font size set in Grasshopper's preferences and
    /// scales with the display instead of being tuned for one machine.
    /// </summary>
    internal sealed class Metrics
    {
        public readonly Font Body;
        public readonly Font Name;
        public readonly Font Small;

        readonly LayoutMetrics _layout;

        public int RowH => _layout.RowH;
        public int HeadH => _layout.HeadH;
        public int ChipH => _layout.ChipH;
        public int ChipGap => _layout.ChipGap;
        public int ListH => _layout.ListH;
        public int FootH => _layout.FootH;
        public int IconSize => _layout.IconSize;
        public int Pad => _layout.Pad;
        public readonly int Width;

        public Metrics()
        {
            Body = Safe(() => GH_FontServer.Standard, SystemFonts.DefaultFont);
            Name = Safe(() => GH_FontServer.StandardBold, new Font(Body, FontStyle.Bold));
            Small = Safe(() => GH_FontServer.Small, Body);

            _layout = LayoutMetrics.From(Body.Height, Small.Height);

            // Wide enough for a long component-and-port pair without truncating either.
            var sample = Measure("Divide Distance with Reference", Name)
                       + Measure("  ▸  ", Body)
                       + Measure("Reference point parameter", Name)
                       + IconSize + Pad * 3 + 14;
            Width = Math.Min(560, Math.Max(340, sample));
        }

        internal static int Measure(string text, Font font)
        {
            try { return GH_FontServer.StringWidth(text, font); }
            catch { return (int)(text.Length * font.Size * 0.62f); }
        }

        static Font Safe(Func<Font> get, Font fallback)
        {
            try { return get() ?? fallback; } catch { return fallback; }
        }
    }
}
