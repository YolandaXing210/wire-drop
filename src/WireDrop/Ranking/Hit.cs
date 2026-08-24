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
        public int Count;
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
