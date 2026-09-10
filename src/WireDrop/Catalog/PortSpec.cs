using System;

namespace WireDrop.Catalog
{
    /// <summary>One input or output of a component, as it will appear as a row in the panel.</summary>
    internal sealed class PortSpec
    {
        public int Index;
        public string Name;
        public string NickName;
        /// <summary>Grasshopper's own description of this port.</summary>
        public string Description;
        /// <summary>Grasshopper's goo type for the port, e.g. GH_Curve.</summary>
        public Type GooType;
        /// <summary>Short name used by the cast table, e.g. "Curve".</summary>
        public string TypeName;
        public bool IsGeneric;
        /// <summary>Grasshopper calls its goo an IGH_GeometricGoo, so a geometry port takes it.</summary>
        public bool IsGeometric;

        public string Label => string.IsNullOrEmpty(NickName) ? Name : NickName;
    }
}
