using System.Diagnostics;

namespace WireDrop.Ranking
{
    /// <summary>
    /// A ceiling on how long one drag may spend asking Grasshopper to cast values.
    ///
    /// It is needed because of where a failed cast goes. When a string will not parse as
    /// coordinates, <c>GH_Point.CastFrom</c> falls through to the secondary conversion,
    /// which calls <c>FindRhinoObjectByNameAndType</c> — Grasshopper's last guess is that
    /// the text names an object in the Rhino document, so it goes and looks. On a document
    /// holding tens of thousands of objects that search is not free, and a failed cast is
    /// the common case here: failing is how the filtering happens.
    ///
    /// The cache keeps that bounded in the ordinary case. This bounds the extraordinary
    /// one. Running out is not an error: every port still gets the answer the type table
    /// gives, which is where the plugin started.
    /// </summary>
    internal sealed class CastBudget
    {
        /// <summary>Comfortably below the frame the drag is drawn in.</summary>
        const long LimitMs = 25;

        readonly Stopwatch _spent = new Stopwatch();
        bool _reported;

        public bool Exhausted
        {
            get
            {
                if (_spent.ElapsedMilliseconds < LimitMs) return false;
                if (!_reported)
                {
                    _reported = true;
                    Log.Once("cast-budget",
                        "this drag spent its " + LimitMs + "ms of casting; the rest of the ports "
                        + "are ranked on their declared types. A very large Rhino document makes "
                        + "failed text casts slow, since Grasshopper searches it by object name.");
                }
                return true;
            }
        }

        public T Charge<T>(System.Func<T> work)
        {
            _spent.Start();
            try { return work(); }
            finally { _spent.Stop(); }
        }
    }
}
