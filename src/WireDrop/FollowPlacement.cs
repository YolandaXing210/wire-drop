using System;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

namespace WireDrop
{
    /// <summary>
    /// After the panel places something it stays on the cursor until you click, so the
    /// wire decides where it lands instead of the point you happened to let go at — the
    /// way a node behaves in Blender's shader editor.
    ///
    /// What follows the cursor is the real object, already in the document and already
    /// wired, not a drawn ghost. Moving an object only expires its layout, so nothing is
    /// recomputed between the drop and the click; the wire follows because Grasshopper
    /// draws it from the grips every frame. It also means there is nothing to commit and
    /// nothing to clean up: a click stops the following, and one Ctrl+Z still takes back
    /// the object and the wire together, exactly as it did when placement was immediate.
    /// </summary>
    internal sealed class FollowPlacement
    {
        /// <summary>At most one at a time; a second placement takes over from the first.</summary>
        static FollowPlacement _active;

        readonly GH_Canvas _canvas;
        readonly IGH_DocumentObject _obj;
        readonly IGH_Param _grip;
        readonly bool _wantInput;
        readonly PointF _origin;
        readonly Cursor _restore;
        bool _stopped;

        FollowPlacement(GH_Canvas canvas, IGH_DocumentObject obj, IGH_Param grip,
                        bool wantInput, PointF origin)
        {
            _canvas = canvas;
            _obj = obj;
            _grip = grip;
            _wantInput = wantInput;
            _origin = origin;
            _restore = canvas.Cursor;
        }

        public static void Begin(GH_Canvas canvas, IGH_DocumentObject obj, IGH_Param grip,
                                 bool wantInput, PointF origin)
        {
            Log.Guard("follow", () =>
            {
                if (canvas == null || obj?.Attributes == null) return;
                if (!Settings.FollowCursor) return;

                _active?.Stop();
                _active = new FollowPlacement(canvas, obj, grip, wantInput, origin);
                _active.Start();
            });
        }

        void Start()
        {
            _canvas.MouseMove += OnMouseMove;
            _canvas.MouseDown += OnMouseDown;
            _canvas.KeyDown += OnKeyDown;
            _canvas.Cursor = Cursors.Hand;

            // Escape has to reach the canvas, and the panel had the focus a moment ago.
            _canvas.Focus();

            // Jump under the cursor straight away rather than waiting for the first move:
            // the panel may have been dismissed with the pointer well away from the drop
            // point. If the pointer is not over the canvas at all, stay put and wait.
            var control = _canvas.PointToClient(Cursor.Position);
            if (_canvas.ClientRectangle.Contains(control)) MoveTo(Unproject(control));
            _canvas.Invalidate();
        }

        void Stop()
        {
            if (_stopped) return;
            _stopped = true;

            _canvas.MouseMove -= OnMouseMove;
            _canvas.MouseDown -= OnMouseDown;
            _canvas.KeyDown -= OnKeyDown;
            _canvas.Cursor = _restore;
            if (ReferenceEquals(_active, this)) _active = null;

            _canvas.Invalidate();
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            Log.Guard("follow-move", () =>
            {
                // The document can close, or the canvas be handed another one, while an
                // object is still on the cursor.
                if (_canvas.Document == null || _obj.Attributes == null) { Stop(); return; }

                MoveTo(Unproject(e.Location));
                _canvas.Invalidate();
            });
        }

        /// <summary>
        /// Any click puts it down. Grasshopper's own handlers ran first and will read this
        /// as a click on the object, which selects it — the right thing to be left with.
        /// </summary>
        void OnMouseDown(object sender, MouseEventArgs e) => Stop();

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;

            // Escape puts it back where the wire was dropped, which is where it would have
            // landed before any of this. It is never left half-placed.
            Log.Guard("follow-cancel", () => MoveTo(_origin));
            Stop();
        }

        PointF Unproject(Point control) =>
            _canvas.Viewport.UnprojectPoint(new PointF(control.X, control.Y));

        void MoveTo(PointF point)
        {
            if (_grip != null)
            {
                Placement.AlignGrip(_obj, _grip, _wantInput, point);
                return;
            }

            // A scribble has no grip to hang on the cursor, so it hangs by its pivot.
            _obj.Attributes.Pivot = point;
            _obj.Attributes.ExpireLayout();
            _obj.Attributes.PerformLayout();
        }
    }
}
