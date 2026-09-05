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

        /// <param name="dragType">Short type name of the port the wire came from.</param>
        /// <param name="fromInput">True when the drag started at an input, so we want producers.</param>
        /// <param name="query">Search text; empty shows the ranked default.</param>
        /// <param name="showAll">Tab widens past the compatible set to the whole library.</param>
        /// <param name="category">Category row filter, or null for all.</param>
        public static HitList Build(string dragType, bool fromInput, string query,
                                    bool showAll, string category)
        {
            var catalog = ComponentCatalog.Instance;
            var q = (query ?? string.Empty).Trim();
            var hasQuery = q.Length > 0;
            var recent = RecentPicks.For(dragType, fromInput);

            // A lone symbol names a component outright — Grasshopper reads "+" as Addition.
            // It is promoted within the catalog rather than synthesised, so its ports,
            // bands, icon and wiring need no special case; it only leads the list.
            var promoted = Guid.Empty;
            var promotedId = ShortcutMap.Component(q);
            if (promotedId != null) Guid.TryParse(promotedId, out promoted);

            // Dragging from an output needs components that consume; from an input, ones that produce.
            var wantInputs = !fromInput;

            var groups = new List<Group>();
            foreach (var entry in catalog.Entries)
            {
                var ports = entry.PortsOn(wantInputs);
                if (ports.Length == 0) continue;

                var matched = new List<(PortSpec Port, int Score)>();
                var best = 0;
                foreach (var port in ports)
                {
                    var score = fromInput
                        ? TypeCompat.Score(port.TypeName, dragType)   // port produces -> our input takes
                        : TypeCompat.Score(dragType, port.TypeName);  // our output -> port takes
                    if (score <= 0)
                    {
                        if (!showAll) continue;
                        score = 0;
                    }
                    matched.Add((port, score));
                    if (score > best) best = score;
                }
                if (matched.Count == 0) continue;

                // A component earns its place on its best port; showing its weaker ports too
                // padded the list with things like "Blend Colours -> Colour A" for a Number.
                var band = TypeCompat.Band(best);
                matched = matched.Where(m => TypeCompat.Band(m.Score) == band).ToList();
                if (matched.Count == 0) continue;

                var textScore = 0;
                if (hasQuery)
                {
                    textScore = promoted != Guid.Empty && entry.Id == promoted
                        ? Promoted
                        : Fuzzy.Score(entry.Name, entry.NickName, entry.Category, entry.SubCategory, q);
                    if (textScore <= 0) continue;
                }

                matched.Sort((a, b) => b.Score != a.Score ? b.Score - a.Score : a.Port.Index - b.Port.Index);

                groups.Add(new Group
                {
                    Entry = entry,
                    Band = band,
                    Best = best,
                    TextScore = textScore,
                    Recent = recent.IndexOf(entry.Id),
                    Ports = matched,
                });
            }

            groups.Sort((a, b) => Compare(a, b, hasQuery));

            // Tallied across every group, so selecting a category never hides the others,
            // and ordered like Grasshopper's ribbon so the row does not reshuffle as counts change.
            var categories = groups
                .GroupBy(g => g.Entry.Category, StringComparer.Ordinal)
                .Select(gr => new CategoryTally { Name = gr.Key, Count = gr.Sum(g => g.Ports.Count) })
                .OrderBy(c => c.Name, Comparer<string>.Create(CategoryOrder.Compare))
                .ToArray();

            if (!string.IsNullOrEmpty(category))
                groups = groups.Where(g =>
                    string.Equals(g.Entry.Category, category, StringComparison.Ordinal)).ToList();

            // A shortcut is an explicit intent rather than a search, so it leads the list.
            var implied = Implied.TryBuild(dragType, fromInput, q, showAll, category);

            var rows = new List<object>();
            if (implied != null) rows.Add(implied);
            var currentBand = -1;
            foreach (var g in groups)
            {
                if (!hasQuery && g.Band != currentBand)
                {
                    currentBand = g.Band;
                    rows.Add(new BandHeader { Band = g.Band, Label = TypeCompat.BandLabel(g.Band) });
                }
                for (int i = 0; i < g.Ports.Count; i++)
                    rows.Add(new Hit
                    {
                        Component = g.Entry,
                        Port = g.Ports[i].Port,
                        Score = g.Ports[i].Score,
                        FirstOfGroup = i == 0,
                    });
            }

            return new HitList
            {
                Rows = rows.ToArray(),
                Categories = categories,
                PortCount = groups.Sum(g => g.Ports.Count) + (implied != null ? 1 : 0),
                ComponentCount = groups.Count + (implied != null ? 1 : 0),
            };
        }

        static int Compare(Group a, Group b, bool hasQuery)
        {
            if (hasQuery && b.TextScore != a.TextScore) return b.TextScore - a.TextScore;
            if (b.Band != a.Band) return b.Band - a.Band;

            // Anything you have actually picked for this wire before wins outright.
            var ra = a.Recent < 0 ? int.MaxValue : a.Recent;
            var rb = b.Recent < 0 ? int.MaxValue : b.Recent;
            if (ra != rb) return ra - rb;

            if (a.Entry.Obscure != b.Entry.Obscure) return a.Entry.Obscure ? 1 : -1;
            if (a.Entry.Popularity != b.Entry.Popularity) return a.Entry.Popularity - b.Entry.Popularity;
            if (a.Entry.Exposure != b.Entry.Exposure) return (int)a.Entry.Exposure - (int)b.Entry.Exposure;
            if (b.Best != a.Best) return b.Best - a.Best;
            return string.Compare(a.Entry.Name, b.Entry.Name, StringComparison.OrdinalIgnoreCase);
        }

        sealed class Group
        {
            public ComponentEntry Entry;
            public int Band;
            public int Best;
            public int TextScore;
            public int Recent;
            public List<(PortSpec Port, int Score)> Ports;
        }
    }


}
