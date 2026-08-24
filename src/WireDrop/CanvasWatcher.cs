using System;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using WireDrop.Interop;
using WireDrop.UI;

namespace WireDrop
{
    /// <summary>
    /// Watches one canvas for a wire released over empty space.
    ///
    /// Grasshopper subscribes its own mouse handlers in the canvas constructor, so ours
    /// always run after its. By the time MouseUp reaches us the interaction has been
    /// destroyed and ActiveInteraction is null — which is why the drag state is captured
    /// on every MouseMove instead, and MouseUp only reads what was captured.
    /// </summary>
    internal sealed class CanvasWatcher
    {
        readonly GH_Canvas _canvas;
        bool _armed;
        WireDragState _state;

        CanvasWatcher(GH_Canvas canvas) { _canvas = canvas; }

        public static void Attach(GH_Canvas canvas)
        {
            if (canvas == null || !WireInteractionFields.Available) return;
            var watcher = new CanvasWatcher(canvas);
            canvas.MouseDown += watcher.OnMouseDown;
            canvas.MouseMove += watcher.OnMouseMove;
            canvas.MouseUp += watcher.OnMouseUp;
            canvas.Disposed += (s, e) =>
            {
                canvas.MouseDown -= watcher.OnMouseDown;
                canvas.MouseMove -= watcher.OnMouseMove;
                canvas.MouseUp -= watcher.OnMouseUp;
            };
        }

        void OnMouseDown(object sender, MouseEventArgs e) => _armed = false;

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!(_canvas.ActiveInteraction is GH_WireInteraction interaction)) return;
            if (WireInteractionFields.TryRead(interaction, out var state))
            {
                _state = state;
                _armed = true;
            }
        }

        void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (!_armed) return;
            _armed = false;

            Log.Guard("drop", () =>
            {
                if (e.Button != MouseButtons.Left) return;
                if (!Settings.Enabled) return;

                // Grasshopper already made a connection; nothing for us to do.
                if (_state.Target != null) return;
                if (_state.Source == null) return;

                var doc = _canvas.Document;
                if (doc == null) return;

                var canvasPoint = _canvas.Viewport.UnprojectPoint(new PointF(e.X, e.Y));

                // Released over a component body rather than empty canvas.
                if (doc.FindObject(canvasPoint, 6f) != null) return;

                WireDropPopup.ShowFor(_canvas, _state.Source, _state.FromInput, canvasPoint, e.Location);
            });
        }
    }
}
