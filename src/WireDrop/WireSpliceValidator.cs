using System;
using System.Drawing;
using Grasshopper.GUI.Canvas;

namespace WireDrop
{
    /// <summary>
    /// Keeps Grasshopper's canvas menu off a wire, so a right-click there is free for the
    /// panel.
    ///
    /// Grasshopper asks its validators before showing that menu, and asks about one point
    /// at a time — so the menu is only ever withheld where a wire actually is, and a hair
    /// to either side it opens as it always did. This is the sanctioned way to answer the
    /// question: <c>AddValidator</c> puts us in a collection alongside anyone else's, and
    /// nothing is patched. Double-click, the obvious gesture, could not be had this way:
    /// Grasshopper's relay insert never asks anybody.
    /// </summary>
    internal sealed class WireSpliceValidator : GH_CanvasValidator
    {
        readonly GH_Canvas _canvas;

        public WireSpliceValidator(GH_Canvas canvas) { _canvas = canvas; }

        public override bool CanShowCanvasMenu(PointF point)
        {
            try
            {
                if (!Settings.Enabled || !Settings.SpliceOnWire) return true;
                return !WireUnder.Exists(_canvas, point);
            }
            catch (Exception ex)
            {
                // Never take the menu away because something went wrong.
                Log.Error("canvas-menu-validator", ex);
                return true;
            }
        }
    }
}
