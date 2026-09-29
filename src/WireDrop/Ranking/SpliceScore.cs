using System;
using System.Collections.Generic;

namespace WireDrop.Ranking
{
    /// <summary>
    /// How well a component sits in the middle of a wire that already exists. Two joins
    /// have to hold rather than one — what the upstream port sends has to reach an input,
    /// and what comes back out has to reach the port the wire was going to — so a row is
    /// worth no more than its worse end, and a row with an end that cannot be made at all
    /// is worth nothing.
    ///
    /// Kept free of Grasshopper types, like the scoring beside it, so the tests can reason
    /// about it without a Rhino install.
    /// </summary>
    internal static class SpliceScore
    {
        /// <summary>The score of a row: the weaker of its two joins.</summary>
        public static int Combine(int inScore, int outScore)
        {
            if (inScore <= 0 || outScore <= 0) return 0;
            return Math.Min(inScore, outScore);
        }

        /// <summary>
        /// The output a splice leaves by, and where it sits in the list: the best-scoring one
        /// for the port downstream of the wire. A tie goes to the earlier output — a component
        /// puts its main result first.
        ///
        /// When Tab has asked for everything and none of them reaches, the first output is
        /// taken anyway, at a score of nothing. That is what Tab means in the rest of this
        /// panel: a drop offers ports the type table rejects, and Grasshopper attempts the
        /// cast when the wire is made. The one case a splice genuinely cannot use is a
        /// component with no outputs at all — there is no way back out of it — and that
        /// still answers -1, whatever Tab asked for.
        /// </summary>
        public static int BestOutput(IReadOnlyList<string> outputTypes, string downstreamType,
                                     bool showAll, out int index)
        {
            index = -1;
            var best = 0;
            if (outputTypes == null) return 0;

            for (int i = 0; i < outputTypes.Count; i++)
            {
                var score = TypeCompat.Score(outputTypes[i], downstreamType);
                if (score <= best) continue;
                best = score;
                index = i;
            }

            if (index < 0 && showAll && outputTypes.Count > 0) index = 0;
            return best;
        }

        /// <summary>
        /// Whether a row survives. Both ends have to reach — a component that takes the
        /// wire but hands on something the far end cannot use is not an insertion, it is a
        /// broken definition. Tab is a request to stop filtering, so it keeps everything.
        /// </summary>
        public static bool Keeps(int inScore, int outScore, bool showAll) =>
            showAll || Combine(inScore, outScore) > 0;
    }
}
