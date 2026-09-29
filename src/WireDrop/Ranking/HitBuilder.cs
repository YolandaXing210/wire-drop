using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using WireDrop.Catalog;

namespace WireDrop.Ranking
{
    internal static class HitBuilder
    {
        /// <summary>Text score for the component a typed symbol names outright.</summary>
        const int Promoted = 1000;

        /// <summary>
        /// Everything about a ranking that does not depend on what is typed: which
        /// components take the wire, on which ports, in which order. Worked out when the
        /// panel opens or Tab changes the scope, then only searched on each keystroke.
        /// </summary>
        /// <param name="dragType">Short type name of the port the wire came from.</param>
        /// <param name="fromInput">True when the drag started at an input, so we want producers.</param>
        /// <param name="showAll">Tab widens past the compatible set to the whole library.</param>
        /// <param name="values">
        /// What is actually on the wire, when the dragged port's declared type would not
        /// say. Null leaves the ranking to the type table alone.
        /// </param>
        /// <param name="spliceInto">
        /// The short type name of the port on the far end of a wire being spliced into, or
        /// null for an ordinary drop. When set, a component has to take the wire *and*
        /// hand on something the far end can use.
        /// </param>
        public static Candidates Prepare(string dragType, bool fromInput, bool showAll,
                                         ValueCast values = null, string spliceInto = null)
        {
            var catalog = ComponentCatalog.Instance;
            var recent = RecentPicks.For(dragType, fromInput);
            var splicing = spliceInto != null;

            // Dragging from an output needs components that consume; from an input, ones that produce.
            // Splicing always needs one that consumes: the wire arrives before it leaves.
            var wantInputs = splicing || !fromInput;

            var result = new Candidates
            {
                DragType = dragType,
                FromInput = fromInput,
                ShowAll = showAll,
                Labelled = values != null,
                Splicing = splicing,
                CatalogCount = catalog.Count,
                Complete = true,
            };

            // Tallied before the search text is applied, so the row answers "where can this
            // wire go" rather than "what did you just type". Typing narrows the list under
            // it; it does not make places disappear.
            var reach = new Dictionary<string, int>(StringComparer.Ordinal);

            // Should this ever run long, the list is cut off where it got to rather than
            // the panel freezing.
            var deadline = Deadline.After();

            foreach (var entry in catalog.Entries)
            {
                if (deadline.Passed)
                {
                    Log.Once("hits-deadline",
                        "ranking the library took more than " + Deadline.LimitMs / 1000
                        + " seconds, so the list shows what was ranked by then.");
                    result.Complete = false;
                    break;
                }

                // What Grasshopper itself hides stays hidden until Tab asks for everything.
                if (entry.Hidden && !showAll) continue;

                // A splice needs something a wire can go *through*. A slider or a toggle has
                // an output and no way in, so choosing one would cut the wire and hang it on
                // an object that never reads it.
                if (splicing && !entry.TakesWire) continue;

                var ports = entry.PortsOn(wantInputs);
                if (ports.Length == 0) continue;

                var matched = new List<(PortSpec Port, int Score)>();
                var best = 0;
                foreach (var port in ports)
                {
                    var score = fromInput
                        ? TypeCompat.Score(port.TypeName, dragType)   // port produces -> our input takes
                        : TypeCompat.Score(dragType, port.TypeName);  // our output -> port takes
                    if (values != null) score = values.Apply(score, port.GooType);
                    if (score <= 0)
                    {
                        if (!showAll) continue;
                        score = 0;
                    }
                    matched.Add((port, score));
                    if (score > best) best = score;
                }
                if (matched.Count == 0) continue;

                // Splicing: the wire also has to leave again. The component earns its place
                // on its best way out, and every row it contributes is capped by it.
                PortSpec outPort = null;
                if (splicing)
                {
                    var outputs = entry.Outputs;
                    var outTypes = new string[outputs.Length];
                    for (int i = 0; i < outputs.Length; i++) outTypes[i] = outputs[i].TypeName;

                    var outScore = SpliceScore.BestOutput(outTypes, spliceInto, showAll, out var outIndex);
                    if (outIndex >= 0) outPort = outputs[outIndex];

                    // Nothing comes back out of it, so there is nothing to splice it into.
                    if (outPort == null) continue;

                    var kept = new List<(PortSpec Port, int Score)>();
                    foreach (var m in matched)
                    {
                        if (!SpliceScore.Keeps(m.Score, outScore, showAll)) continue;
                        kept.Add((m.Port, SpliceScore.Combine(m.Score, outScore)));
                    }
                    matched = kept;
                    if (matched.Count == 0) continue;

                    best = 0;
                    foreach (var m in matched) if (m.Score > best) best = m.Score;
                }

                // A component earns its place on its best port; showing its weaker ports too
                // padded the list with things like "Blend Colours -> Colour A" for a Number.
                // That is a courtesy of the compatible list, though, not a rule: Tab asked
                // for everything, and everything includes a component's other ports.
                var band = TypeCompat.Band(best);
                if (!showAll)
                {
                    matched.RemoveAll(m => TypeCompat.Band(m.Score) != band);
                    if (matched.Count == 0) continue;
                }

                reach.TryGetValue(entry.Category, out var count);
                reach[entry.Category] = count + matched.Count;

                matched.Sort((a, b) => b.Score != a.Score ? b.Score - a.Score : a.Port.Index - b.Port.Index);

                // The rows themselves are the same whatever is typed, so they are made once.
                var rows = new Hit[matched.Count];
                for (int i = 0; i < rows.Length; i++)
                    rows[i] = new Hit
                    {
                        Component = entry,
                        Port = matched[i].Port,
                        OutPort = outPort,
                        Score = matched[i].Score,
                        FirstOfGroup = i == 0,
                    };

                result.Groups.Add(new Group
                {
                    Entry = entry,
                    Band = band,
                    Best = best,
                    Recent = recent.IndexOf(entry.Id),
                    Rows = rows,
                });
            }

            // The order with nothing typed. A search re-sorts on the text score first and
            // falls back to this, so it is kept as a number rather than worked out again.
            result.Groups.Sort(CompareUntyped);
            for (int i = 0; i < result.Groups.Count; i++) result.Groups[i].Order = i;

            // Ordered like Grasshopper's ribbon, so the row never reshuffles under the cursor.
            result.Reach = reach
                .OrderBy(c => c.Key, Comparer<string>.Create(CategoryOrder.Compare))
                .Select(c => (c.Key, c.Value))
                .ToArray();

            return result;
        }

