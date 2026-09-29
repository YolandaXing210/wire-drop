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
        readonly DropTargets _targets;
        bool _armed;
        WireDragState _state;

        /// <summary>
        /// How far the pointer may travel between press and release and still count as a
        /// click. Right-drag pans the canvas in Grasshopper, and a pan that happens to end
        /// over a wire must not open anything. Two rather than three because that is where
        /// Grasshopper's own interactions begin to treat the movement as a drag: at three
        /// the canvas has already jumped.
        /// </summary>
        const int DragSlop = 2;

        /// <summary>
        /// Rhino's cross-platform WinForms reports the physical Control key on macOS as
        /// Keys.MacControl; Command is what arrives as Keys.Control. Grasshopper answers
        /// Control+left there with the same canvas menu it answers a right-click with, and
        /// our validator withholds that menu over a wire — so the gesture has to reach the
        /// panel too, or on a Mac it would do nothing at all.
        /// </summary>
        const Keys MacControl = (Keys)0x80000;

        Point _rightDown;
        bool _rightArmed;

        CanvasWatcher(GH_Canvas canvas)
        {
            _canvas = canvas;
            _targets = DropTargets.Attach(canvas);
            // Registered before the handlers below, so a throw here would cost the drop as
            // well as the splice. It is a list append, but the drop is not ours to risk.
            try { canvas.AddValidator(new WireSpliceValidator(canvas)); }
            catch (Exception ex) { Log.Error("add-validator", ex); }
        }

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

        void OnMouseDown(object sender, MouseEventArgs e)
        {
            _armed = false;
            _targets.Hide();

            if (e.Button == MouseButtons.Right ||
                (e.Button == MouseButtons.Left && Control.ModifierKeys == MacControl))
            {
                _rightDown = e.Location;
                _rightArmed = true;
            }
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!(_canvas.ActiveInteraction is GH_WireInteraction interaction)) return;
            if (WireInteractionFields.TryRead(interaction, out var state))
            {
                _state = state;
                _armed = true;

                // Say where this wire could go while it is still on the cursor. The point
                // comes from Grasshopper's own drag state rather than a second reading of the
                // mouse, so the outlines follow the wire exactly.
                _targets.Show(state.Source, state.FromInput, state.Point);
            }
        }

        void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Returning here skips the drop cleanup below, which is safe: the target
                // outlines only exist while a wire is being dragged with the left button,
                // and that drag ends with a left release of its own.
                var armed = _rightArmed;
                _rightArmed = false;
                if (armed) TrySplice(e);
                return;
            }

            // The Mac's other secondary click. Only when no wire is on the cursor: a left
            // release that is carrying one belongs to the drop below, modifier or not.
            if (!_armed && e.Button == MouseButtons.Left && Control.ModifierKeys == MacControl)
            {
                var armed = _rightArmed;
                _rightArmed = false;
                if (armed) TrySplice(e);
                return;
            }

            if (!_armed) return;
            _armed = false;
            _targets.Hide();

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

        /// <summary>
        /// A right-click on a wire opens the panel on it. Grasshopper's own handlers ran
        /// first and, having asked our validator, left its canvas menu unshown.
        /// </summary>
        void TrySplice(MouseEventArgs e)
        {
            Log.Guard("splice-click", () =>
            {
                if (!Settings.Enabled || !Settings.SpliceOnWire) return;
                if (_canvas.ActiveInteraction != null) return;
                if (Math.Abs(e.X - _rightDown.X) > DragSlop) return;
                if (Math.Abs(e.Y - _rightDown.Y) > DragSlop) return;

                var point = _canvas.Viewport.UnprojectPoint(new PointF(e.X, e.Y));
                if (!WireUnder.Find(_canvas, point, out var upstream, out var downstream)) return;

                WireDropPopup.ShowForSplice(_canvas, upstream, downstream, point, e.Location);
            });
        }
    }
}
