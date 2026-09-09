using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using WireDrop.Ranking;

namespace WireDrop.UI
{
    /// <summary>
    /// While the wire is still on the cursor, every object on screen that could take it is
    /// outlined in green — so where it can go is visible before the button is released,
    /// not only afterwards in the panel.
    ///
    /// The outline is drawn straight onto the canvas rather than through the objects
    /// themselves: nothing in the document is touched, so a drag that goes nowhere leaves
    /// no trace. The set is worked out once when the drag starts and only tested for
    /// visibility per frame, since a document does not change mid-drag but the viewport can.
    /// </summary>
    internal sealed class DropTargets
    {
        /// <summary>
        /// One colour, one meaning: the value goes in as it is. Anything that would have to
        /// be converted on the way is not outlined at all, so what is green is what will
        /// connect and do exactly what it looks like.
        /// </summary>
        static readonly Color Green = Color.FromArgb(60, 175, 70);

        readonly GH_Canvas _canvas;
        readonly List<Target> _targets = new List<Target>();
        IGH_Param _source;
        ValueCast _values;
        ValueProbe _probe;
        bool _fromInput;
        bool _built;

        readonly struct Target
        {
            public readonly IGH_Attributes Attributes;
            /// <summary>The ports that take it — one dot each, so which is never a guess.</summary>
            public readonly IGH_Param[] Ports;
            public Target(IGH_Attributes attributes, IGH_Param[] ports)
            {
                Attributes = attributes;
                Ports = ports;
            }
        }

        DropTargets(GH_Canvas canvas) { _canvas = canvas; }

        public static DropTargets Attach(GH_Canvas canvas)
        {
            var targets = new DropTargets(canvas);
            canvas.CanvasPostPaintObjects += targets.OnPostPaintObjects;
            canvas.Disposed += (s, e) => canvas.CanvasPostPaintObjects -= targets.OnPostPaintObjects;
            return targets;
        }

        /// <summary>
        /// Called on every mouse move of a drag. The set is rebuilt only when the drag
        /// itself changes, not per move, and never while painting.
        /// </summary>
        public void Show(IGH_Param source, bool fromInput)
        {
            if (_built && ReferenceEquals(source, _source) && fromInput == _fromInput) return;

            _source = source;
            _fromInput = fromInput;
            _built = true;
            Log.Guard("drop-targets", Build);
        }

        public void Hide()
        {
            if (!_built && _targets.Count == 0) return;
            _built = false;
            _source = null;
            _values = null;
            _probe = null;
            _targets.Clear();
            try { _canvas.Invalidate(); } catch { }
        }

        void Build()
        {
            _targets.Clear();
            if (!Settings.Enabled || !Settings.HighlightTargets) return;

            var doc = _canvas.Document;
            if (doc == null || _source == null) return;

            var dragType = TypeCompat.ShortName(SafeType(_source));
            var owner = TopLevel(_source);
            _values = ValueCast.For(_source, _fromInput);
            _probe = ValueProbe.For(_source, _fromInput);

            foreach (var obj in doc.Objects)
            {
                if (obj?.Attributes == null || ReferenceEquals(obj, owner)) continue;
                var ports = Fitting(obj, dragType);
                if (ports.Length == 0) continue;
                _targets.Add(new Target(obj.Attributes, ports));
            }
        }

        /// <summary>
        /// The object's facing ports that take the value as it is — the ones that get a dot.
        /// A port that would convert it does not count: those are the ones that connect and
        /// then quietly do something other than what was meant.
        /// </summary>
        IGH_Param[] Fitting(IGH_DocumentObject obj, string dragType)
        {
            var fits = new List<IGH_Param>();
            if (obj is IGH_Component component)
            {
                // Dragging from an input wants something that produces, and vice versa.
                var ports = _fromInput ? component.Params.Output : component.Params.Input;
                if (ports != null)
                    foreach (var port in ports)
                        if (Takes(Score(port, dragType))) fits.Add(port);
            }
            else if (obj is IGH_Param param)
            {
                if (Takes(Score(param, dragType))) fits.Add(param);
            }
            // anything else — scribbles, groups — has nothing to connect

            return fits.ToArray();
        }

