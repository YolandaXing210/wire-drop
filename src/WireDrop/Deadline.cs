using System.Diagnostics;

namespace WireDrop
{
    /// <summary>
    /// A backstop on how long one piece of work may hold Rhino's UI thread.
    ///
    /// Everything here runs on that thread, because Grasshopper's document is not safe to
    /// touch from any other — so nothing can be stopped from outside once it has started.
    /// Work that could run long checks <see cref="Passed"/> between steps instead and, when
    /// it has, stops and hands back what it has so far.
    ///
    /// It is not the budget the ordinary case is designed to: a frame is sixteen
    /// milliseconds, and <c>CastBudget</c> keeps the known slow calls far
    /// inside one. This is for what nobody measured — a third-party component that takes
    /// seconds to construct, a definition of a size nobody tried.
    ///
    /// Free of Grasshopper types, so the tests can hold it to its word.
    /// </summary>
    internal readonly struct Deadline
    {
        public const int LimitMs = 3000;

        readonly long _end;

        Deadline(long end) { _end = end; }

        public static Deadline After(int milliseconds = LimitMs) =>
            new Deadline(Stopwatch.GetTimestamp() + milliseconds * Stopwatch.Frequency / 1000);

        /// <summary>Cheap enough to ask on every step of a loop: one timestamp read.</summary>
        public bool Passed => Stopwatch.GetTimestamp() >= _end;
    }
}
