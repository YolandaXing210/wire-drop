using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace WireDrop.Ranking
{
    /// <summary>
    /// What is actually on the wire, when the declared type will not say.
    ///
    /// The declared type is a promise about what a port carries; the value is the fact.
    /// A panel declares text and holds 3,4,5; a generic parameter declares nothing at all.
    /// So the first value on the port is taken and offered to Grasshopper's own casting —
    /// <c>IGH_Goo.CastFrom</c>, the same call a wire makes at solve time, which gets
    /// third-party types right for free. Every port is asked, not only the vague ones:
    /// the answer costs one call per goo type and it is the fact rather than the promise.
    ///
    /// The answer decides the port both ways. A cast that works promotes it; a cast that
    /// fails removes it, because connecting text that reads "hello world" to a point input
    /// is not a weaker option, it is an error waiting to be made. A port that takes
    /// anything is left exactly as the type table had it: nothing is converted on the way
    /// in, so nothing can fail there. Tab still widens the panel to the whole library, so
    /// nothing is permanently out of reach.
    /// </summary>
    internal sealed class ValueCast
    {
        readonly IGH_Goo _value;
        readonly CastBudget _budget = new CastBudget();
        /// <summary>null where the question cannot be put — see <see cref="Casts"/>.</summary>
        readonly Dictionary<Type, bool?> _casts = new Dictionary<Type, bool?>();

        ValueCast(IGH_Goo value) { _value = value; }

        /// <summary>The value on the dragged port, or null when reading it would tell us nothing.</summary>
        public static ValueCast For(IGH_Param source, bool fromInput)
        {
            // This is the outward direction only: our own value, offered to their ports.
            // Dragging from an input turns the question around, which is ValueProbe's.
            if (fromInput) return null;

            ValueCast result = null;
            Log.Guard("value-cast", () =>
            {
                if (source == null || !Settings.ReadValues) return;

                // Grasshopper pumps messages during a long solution, so a mouse handler
                // can land in the middle of one, with data half written.
                var doc = source.OnPingDocument();
                if (doc == null || doc.SolutionDepth > 0) return;

                var first = FirstValue(source);
                if (first == null) return;

                result = new ValueCast(first);
            });
            return result;
        }

        /// <summary>
        /// The score a port deserves once the value is taken into account: raised when the
        /// value goes in, zero — dropped — when it will not, and untouched when there is
        /// no answer to be had.
        /// </summary>
        public int Apply(int score, Type gooType)
        {
            if (!WorthAsking(_value, score)) return score;

            var casts = Casts(gooType);
            if (casts == null) return score;
            return casts.Value ? Math.Max(score, TypeCompat.ValueFloor) : 0;
        }

        /// <summary>
        /// Asked once per goo type and remembered: a drag spans thousands of ports but only
        /// a hundred or so distinct types between them.
        ///
        /// Null means the question could not be put, and the port is then left to the type
        /// table. That covers the two cases where a false would be a lie: a type that
        /// cannot be built to ask — a generic port declares <c>IGH_Goo</c> itself, and
        /// takes any value without converting it — and a third-party goo whose CastFrom
        /// throws, which says nothing about the value.
        /// </summary>
        bool? Casts(Type gooType)
        {
            if (gooType == null) return null;
            if (_casts.TryGetValue(gooType, out var known)) return known;
            if (_budget.Exhausted) return null;

            bool? casts = null;
            try
            {
                if (!gooType.IsAbstract && !gooType.IsInterface &&
                    typeof(IGH_Goo).IsAssignableFrom(gooType) &&
                    gooType.GetConstructor(Type.EmptyTypes) != null)
                {
                    var probe = Activator.CreateInstance(gooType) as IGH_Goo;
                    if (probe != null) casts = _budget.Charge(() => probe.CastFrom(_value));
                }
            }
            catch { casts = null; }

            _casts[gooType] = casts;
            return casts;
        }

        internal static Type SafeType(IGH_Param param) { try { return param?.Type; } catch { return null; } }

        /// <summary>
        /// The first item on a port, or null when it is carrying nothing. The first only:
        /// a tree on a port may hold a hundred thousand, and one is enough to tell what
        /// kind of thing is travelling.
        /// </summary>
        /// <summary>
        /// Whether a value is worth putting to <c>CastFrom</c> at all.
        ///
        /// Only text takes the slow road. When a string will not parse, Grasshopper's last
        /// guess is that it names an object in the Rhino document and goes to look: measured
        /// on a 40,000 object model, <c>GH_Point.CastFrom("hello world")</c> costs 17 ms
        /// against 0.18 ms for text that does parse. Multiply by the geometry types and a
        /// drag off a panel would stall for half a second.
        ///
        /// So text is asked only when it could plausibly be data: it has a digit in it and
        /// no letters beyond an exponent's e. Prose is not put to the question.
        ///
        /// Declining to ask is not answering no — the port keeps whatever the type table
        /// gave it. For the case this exists to catch that lands in the same place anyway:
        /// text to a point is a conversion, and conversions are not outlined.
        /// </summary>
        /// <summary>
        /// Whether a port is worth the cost of the question, given what the type table
        /// already said about it. Measured on a 40,000 object Rhino model:
        ///
        /// <code>
        /// text -> a type the table rates possible      0.01 - 0.6 ms
        /// text -> a type the table rates impossible   16 - 28 ms
        /// any non-text value, any type                 0.02 ms
        /// </code>
        ///
        /// The expensive column is one thing: when a string will not become the type asked
        /// for, Grasshopper's last guess is that it names an object in the Rhino document,
        /// and it goes and looks. Those are exactly the ports the table already scores at
        /// zero, so the rule that avoids the cost is also the honest one — the value is
        /// asked to settle doubt, not to overturn certainty or to invent a route the table
        /// has never heard of. Only from text, where the question is dear; any other kind
        /// of value is cheap enough to ask about anything.
        /// </summary>
        internal static bool WorthAsking(IGH_Goo value, int score)
        {
            // Same declared type: a Number port carries numbers, and the value cannot
            // disagree with itself. The commonest case there is, and free to skip.
            if (score >= TypeCompat.Exact) return false;

            // Already direct: the table is confident and asking could only contradict it.
            if (TypeCompat.Band(score) == 2) return false;

            // Doubtful — this is what the value is for.
            if (score > 0) return WorthAsking(value);

            // No route known at all. From text that is the 16-28 ms question; from
            // anything else it is 20 microseconds, so it is still worth asking.
            return !(value is GH_String);
        }

        internal static bool WorthAsking(IGH_Goo value)
        {
            if (!(value is GH_String text)) return true;

            var s = text.Value;
            if (string.IsNullOrEmpty(s)) return false;

            var digits = false;
            foreach (var c in s)
            {
                if (c >= '0' && c <= '9') { digits = true; continue; }
                if (char.IsLetter(c) && c != 'e' && c != 'E') return false;
            }
            return digits;
        }

        internal static IGH_Goo FirstValue(IGH_Param param)
        {
            try
            {
                var data = param?.VolatileData;
                if (data == null || data.IsEmpty) return null;
                foreach (IGH_Goo goo in data.AllData(true)) return goo;
            }
            catch { }
            return null;
        }


    }
}