        /// <param name="query">Search text; empty shows the ranked default.</param>
        /// <param name="category">Category row filter, or null for all.</param>
        public static HitList Build(Candidates candidates, string query, string category)
        {
            var q = (query ?? string.Empty).Trim();
            var hasQuery = q.Length > 0;

            // A lone symbol names a component outright — Grasshopper reads "+" as Addition.
            // It is promoted within the catalog rather than synthesised, so its ports,
            // bands, icon and wiring need no special case; it only leads the list.
            var promoted = Guid.Empty;
            var promotedId = ShortcutMap.Component(q);
            if (promotedId != null) Guid.TryParse(promotedId, out promoted);

            var matches = new Dictionary<string, int>(StringComparer.Ordinal);
            var shown = new List<(Group Group, int Text)>(candidates.Groups.Count);

            foreach (var g in candidates.Groups)
            {
                var textScore = 0;
                if (hasQuery)
                {
                    var entry = g.Entry;
                    textScore = promoted != Guid.Empty && entry.Id == promoted
                        ? Promoted
                        : Fuzzy.Score(entry.Name, entry.NickName, entry.Category, entry.SubCategory, q);
                    if (textScore <= 0) continue;
                }

                matches.TryGetValue(g.Entry.Category, out var n);
                matches[g.Entry.Category] = n + g.Rows.Length;

                if (!string.IsNullOrEmpty(category) &&
                    !string.Equals(g.Entry.Category, category, StringComparison.Ordinal))
                    continue;

                shown.Add((g, textScore));
            }

            // Already in order when nothing is typed; a search only puts the text first.
            if (hasQuery)
                shown.Sort((a, b) => b.Text != a.Text ? b.Text - a.Text : a.Group.Order - b.Group.Order);

            var categories = new CategoryTally[candidates.Reach.Length];
            for (int i = 0; i < categories.Length; i++)
            {
                var (name, count) = candidates.Reach[i];
                matches.TryGetValue(name, out var m);
                categories[i] = new CategoryTally { Name = name, Count = count, Matches = m };
            }

            // A shortcut is an explicit intent rather than a search, so it leads the list.
            // Nothing one makes — slider, panel, scribble, point — is something you put
            // *through* a wire, so a splice has none of them.
            var implied = candidates.Splicing
                ? null
                : Implied.TryBuild(candidates.DragType, candidates.FromInput, q, candidates.ShowAll, category);

            var rows = new List<object>();
            if (implied != null) rows.Add(implied);
            var currentBand = -1;
            var portCount = 0;
            foreach (var (g, _) in shown)
            {
                if (!hasQuery && g.Band != currentBand)
                {
                    currentBand = g.Band;
                    rows.Add(new BandHeader
                    {
                        Band = g.Band,
                        Label = TypeCompat.BandLabel(g.Band, candidates.Labelled),
                    });
                }
                rows.AddRange(g.Rows);
                portCount += g.Rows.Length;
            }

            return new HitList
            {
                Rows = rows.ToArray(),
                Categories = categories,
                PortCount = portCount + (implied != null ? 1 : 0),
                ComponentCount = shown.Count + (implied != null ? 1 : 0),
            };
        }

