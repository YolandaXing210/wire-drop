using System;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using WireDrop.Catalog;
using WireDrop.Interop;

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

                // Ports are only knowable by instantiating every proxy, so start that
                // now, a slice per idle tick, rather than stalling the first wire drop.
                ComponentCatalog.Instance.BeginBuild();
            }
            catch (Exception ex)
            {
                Log.Error("priority-load", ex);
            }
            return GH_LoadingInstruction.Proceed;
        }

        static void OnCanvasCreated(GH_Canvas canvas) => Log.Guard("attach", () => CanvasWatcher.Attach(canvas));
    }
}
