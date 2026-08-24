namespace WireDrop.Catalog
{
    /// <summary>
    /// A first-screen prior. Ranking a band alphabetically opens a Circle drag on
    /// "Containment, Control Points, Control Polygon" — alphabet, not intent. This list
    /// is a starting order only; <see cref="RecentPicks"/> overtakes it as you use the panel.
    /// </summary>
    internal static class Popularity
    {
        public static readonly string[] Names =
        {
            "Divide Curve", "End Points", "Evaluate Curve", "Curve Closest Point", "Offset Curve",
            "Join Curves", "Explode", "Flip Curve", "Length", "Area", "Bounding Box",
            "Move", "Rotate", "Scale", "Orient", "Mirror",
            "Extrude", "Loft", "Sweep1", "Sweep2", "Revolution", "Pipe",
            "Boundary Surfaces", "Surface Closest Point", "Evaluate Surface", "Mesh Surface",
            "Deconstruct Brep", "Cap Holes", "Solid Union", "Solid Difference", "Solid Intersection",
            "Construct Point", "Deconstruct", "Distance", "Vector 2Pt",
            "Unit X", "Unit Y", "Unit Z", "Amplitude",
            "Series", "Range", "Random", "Jitter",
            "List Item", "List Length", "Cull Pattern", "Dispatch", "Sort List",
            "Graft Tree", "Flatten Tree", "Weave", "Merge",
            "Panel", "Number Slider",
            "Multiplication", "Addition", "Subtraction", "Division", "Sine", "Cosine",
            "Remap Numbers", "Bounds", "Construct Domain",
            "Circle", "Line", "Rectangle", "Polyline", "Interpolate", "Nurbs Curve", "Arc",
            "Perp Frame", "Horizontal Frame", "Shatter", "Region Union",
            "Curve | Curve", "Brep | Brep", "Contour", "Project", "Pull Curve",
            "Populate 2D", "Populate 3D", "Voronoi", "Delaunay Mesh", "Convex Hull",
            "Point On Curve", "Fillet", "Rebuild Curve", "Divide Length", "Divide Distance",
            "Curve Middle", "Extrude Point", "Extrude Linear", "Surface Split", "Brep Edges",
            "Deconstruct Point", "Vector XYZ", "Angle", "Cross Product", "Dot Product",
            "Mass Addition", "Bounds 2D",
        };
    }
}
