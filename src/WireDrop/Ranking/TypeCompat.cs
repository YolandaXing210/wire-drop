using System;
using System.Collections.Generic;

namespace WireDrop.Ranking
{
    /// <summary>
    /// How well a value of one Grasshopper type can travel down a wire into a port of another.
    /// Grasshopper's casting is deliberately permissive, so the score — not a yes/no filter —
    /// is what makes the list usable: 100 is the same type, 85+ is a lossless widening,
    /// the middle is a real conversion, and 50 is a port that takes anything at all.
    /// </summary>
    internal static class TypeCompat
    {
        public const int Exact = 100;
        /// <summary>
        /// A cast Grasshopper itself confirmed against the value on the wire. Above the
        /// direct floor because it is the strongest evidence there is: not "these types
        /// usually go together" but "this value goes into this port".
        /// </summary>
        public const int ValueFloor = 90;
        public const int DirectFloor = 85;
        /// <summary>Anything geometric, arriving at a geometry port unchanged.</summary>
        public const int Geometric = 90;
        public const int ConvertFloor = 55;

        /// <summary>destination port type -> { source type -> score }. "*" accepts anything.</summary>
        static readonly Dictionary<string, Dictionary<string, int>> Accept =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal)
        {
            ["Generic"]   = new() { ["*"] = 50 },
            ["Text"]      = new() { ["*"] = 60 },
            // A geometry port is a container, not a converter: a point goes into one as a
            // point. Scored direct for that reason, and extended at run time — see
            // RegisterGeometric — so this list is a starting point, not the whole truth.
            ["Geometry"]  = new() { ["Point"] = Geometric, ["Curve"] = Geometric, ["Line"] = Geometric,
                                    ["Arc"] = Geometric, ["Circle"] = Geometric, ["Rectangle"] = Geometric,
                                    ["Surface"] = Geometric, ["Brep"] = Geometric, ["Mesh"] = Geometric,
                                    ["SubD"] = Geometric, ["Box"] = Geometric },
            ["Curve"]     = new() { ["Line"] = 90, ["Arc"] = 90, ["Circle"] = 90, ["Rectangle"] = 90,
                                    ["Surface"] = 55, ["Brep"] = 55 },
            ["Brep"]      = new() { ["Surface"] = 90, ["Box"] = 88, ["SubD"] = 70, ["Mesh"] = 60,
                                    ["Circle"] = 55, ["Rectangle"] = 55 },
            ["Surface"]   = new() { ["Brep"] = 70, ["Box"] = 65, ["SubD"] = 60 },
            ["Mesh"]      = new() { ["Brep"] = 70, ["Surface"] = 70, ["Box"] = 70, ["SubD"] = 75 },
            ["SubD"]      = new() { ["Mesh"] = 70, ["Brep"] = 65, ["Surface"] = 65 },
            ["Box"]       = new() { ["Brep"] = 60, ["Surface"] = 60, ["Mesh"] = 60 },
            ["Number"]    = new() { ["Integer"] = 90, ["Boolean"] = 70, ["Text"] = 55,
                                    ["Complex"] = 60, ["Domain"] = 55 },
            // A real number is a complex one whose imaginary part is zero — the same
            // widening as Integer to Number, and nothing is lost going in.
            ["Complex"]   = new() { ["Number"] = 90, ["Integer"] = 88 },
            ["Integer"]   = new() { ["Number"] = 88, ["Boolean"] = 70, ["Text"] = 55 },
            ["Boolean"]   = new() { ["Number"] = 70, ["Integer"] = 70, ["Text"] = 55 },
            // Three numbers either way. Reading them as a position or as a direction is a
            // change of meaning, not of value: nothing is computed, nothing can fail.
            ["Point"]     = new() { ["Vector"] = 90, ["Text"] = 55 },
            ["Vector"]    = new() { ["Point"] = 90, ["Text"] = 55 },
            ["Plane"]     = new() { ["Point"] = 65, ["Circle"] = 70, ["Rectangle"] = 70 },
            ["Line"]      = new() { ["Curve"] = 60, ["Rectangle"] = 55 },
            ["Circle"]    = new() { ["Arc"] = 70, ["Curve"] = 55 },
            ["Arc"]       = new() { ["Circle"] = 70, ["Curve"] = 55 },
            ["Rectangle"] = new() { ["Curve"] = 55, ["Plane"] = 55 },
            ["Domain"]    = new() { ["Number"] = 70, ["Integer"] = 70, ["Text"] = 55 },
            ["Colour"]    = new() { ["Text"] = 60, ["Number"] = 55, ["Integer"] = 55 },
            // The same sixteen numbers under two names.
            ["Transform"] = new() { ["Matrix"] = 90 },
            ["Matrix"]    = new() { ["Transform"] = 90 },
        };

