using System;
using System.Drawing;
using System.Reflection;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;

namespace WireDrop.Interop
{
    /// <summary>
    /// GH_WireInteraction keeps the live drag state in private fields. They are the only
    /// honest source for "what was dragged" and "did it land on something", so we bind
    /// them once at load. If a future Grasshopper renames any of them, <see cref="Available"/>
    /// goes false and WireDrop stays dormant instead of breaking the canvas.
    /// </summary>
    internal static class WireInteractionFields
    {
        static readonly FieldInfo Source;
        static readonly FieldInfo Target;
        static readonly FieldInfo FromInput;
        static readonly FieldInfo Point;

        public static bool Available { get; }

        static WireInteractionFields()
        {
            try
            {
                const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var t = typeof(GH_WireInteraction);
                Source = t.GetField("m_source", Flags);
                Target = t.GetField("m_target", Flags);
                FromInput = t.GetField("m_dragfrominput", Flags);
                Point = t.GetField("m_point", Flags);

                Available = Source != null && Target != null && FromInput != null && Point != null
                            && typeof(IGH_Param).IsAssignableFrom(Source.FieldType)
                            && typeof(IGH_Param).IsAssignableFrom(Target.FieldType)
                            && FromInput.FieldType == typeof(bool)
                            && Point.FieldType == typeof(PointF);
            }
            catch (Exception ex)
            {
                Available = false;
                Log.Error("wire-interaction-binding", ex);
            }

            if (!Available)
                Log.Once("wire-interaction-unsupported",
                    "this Grasshopper build exposes the wire drag differently than expected; WireDrop is inactive.");
        }

        public static bool TryRead(GH_WireInteraction interaction, out WireDragState state)
        {
            state = default;
            if (!Available || interaction == null) return false;
            try
            {
                state = new WireDragState(
                    Source.GetValue(interaction) as IGH_Param,
                    Target.GetValue(interaction) as IGH_Param,
                    (bool)FromInput.GetValue(interaction),
                    (PointF)Point.GetValue(interaction));
                return state.Source != null;
            }
            catch (Exception ex)
            {
                Log.Error("wire-interaction-read", ex);
                return false;
            }
        }
    }

    internal readonly struct WireDragState
    {
        /// <summary>The port the drag started from.</summary>
        public readonly IGH_Param Source;
        /// <summary>The port under the cursor, or null when the wire is over empty canvas.</summary>
        public readonly IGH_Param Target;
        /// <summary>True when the drag started at an input, so we need a component that produces.</summary>
        public readonly bool FromInput;
        /// <summary>Cursor position in canvas coordinates.</summary>
        public readonly PointF Point;

        public WireDragState(IGH_Param source, IGH_Param target, bool fromInput, PointF point)
        {
            Source = source; Target = target; FromInput = fromInput; Point = point;
        }
    }
}
