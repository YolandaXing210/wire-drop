using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Special;
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

                Finish(canvas, doc, source, fromInput, dropPoint, obj, port.Index, true);
                RecentPicks.Record(dragType, fromInput, entry.Id);
            });
        }

        /// <summary>
        /// Places the object a typed shortcut implies — slider, panel, scribble, point —
        /// and wires it in. The factory travels on the row, so all of the parsing stays
        /// with Grasshopper's own parsers in <see cref="Ranking.Implied"/>. Full names are
        /// deliberately not applied: on these objects the label is the content, not a
        /// component name, so copying Name over NickName would overwrite what was typed.
        /// </summary>
        public static void InsertImplied(GH_Canvas canvas, IGH_Param source, bool fromInput,
                                         PointF dropPoint, Func<IGH_DocumentObject> create,
                                         bool connects)
        {
            Log.Guard("implied-placement", () =>
            {
                var doc = canvas?.Document;
                if (doc == null || source == null || create == null) return;

                var obj = create();
                if (obj == null) return;

                obj.CreateAttributes();
                if (obj.Attributes == null) return;

                Finish(canvas, doc, source, fromInput, dropPoint, obj, 0, connects);
            });
        }

        /// <summary>
        /// Puts the chosen component in the middle of a wire that is already there: the
        /// old connection goes and the wire runs through the new object instead.
        ///
        /// Deliberately not routed through <see cref="Finish"/>. That one wires a single
        /// port and leaves the object hanging from it; this one has to take a connection
        /// apart and make two, and what it hangs by is the object itself.
        /// </summary>
        public static void Splice(GH_Canvas canvas, IGH_Param upstream, IGH_Param downstream,
                                  PointF pointOnWire, ComponentEntry entry,
                                  PortSpec inPort, PortSpec outPort, string dragType)
        {
            Log.Guard("splice", () =>
            {
                var doc = canvas?.Document;
                if (doc == null || upstream == null || downstream == null) return;
                if (entry == null || inPort == null || outPort == null) return;

                var proxy = Instances.ComponentServer.EmitObjectProxy(entry.Id);
                var obj = proxy?.CreateInstance();
                if (obj == null) return;

                obj.CreateAttributes();
                if (obj.Attributes == null) return;
                ApplyFullNames(obj);
                obj.Attributes.Pivot = pointOnWire;

                var newIn = ResolveParam(obj, true, inPort.Index);
                var newOut = ResolveParam(obj, false, outPort.Index);
                if (newIn == null || newOut == null) return;

                // Where the old wire sat among the sources of the port it fed. An input
                // taking several wires cares about their order, so the new one takes the
                // old one's place rather than being added at the end of the list.
                var at = downstream.Sources.IndexOf(upstream);
                if (at < 0)
                {
                    Log.Once("splice-source-gone",
                             "the wire was taken apart before the panel closed; nothing was changed.");
                    return;
                }

                // Recorded before anything moves, so one Ctrl+Z takes back the object and
                // both new wires and puts the original one back.
                doc.UndoUtil.RecordWireEvent("Wire Splice", downstream);
                doc.UndoUtil.RecordAddObjectEvent("Wire Splice", obj);
                doc.UndoUtil.MergeRecords(2);

                doc.AddObject(obj, false);

                downstream.RemoveSource(upstream);
                newIn.AddSource(upstream);

                // A faint or hidden wire stays that way through the splice, the way it does
                // through a relay Grasshopper inserts itself.
                newIn.WireDisplay = downstream.WireDisplay;

                downstream.AddSource(newOut, at);

                // Centred on the point that was clicked, so the wire runs through it
                // rather than starting at its corner.
                var carry = CentreOn(obj, pointOnWire);

                doc.NewSolution(false);
                canvas.Refresh();

                RecentPicks.Record(dragType, false, entry.Id);

                // Placed and wired, but not yet settled: it rides the cursor until a click.
                FollowPlacement.Begin(canvas, obj, null, false, pointOnWire, carry);
            });
        }

        /// <summary>
        /// Puts the object's middle on a point and hands back the offset that keeps it
        /// there, for the cursor to carry it by. Laid out before, since an object that has
        /// never been drawn has no bounds yet, and again after the move.
        ///
        /// Every layout call the splice makes lives in here, inside the one try/catch. A
        /// component with a broken Layout() of its own is a real input — WireDrop places
        /// whatever is in the catalogue — and a throw that escaped to the caller would
        /// abandon the splice with the document already rewired and no new solution run.
        /// Hanging by the pivot rather than the middle is the whole cost of failing here.
        /// </summary>
        static SizeF CentreOn(IGH_DocumentObject obj, PointF point)
        {
            try
            {
                obj.Attributes.ExpireLayout();
                obj.Attributes.PerformLayout();

                var bounds = obj.Attributes.Bounds;
                var pivot = obj.Attributes.Pivot;
                var carry = new SizeF(pivot.X - (bounds.Left + bounds.Width / 2f),
                                      pivot.Y - (bounds.Top + bounds.Height / 2f));

                obj.Attributes.Pivot = new PointF(point.X + carry.Width, point.Y + carry.Height);
                obj.Attributes.ExpireLayout();
                obj.Attributes.PerformLayout();
                return carry;
            }
            catch (Exception ex) { Log.Error("centre-on", ex); return SizeF.Empty; }
        }

        /// <summary>
        /// Adds the object at the drop point, wires it if it has anything to wire, and
        /// records both as one undo step.
        /// </summary>
        static void Finish(GH_Canvas canvas, GH_Document doc, IGH_Param source, bool fromInput,
                           PointF dropPoint, IGH_DocumentObject obj, int portIndex, bool connects)
        {
            obj.Attributes.Pivot = dropPoint;

            // Record before mutating so one Ctrl+Z takes back the object and the wire. Only
            // a drag from an input changes an existing object; the other direction is all
            // inside the new one, which the add event already covers.
            var rewiring = connects && fromInput;
            if (rewiring) doc.UndoUtil.RecordWireEvent("Wire Drop", source);
            doc.UndoUtil.RecordAddObjectEvent("Wire Drop", obj);
            if (rewiring) doc.UndoUtil.MergeRecords(2);

            doc.AddObject(obj, false);

            // Dragging from an output needs the new object's input, and vice versa.
            var wantInput = !fromInput;
            IGH_Param target = null;
            if (connects)
            {
                target = ResolveParam(obj, wantInput, portIndex);
                if (target != null)
                {
                    AlignGrip(obj, target, wantInput, dropPoint);
                    if (fromInput) source.AddSource(target);
                    else target.AddSource(source);
                }
            }

            doc.NewSolution(false);
            canvas.Refresh();

            // Placed and wired, but not yet settled: it rides the cursor until a click.
            FollowPlacement.Begin(canvas, obj, target, wantInput, dropPoint);
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
        internal static void AlignGrip(IGH_DocumentObject obj, IGH_Param target, bool wantInput, PointF dropPoint)
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
