using System;
using System.Collections.Generic;

namespace WireDrop.Ranking
{
    /// <summary>
    /// Grasshopper's own category order — the order of the ribbon tabs.
    ///
    /// The SDK does not expose it: the server keeps categories in a SortedList keyed by
    /// name, so the registration order is lost by the time anything can read it. This
    /// list is that registration order, taken from where Grasshopper declares it when it
    /// builds the component server. Anything not registered there is third-party and
    /// follows alphabetically, which is where the ribbon puts it too.
    /// </summary>
    internal static class CategoryOrder
    {
        static readonly string[] Canonical =
        {
            "Params",
            "Maths",
            "Sets",
            "Vector",
            "Curve",
            "Surface",
            "Mesh",
            "Intersect",
            "Transform",
            "Display",
            "Rhino",
            "Kangaroo2",
        };

        static readonly Dictionary<string, int> Index = Build();

        static Dictionary<string, int> Build()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Canonical.Length; i++) map[Canonical[i]] = i;
            return map;
        }

        /// <summary>Rank of a category; unregistered categories share the last slot.</summary>
        public static int Of(string category)
        {
            if (string.IsNullOrEmpty(category)) return int.MaxValue;
            return Index.TryGetValue(category, out var i) ? i : int.MaxValue;
        }

        /// <summary>Ribbon order first, then alphabetical for everything Grasshopper does not register.</summary>
        public static int Compare(string a, string b)
        {
            var ra = Of(a);
            var rb = Of(b);
            if (ra != rb) return ra.CompareTo(rb);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
