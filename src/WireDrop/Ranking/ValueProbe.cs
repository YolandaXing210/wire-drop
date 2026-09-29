using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace WireDrop.Ranking
{
    /// <summary>
    /// The other direction. Dragging from an input, what is wanted is a producer, and the
    /// candidates are objects already on the canvas — which means their outputs are already
    /// carrying something. So the question turns around: not "does my value go into that
    /// port" but "does what that port is holding come into mine".
    ///
    /// The probe is the type the dragged input takes, and every candidate is asked with its
    /// own first value. A panel reading 3,4,5 answers a Vector input; one reading
    /// "hello world" does not, and is dropped rather than offered as a conversion that
    /// could only fail.
    ///
    /// This has no counterpart in the panel's list: those candidates come from the
    /// installed library rather than the document, and a component that is not on the
    /// canvas is not holding anything to read.
    /// </summary>
    internal sealed class ValueProbe
    {
        readonly Type _wanted;
        readonly CastBudget _budget = new CastBudget();

        ValueProbe(Type wanted) { _wanted = wanted; }

        /// <summary>
        /// Null when the dragged input cannot be asked — which includes a generic input,
        /// since it declares <c>IGH_Goo</c> itself and takes anything anyway.
        /// </summary>
        public static ValueProbe For(IGH_Param destination, bool fromInput)
        {
            if (!fromInput) return null;

            ValueProbe result = null;
            Log.Guard("value-probe", () =>
            {
                if (destination == null || !Settings.ReadValues) return;

                // Grasshopper pumps messages during a long solution, so a mouse handler can
                // land in the middle of one, with data half written.
                var doc = destination.OnPingDocument();
                if (doc == null || doc.SolutionDepth > 0) return;

                var wanted = ValueCast.SafeType(destination);
                if (wanted == null || wanted.IsAbstract || wanted.IsInterface) return;
                if (!typeof(IGH_Goo).IsAssignableFrom(wanted)) return;
                if (wanted.GetConstructor(Type.EmptyTypes) == null) return;

                result = new ValueProbe(wanted);
            });
            return result;
        }

        /// <summary>
        /// The score a candidate output deserves once what it is carrying is taken into
        /// account: raised when that value comes in, zero when it will not, untouched when
        /// there is nothing to go on.
        ///
        /// Every candidate is asked, whatever it declares: reading the first item off a
        /// port is free — a lazy enumerator, no allocation, the same cost on a tree of ten
        /// items as on a tree of a million.
        /// </summary>
        public int Apply(int score, IGH_Param candidate)
        {
            if (candidate == null) return score;

            if (_budget.Exhausted) return score;

            var value = ValueCast.FirstValue(candidate);
            if (value == null || !ValueCast.WorthAsking(value, score)) return score;

            try
            {
                // A fresh goo each time: CastFrom writes into the one it is called on, and
                // no candidate should be judged on what the last one left behind.
                var probe = Activator.CreateInstance(_wanted) as IGH_Goo;
                if (probe == null) return score;
                return _budget.Charge(() => probe.CastFrom(value))
                    ? Math.Max(score, TypeCompat.ValueFloor)
                    : 0;
            }
            catch
            {
                // A goo that throws has said nothing about the value.
                return score;
            }
        }
    }
}
