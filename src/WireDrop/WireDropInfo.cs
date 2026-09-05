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
        public override string AuthorName => "Yolanda Xing";
        public override string AuthorContact => "https://github.com/YolandaXing210/wire-drop";

        /// <summary>
        /// Read off the assembly rather than written down here. Yak takes this as the
        /// package's content version, so a hard-coded number silently ships a package
        /// whose contents disagree with its manifest.
        /// </summary>
        public override string Version
        {
            get
            {
                var version = typeof(WireDropInfo).Assembly.GetName().Version;
                return version == null
                    ? "0.0.0"
                    : version.Major + "." + version.Minor + "." + version.Build;
            }
        }
    }
}