        /// <summary>Band 1 is "converts", and converting is exactly what is not shown.</summary>
        static bool Takes(int score) => score > 0 && TypeCompat.Band(score) != 1;

        /// <summary>
        /// Where the wire would actually land on that port. Grasshopper keeps the point on
        /// the port's own attributes, so this is the same spot its own wires end at.
        /// </summary>
        PointF? Grip(IGH_Param port)
        {
            try
            {
                var attributes = port?.Attributes;
                if (attributes == null) return null;
                if (_fromInput)
                    return attributes.HasOutputGrip ? attributes.OutputGrip : (PointF?)null;
                return attributes.HasInputGrip ? attributes.InputGrip : (PointF?)null;
            }
            catch { return null; }
        }

        int Score(IGH_Param port, string dragType)
        {
            if (port == null) return 0;
            var type = SafeType(port);
            var portType = TypeCompat.ShortName(type);
            if (_fromInput)
            {
                // It produces, our input takes — so it is judged on what it already carries.
                var produced = TypeCompat.Score(portType, dragType);
                return _probe == null ? produced : _probe.Apply(produced, port);
            }

            // Our output, its port takes — judged on the value we are dragging.
            var accepted = TypeCompat.Score(dragType, portType);
            return _values == null ? accepted : _values.Apply(accepted, type);
        }

        void OnPostPaintObjects(GH_Canvas sender)
        {
            if (_targets.Count == 0) return;

            Log.Guard("drop-targets-paint", () =>
            {
                var g = sender.Graphics;
                if (g == null) return;

                // Whatever state the graphics was handed to us in, this puts it in canvas
                // coordinates: ApplyProjection assigns the transform rather than compounding
                // it, and it is how Grasshopper's own window-select interaction draws.
                var viewport = sender.Viewport;
                viewport.ApplyProjection(g);

                // Constant on screen however far the canvas is zoomed out.
                var zoom = Math.Max(0.1f, viewport.Zoom);
                var width = 2f / zoom;
                var smoothing = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // One pen for the frame rather than one per object: a busy canvas can put a
                // few hundred outlines on screen for every frame of the drag.
                using var pen = new Pen(Green, width);
                using var fill = new SolidBrush(Green);
                using var rim = new Pen(Color.FromArgb(210, 255, 255, 255), width * 0.75f);
                var radius = 4.5f / zoom;
                try
                {
                    foreach (var target in _targets)
                    {
                        var bounds = target.Attributes.Bounds;
                        if (bounds.IsEmpty) continue;
                        if (!viewport.IsVisible(ref bounds, 20f)) continue;

                        var outlined = bounds;
                        outlined.Inflate(width + 1f, width + 1f);
                        using (var path = Outline(outlined, 3f + width)) g.DrawPath(pen, path);

                        // A dot on every port that takes it, so which one is never a guess.
                        foreach (var port in target.Ports)
                        {
                            var grip = Grip(port);
                            if (grip == null) continue;
                            var dot = new RectangleF(grip.Value.X - radius, grip.Value.Y - radius,
                                                     radius * 2f, radius * 2f);
                            g.FillEllipse(fill, dot);
                            g.DrawEllipse(rim, dot);
                        }
                    }
                }
                finally { g.SmoothingMode = smoothing; }
            });
        }

        static GraphicsPath Outline(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2f;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        static Type SafeType(IGH_Param param) { try { return param?.Type; } catch { return null; } }

        static IGH_DocumentObject TopLevel(IGH_Param param)
        {
            try { return param?.Attributes?.GetTopLevel?.DocObject; } catch { return null; }
        }
    }
}