        static int CompareUntyped(Group a, Group b)
        {
            if (b.Band != a.Band) return b.Band - a.Band;

            // Anything you have actually picked for this wire before wins outright.
            var ra = a.Recent < 0 ? int.MaxValue : a.Recent;
            var rb = b.Recent < 0 ? int.MaxValue : b.Recent;
            if (ra != rb) return ra.CompareTo(rb);

            if (a.Entry.Hidden != b.Entry.Hidden) return a.Entry.Hidden ? 1 : -1;
            if (a.Entry.Obscure != b.Entry.Obscure) return a.Entry.Obscure ? 1 : -1;
            if (a.Entry.Popularity != b.Entry.Popularity) return a.Entry.Popularity.CompareTo(b.Entry.Popularity);
            if (a.Entry.Exposure != b.Entry.Exposure) return (int)a.Entry.Exposure - (int)b.Entry.Exposure;
            if (b.Best != a.Best) return b.Best - a.Best;
            return string.Compare(a.Entry.Name, b.Entry.Name, StringComparison.OrdinalIgnoreCase);
        }

        internal sealed class Group
        {
            public ComponentEntry Entry;
            public int Band;
            public int Best;
            public int Recent;
            /// <summary>Position in the order with nothing typed.</summary>
            public int Order;
            public Hit[] Rows;
        }
    }

    /// <summary>
    /// What <see cref="HitBuilder.Prepare"/> worked out, kept by the panel for as long as
    /// it holds. It stops holding when Tab changes the scope, when the library has grown
    /// since — a panel opened before the idle build finished — or when it was cut off by
    /// the deadline and is worth another try.
    /// </summary>
    internal sealed class Candidates
    {
        public string DragType;
        public bool FromInput;
        public bool ShowAll;
        public bool Labelled;
        public bool Splicing;
        public int CatalogCount;
        public bool Complete;
        public readonly List<HitBuilder.Group> Groups = new List<HitBuilder.Group>();
        /// <summary>Every category the wire reaches, in ribbon order, with its port count.</summary>
        public (string Name, int Count)[] Reach = Array.Empty<(string, int)>();

        public bool StillHolds(bool showAll) =>
            Complete && ShowAll == showAll && CatalogCount == ComponentCatalog.Instance.Count;
    }
}
