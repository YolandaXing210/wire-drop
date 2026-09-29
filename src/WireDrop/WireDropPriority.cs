using System;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using WireDrop.Catalog;
using WireDrop.Interop;
using WireDrop.UI;

namespace WireDrop
{
    public class WireDropPriority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            try
            {
                if (!WireInteractionFields.Available) return GH_LoadingInstruction.Proceed;

                Instances.CanvasCreated += OnCanvasCreated;
                if (Instances.ActiveCanvas != null) OnCanvasCreated(Instances.ActiveCanvas);

                SettingsMenu.AddWhenReady();

                // Ports are only knowable by instantiating every proxy, so start that
                // now, a slice per idle tick, rather than stalling the first wire drop.
                ComponentCatalog.Instance.BeginBuild();

                // Most plugins load after this one, many of them long after Grasshopper
                // opened; each is read in on idle as it arrives.
                if (Instances.IsComponentServer)
                    Instances.ComponentServer.GHAFileLoaded += OnGhaLoaded;
            }
            catch (Exception ex)
            {
                Log.Error("priority-load", ex);
            }
            return GH_LoadingInstruction.Proceed;
        }

        static void OnGhaLoaded(object sender, EventArgs e) =>
            Log.Guard("catalog-gha-loaded", () => ComponentCatalog.Instance.Refresh());

        static void OnCanvasCreated(GH_Canvas canvas) => Log.Guard("attach", () => CanvasWatcher.Attach(canvas));
    }
}
