using System.Collections.Generic;
using System.Drawing;

namespace WireDrop.UI
{
    /// <summary>
    /// The few rectangles nearest a point.
    ///
    /// It keeps the drop-target outlines to a handful. A wire dragged across a busy
    /// definition could otherwise light up a couple of hundred components at once, which
    /// tells you no more than lighting up none of them would.
    ///
    /// Distance is measured to the nearest edge rather than to the centre, so a wide
    /// component beside the cursor counts as near however far away its middle is: what gets
    /// outlined is the body, so the body is what should decide.
    ///
    /// Free of Grasshopper types, like the layout maths beside it, so the tests can reason
    /// about it without a Rhino install to load assemblies from.
    /// </summary>
    internal static class Nearest
    {
        /// <summary>
        /// Indices of the <paramref name="max"/> rectangles nearest <paramref name="cursor"/>,
        /// nearest first, a tie going to the earlier one.
        /// </summary>
        /// <param name="chosen">
        /// Cleared before it is filled, and the caller's to keep: this runs on every frame of
        /// a drag, so the list is reused rather than allocated.
        /// </param>
        public static void Pick(IReadOnlyList<RectangleF> bounds, PointF cursor, int max,
                                List<int> chosen)
        {
            chosen.Clear();
            if (bounds == null || max <= 0) return;

            // Insertion into a list this short beats sorting the candidates: there can be
            // hundreds of them and only a handful are ever drawn, so the whole order is work
            // nobody looks at.
            var distances = new List<float>(max + 1);

            for (int i = 0; i < bounds.Count; i++)
            {
                var distance = DistanceSquared(bounds[i], cursor);

                // Already holding its quota of nearer ones.
                if (chosen.Count == max && distance >= distances[max - 1]) continue;

                var at = 0;
                while (at < chosen.Count && distances[at] <= distance) at++;

                chosen.Insert(at, i);
                distances.Insert(at, distance);

                if (chosen.Count > max)
                {
                    chosen.RemoveAt(max);
                    distances.RemoveAt(max);
                }
            }
        }

        /// <summary>
        /// Squared, because nothing here needs the distance itself — only which of two is
        /// the smaller, and a square root would not change that answer.
        /// </summary>
        static float DistanceSquared(RectangleF r, PointF p)
        {
            var dx = p.X < r.Left ? r.Left - p.X : p.X > r.Right ? p.X - r.Right : 0f;
            var dy = p.Y < r.Top ? r.Top - p.Y : p.Y > r.Bottom ? p.Y - r.Bottom : 0f;
            return dx * dx + dy * dy;
        }
    }
}
