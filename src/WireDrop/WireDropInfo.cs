using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace WireDrop
{
    public class WireDropInfo : GH_AssemblyInfo
    {
        public override string Name => "WireDrop";
        public override Bitmap Icon => null;
        public override string Description =>
            "Pull a wire off any port and release it on empty canvas to search everything it can connect to.";
        public override Guid Id => new Guid("7B1D4A2E-9C63-4E58-9E31-2C0A7F5D8B41");
        public override string AuthorName => "WireDrop";
        public override string AuthorContact => "";
        public override string Version => "0.1.0";
    }
}
