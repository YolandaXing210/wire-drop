using System.Drawing;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace WireDrop
{
    /// <summary>
    /// The wire under a point on the canvas, if there is one.
    ///
    /// Grasshopper does the searching: <c>GH_Document.FindWireAt</c> is public, and it is
    /// the same call Grasshopper's own double-click uses to decide where to put a relay.
    /// Using it rather than re-deriving the curve keeps this correct if a future
    /// Grasshopper changes how wires are drawn.
    ///
    /// The radius is deliberately tighter than Grasshopper's own — six screen pixels where
    /// its relay insert allows fourteen. Every pixel of slack here is a pixel where the
    /// canvas menu is withheld from a right-click, so this band is narrow on purpose and a
    /// wire is a little harder to hit than it is for a relay.
    /// </summary>
    internal static class WireUnder
    {
        /// <summary>
        /// Slack around the wire, in screen pixels. Deliberately less than the fourteen
        /// Grasshopper allows itself for a relay — see above.
        /// </summary>
        const float Slack = 6f;

        public static bool Find(GH_Canvas canvas, PointF point,
                                out IGH_Param upstream, out IGH_Param downstream)
        {
            upstream = null;
            downstream = null;

            var doc = canvas?.Document;
            if (doc == null) return false;

            // Wires are drawn behind objects, so one passing under a component belongs to
            // the component as far as the cursor is concerned. A group is not in the way:
            // it is a box drawn round things, and the wires inside it are still reachable.
            var over = doc.FindAttribute(point, false)?.DocObject;
            if (over != null && !(over is GH_Group)) return false;

            // The radius is in canvas units, and the canvas is zoomable.
            var radius = Slack * canvas.Viewport.ZoomInverse;

            return doc.FindWireAt(point, radius, ref upstream, ref downstream)
                   && upstream != null && downstream != null;
        }

        public static bool Exists(GH_Canvas canvas, PointF point) =>
            Find(canvas, point, out _, out _);
    }
}
