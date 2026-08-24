using System;
using System.Drawing;
using Grasshopper.GUI.Canvas;

namespace WireDrop.UI
{
    /// <summary>
    /// The panel has to sit on whatever canvas the user runs. Rather than hard-code
    /// Grasshopper's classic grey, read the canvas colour and pick the matching scheme.
    /// </summary>
    internal sealed class Palette
    {
        public Color Back, Field, FieldBorder, Text, Dim, Line, Strip, Footer;
        public Color Sel, SelText, PortText;
        public Color Chip, ChipText, ChipSel, ChipSelText;

        public static Palette Current()
        {
            var dark = false;
            try
            {
                var c = GH_Skin.canvas_back;
                if (c.A > 0)
                {
                    var lum = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
                    dark = lum < 0.45;
                }
            }
            catch { }
            return dark ? Dark() : Light();
        }

        static Palette Light() => new Palette
        {
            Back = Color.FromArgb(0xF6, 0xF6, 0xF6),
            Field = Color.White,
            FieldBorder = Color.FromArgb(0xA8, 0xA8, 0xA8),
            Text = Color.FromArgb(0x1C, 0x1C, 0x1C),
            Dim = Color.FromArgb(0x76, 0x76, 0x76),
            Line = Color.FromArgb(0xD2, 0xD2, 0xD2),
            Strip = Color.FromArgb(0xE6, 0xE6, 0xE6),
            Footer = Color.FromArgb(0xDC, 0xDC, 0xDC),
            Sel = Color.FromArgb(0xC9, 0xD8, 0x9A),
            SelText = Color.FromArgb(0x1C, 0x1C, 0x1C),
            PortText = Color.FromArgb(0x25, 0x40, 0x1B),
            Chip = Color.FromArgb(0xE2, 0xE2, 0xE2),
            ChipText = Color.FromArgb(0x4C, 0x4C, 0x4C),
            ChipSel = Color.FromArgb(0x7C, 0x8F, 0x1E),
            ChipSelText = Color.White,
        };

        static Palette Dark() => new Palette
        {
            Back = Color.FromArgb(0x2A, 0x2D, 0x2A),
            Field = Color.FromArgb(0x1E, 0x21, 0x1E),
            FieldBorder = Color.FromArgb(0x4A, 0x4F, 0x4A),
            Text = Color.FromArgb(0xE2, 0xE6, 0xE1),
            Dim = Color.FromArgb(0x98, 0xA0, 0x98),
            Line = Color.FromArgb(0x3A, 0x3F, 0x3A),
            Strip = Color.FromArgb(0x23, 0x26, 0x23),
            Footer = Color.FromArgb(0x22, 0x25, 0x22),
            Sel = Color.FromArgb(0x4E, 0x5E, 0x28),
            SelText = Color.FromArgb(0xF0, 0xF4, 0xEA),
            PortText = Color.FromArgb(0xC3, 0xD8, 0x8A),
            Chip = Color.FromArgb(0x34, 0x38, 0x34),
            ChipText = Color.FromArgb(0xB4, 0xBC, 0xB4),
            ChipSel = Color.FromArgb(0x9D, 0xB4, 0x31),
            ChipSelText = Color.FromArgb(0x10, 0x16, 0x0C),
        };
    }
}
