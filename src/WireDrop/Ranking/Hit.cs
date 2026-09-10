using System;
using Grasshopper.Kernel;
using WireDrop.Catalog;

namespace WireDrop.Ranking
{
    /// <summary>One row: a component paired with one of its connectable ports.</summary>
    internal sealed class Hit
    {
        public ComponentEntry Component;
        public PortSpec Port;
        public int Score;
        /// <summary>First row of a component's run — carries the icon and the name.</summary>
        public bool FirstOfGroup;
        /// <summary>
        /// Set only on a shortcut row — slider, panel, scribble, point: makes the object
        /// the typed text implies. Null on every ordinary row, which is built from a proxy.
        /// </summary>
        public Func<IGH_DocumentObject> Create;
        /// <summary>False for a scribble, which has no ports to wire to.</summary>
        public bool Connects = true;
    }

    /// <summary>A band heading drawn between runs of rows.</summary>
    internal sealed class BandHeader
    {
        public int Band;
        public string Label;
    }

    internal sealed class CategoryTally
    {
        public string Name;
        /// <summary>Ports this wire can reach in the category, whatever is typed.</summary>
        public int Count;
        /// <summary>How many of those survive the current search text.</summary>
        public int Matches;
    }

    internal sealed class HitList
    {
        /// <summary>Rows and band headers, in display order. Entries are Hit or BandHeader.</summary>
        public object[] Rows = System.Array.Empty<object>();
        public CategoryTally[] Categories = System.Array.Empty<CategoryTally>();
        public int PortCount;
        public int ComponentCount;
    }
}
