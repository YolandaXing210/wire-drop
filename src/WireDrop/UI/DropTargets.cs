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
        /// One solid colour per band, all fully opaque: green fits directly, yellow needs a
        /// conversion, blue is a port that simply takes anything. Colour carries the
        /// distinction rather than opacity, so a weak fit is as legible as a strong one and
        /// which is which is still obvious at a glance. All three read on either canvas skin.
        /// </summary>
        static readonly Color[] Ink =
        {
            Color.FromArgb(55, 125, 220),    // 0 — takes anything
            Color.FromArgb(240, 190, 20),    // 1 — converts
            Color.FromArgb(60, 175, 70),     // 2 — direct
        };

        readonly GH_Canvas _canvas;
        readonly List<Target> _targets = new List<Target>();
        IGH_Param _source;
        bool _fromInput;
        bool _built;

        readonly struct Target
        {
            public readonly IGH_Attributes Attributes;
            /// <summary>2 direct, 1 converts, 0 generic — green, yellow, blue.</summary>
            public readonly int Band;
            public Target(IGH_Attributes attributes, int band) { Attributes = attributes; Band = band; }
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

            foreach (var obj in doc.Objects)
            {
                if (obj?.Attributes == null || ReferenceEquals(obj, owner)) continue;
                var band = Band(obj, dragType);
                if (band < 0) continue;
                _targets.Add(new Target(obj.Attributes, band));
            }
        }

        /// <summary>The best band any of the object's facing ports can offer, or -1 for none.</summary>
        int Band(IGH_DocumentObject obj, string dragType)
        {
            var best = 0;
            if (obj is IGH_Component component)
            {
                // Dragging from an input wants something that produces, and vice versa.
                var ports = _fromInput ? component.Params.Output : component.Params.Input;
                if (ports == null) return -1;
                foreach (var port in ports) best = Math.Max(best, Score(port, dragType));
            }
            else if (obj is IGH_Param param)
            {
                best = Score(param, dragType);
            }
            else return -1;   // scribbles, groups, anything with nothing to connect

            return best > 0 ? TypeCompat.Band(best) : -1;
        }

        int Score(IGH_Param port, string dragType)
        {
            if (port == null) return 0;
            var portType = TypeCompat.ShortName(SafeType(port));
            return _fromInput
                ? TypeCompat.Score(portType, dragType)   // it produces -> our input takes
                : TypeCompat.Score(dragType, portType);  // our output -> it takes
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
                var width = 2f / Math.Max(0.1f, viewport.Zoom);
                var smoothing = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // One pen per band rather than one per object: a busy canvas can put a
                // few hundred outlines on screen for every frame of the drag.
                var pens = new Pen[Ink.Length];
                for (int band = 0; band < pens.Length; band++)
                    pens[band] = new Pen(Ink[band], width);

                try
                {
                    foreach (var target in _targets)
                    {
                        var bounds = target.Attributes.Bounds;
                        if (bounds.IsEmpty) continue;
                        if (!viewport.IsVisible(ref bounds, 20f)) continue;

                        bounds.Inflate(width + 1f, width + 1f);
                        using var path = Outline(bounds, 3f + width);
                        g.DrawPath(pens[target.Band], path);
                    }
                }
                finally
                {
                    foreach (var pen in pens) pen?.Dispose();
                    g.SmoothingMode = smoothing;
                }
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
