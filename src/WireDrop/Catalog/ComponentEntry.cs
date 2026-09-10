using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace WireDrop.Catalog
{
    internal sealed class ComponentEntry
    {
        public Guid Id;
        public string Name;
        public string NickName;
        public string Category;
        public string SubCategory;
        /// <summary>Grasshopper's own description of the component, shown under the list.</summary>
        public string Description;
        public Bitmap Icon;
        public GH_Exposure Exposure;
        /// <summary>Grasshopper hides these from the ribbon; they rank below everything else.</summary>
        public bool Obscure;
        /// <summary>
        /// Obsolete, or hidden outright by whoever wrote it. Kept out of the compatible
        /// list and out of search, and reachable only by asking for everything with Tab.
        /// </summary>
        public bool Hidden;
        public PortSpec[] Inputs = Array.Empty<PortSpec>();
        public PortSpec[] Outputs = Array.Empty<PortSpec>();

        /// <summary>Position in the built-in frequency list, or int.MaxValue when absent.</summary>
        public int Popularity = int.MaxValue;

        public string CategoryPath =>
            string.IsNullOrEmpty(SubCategory) ? Category : Category + " ▸ " + SubCategory;

        public PortSpec[] PortsOn(bool inputs) => inputs ? Inputs : Outputs;
    }
}
