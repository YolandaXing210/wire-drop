using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using WireDrop.Catalog;

namespace WireDrop
{
    /// <summary>Creates the chosen component at the drop point and connects the wire.</summary>
    internal static class Placement
    {
        public static void Insert(GH_Canvas canvas, IGH_Param source, bool fromInput,
                                  PointF dropPoint, ComponentEntry entry, PortSpec port,
                                  string dragType)
        {
            Log.Guard("placement", () =>
            {
                var doc = canvas?.Document;
                if (doc == null || source == null || entry == null || port == null) return;

                var proxy = Instances.ComponentServer.EmitObjectProxy(entry.Id);
                var obj = proxy?.CreateInstance();
                if (obj == null) return;

                obj.CreateAttributes();
                if (obj.Attributes == null) return;
                ApplyFullNames(obj);
                obj.Attributes.Pivot = dropPoint;

                // Dragging from an output needs the new component's input, and vice versa.
                var wantInput = !fromInput;

                // Record before mutating so one Ctrl+Z takes back the component and the wire.
                if (fromInput) doc.UndoUtil.RecordWireEvent("Wire Drop", source);
                doc.UndoUtil.RecordAddObjectEvent("Wire Drop", obj);
                if (fromInput) doc.UndoUtil.MergeRecords(2);

                doc.AddObject(obj, false);

                var target = ResolveParam(obj, wantInput, port.Index);
                if (target == null) return;

                AlignGrip(obj, target, wantInput, dropPoint);

                if (fromInput) source.AddSource(target);
                else target.AddSource(source);

                RecentPicks.Record(dragType, fromInput, entry.Id);

                doc.NewSolution(false);
                canvas.Refresh();
            });
        }

        /// <summary>
        /// Grasshopper's "Draw Full Names" is applied once when an object is created, not
        /// at render time — GH_Canvas.InstantiateNewObject walks the new object's attribute
        /// tree and copies each Name over its NickName. Creating the object ourselves
        /// bypasses that, which is why placed components came out showing "C" and "N"
        /// while the rest of the canvas showed "Curve" and "Count".
        /// </summary>
        static void ApplyFullNames(IGH_DocumentObject obj)
        {
            try
            {
                if (!CentralSettings.CanvasFullNames) return;

                var tree = new List<IGH_Attributes>();
                obj.Attributes.AppendToAttributeTree(tree);
                foreach (var attributes in tree)
                {
                    var target = attributes?.DocObject;
                    if (target == null || string.IsNullOrEmpty(target.Name)) continue;
                    target.NickName = target.Name;
                }
            }
            catch (Exception ex) { Log.Error("full-names", ex); }
        }

        static IGH_Param ResolveParam(IGH_DocumentObject obj, bool wantInput, int index)
        {
            if (obj is IGH_Component component)
            {
                var list = wantInput ? component.Params.Input : component.Params.Output;
                if (list == null || list.Count == 0) return null;
                return list[Math.Max(0, Math.Min(index, list.Count - 1))];
            }
            return obj as IGH_Param;
        }

        /// <summary>
        /// Nudges the whole component so the port we are wiring sits exactly under the
        /// cursor — the wire then reads as one straight run instead of doubling back.
        /// </summary>
        static void AlignGrip(IGH_DocumentObject obj, IGH_Param target, bool wantInput, PointF dropPoint)
        {
            try
            {
                obj.Attributes.ExpireLayout();
                obj.Attributes.PerformLayout();

                var attr = target.Attributes ?? obj.Attributes;
                if (wantInput && !attr.HasInputGrip) return;
                if (!wantInput && !attr.HasOutputGrip) return;

                var grip = wantInput ? attr.InputGrip : attr.OutputGrip;
                var pivot = obj.Attributes.Pivot;
                obj.Attributes.Pivot = new PointF(
                    pivot.X + (dropPoint.X - grip.X),
                    pivot.Y + (dropPoint.Y - grip.Y));

                obj.Attributes.ExpireLayout();
                obj.Attributes.PerformLayout();
            }
            catch (Exception ex) { Log.Error("align-grip", ex); }
        }
    }
}