        static readonly Dictionary<string, string> Alias = new(StringComparer.Ordinal)
        {
            ["Angle"] = "Number",
            ["SurfaceOrBrep"] = "Brep",
            ["Circular"] = "Circle",
            ["String"] = "Text",
            ["Interval"] = "Domain",
            ["Interval2D"] = "Domain2",
            ["GeometricGoo"] = "Geometry",
            ["ObjectWrapper"] = "Generic",
            ["Goo"] = "Generic",
            ["Colour"] = "Colour",
            ["Color"] = "Colour",
        };

        /// <summary>
        /// Records that a type is geometry, so that it reaches a geometry port directly.
        ///
        /// Which types those are is not written down here: the catalog asks each port
        /// whether its goo is an <c>IGH_GeometricGoo</c> as it reads it, and says so. That
        /// keeps this correct for geometry types nobody here has heard of — a third-party
        /// one arrives already knowing it belongs. Kept as strings so the scoring stays
        /// free of Grasshopper types and testable without a Rhino install.
        /// </summary>
        public static void RegisterGeometric(string typeName)
        {
            typeName = Normalise(typeName);
            if (string.IsNullOrEmpty(typeName)) return;
            if (string.Equals(typeName, "Geometry", StringComparison.Ordinal)) return;
            Accept["Geometry"][typeName] = Geometric;
        }

        /// <summary>Turns a goo type such as GH_Curve into the short name the table uses.</summary>
        public static string ShortName(Type gooType)
        {
            if (gooType == null) return "Generic";
            var n = gooType.Name;
            if (n.StartsWith("GH_", StringComparison.Ordinal)) n = n.Substring(3);
            if (n.StartsWith("IGH_", StringComparison.Ordinal)) n = n.Substring(4);
            var tick = n.IndexOf('`');
            if (tick > 0) n = n.Substring(0, tick);
            return Alias.TryGetValue(n, out var a) ? a : n;
        }

        public static string Normalise(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "Generic";
            return Alias.TryGetValue(typeName, out var a) ? a : typeName;
        }

        /// <summary>0 when nothing can travel from <paramref name="source"/> into <paramref name="destination"/>.</summary>
        public static int Score(string source, string destination)
        {
            source = Normalise(source);
            destination = Normalise(destination);
            if (string.Equals(source, destination, StringComparison.Ordinal)) return Exact;

            if (Accept.TryGetValue(destination, out var table))
            {
                if (table.TryGetValue("*", out var any)) return any;
                if (table.TryGetValue(source, out var s)) return s;
            }
            // A generic value can be dropped into anything; Grasshopper will try at runtime.
            if (string.Equals(source, "Generic", StringComparison.Ordinal)) return 45;
            return 0;
        }

        /// <summary>2 = direct, 1 = converts, 0 = generic / no conversion.</summary>
        public static int Band(int score) =>
            score >= DirectFloor ? 2 : score >= ConvertFloor ? 1 : 0;

        public static string BandLabel(int band) => BandLabel(band, false);

        /// <param name="valueChecked">
        /// True when the value on the wire was read, which puts confirmed casts in the top
        /// band alongside the type matches — so the heading has to account for both.
        /// </param>
        public static string BandLabel(int band, bool valueChecked) => band switch
        {
            2 => valueChecked
                ? "direct — same type, or checked against the value on the wire"
                : "direct — same type or lossless cast",
            1 => "converts",
            _ => "generic / no conversion",
        };
    }
}
