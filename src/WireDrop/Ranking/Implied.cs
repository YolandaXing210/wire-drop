using System;
using System.Collections.Generic;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using WireDrop.Catalog;

namespace WireDrop.Ranking
{
    /// <summary>
    /// The row you get for typing a shortcut rather than a name: a number becomes a slider,
    /// <c>"text</c> a panel, <c>~text</c> a scribble, <c>3,4</c> a point. Grasshopper's own
    /// canvas search does the same through
    /// <c>GH_PopupSearchDialog.CreateImpliedObject</c>, and the parsing here is
    /// Grasshopper's throughout — <c>ParseExpression</c>, <c>HarvestRange</c>,
    /// <c>SetInitCode</c>, <c>ToPoint3d</c> — so what you type means what it means there.
    /// The lone symbols (+, -, *, /) are not here: they name an ordinary component, so
    /// <see cref="HitBuilder"/> promotes it out of the catalog with its real ports instead.
    /// </summary>
    internal static class Implied
    {
        /// <summary>Said on the scribble row, since nothing about it looks unwired.</summary>
        const string Unwired = "Placed on the canvas — nothing is wired to it.";

        public static Hit TryBuild(string dragType, bool fromInput, string query,
                                   bool showAll, string category)
        {
            var text = (query ?? string.Empty).Trim();
            if (text.Length == 0) return null;

            Hit hit = null;
            Log.Guard("implied", () =>
            {
                // A panel takes anything and hands back text, so it suits either direction.
                if (ShortcutMap.TryPanel(text, out var panelText))
                {
                    hit = Row(PanelId, "Panel", panelText.Length == 0 ? "(empty)" : panelText,
                              category, TypeCompat.Score("Text", dragType),
                              () => { var panel = new GH_Panel(); panel.SetUserText(panelText); return panel; },
                              connects: true);
                    return;
                }

                // A scribble is canvas annotation with no ports at all: it is placed where
                // the wire was dropped and nothing is connected to it.
                if (ShortcutMap.TryScribble(text, out var note))
                {
                    hit = Row(ScribbleId, "Scribble", note, category, 0, Unwired,
                              () => { var scribble = new GH_Scribble(); scribble.Text = note; return scribble; },
                              connects: false);
                    return;
                }

                // The rest only produce a value, so they can only answer a wire off an input.
                if (!fromInput) return;

                if (SliderText.HasDigit(text) && IsSliderCode(text))
                {
                    var score = TypeCompat.Score("Number", dragType);
                    if (score <= 0 && !showAll) return;
                    hit = Row(SliderId, "Number Slider", DescribeSlider(text), category, score,
                              () => { var slider = new GH_NumberSlider(); slider.SetInitCode(text); return slider; },
                              connects: true);
                    return;
                }

                if (ShortcutMap.LooksLikePoint(text) && TryPoint(text, out var point))
                {
                    var score = TypeCompat.Score("Point", dragType);
                    if (score <= 0 && !showAll) return;
                    hit = Row(PointId, "Point", Describe(point), category, score,
                              () =>
                              {
                                  var param = new Param_Point();
                                  param.PersistentData.Append(new GH_Point(point));
                                  return param;
                              },
                              connects: true);
                }
            });
            return hit;
        }

        // ---------- Grasshopper's parsers ----------

        /// <summary>
        /// Both of the tests Grasshopper's own search makes, and nothing more. An explicit
        /// range — "0&lt;5&lt;10" — is <c>HarvestRange</c>; anything that simply evaluates
        /// to a number — "5", "0.25", "-2", "2+3" — is a numeric <c>ParseExpression</c>,
        /// which is the last thing CreateImpliedObject tries. HarvestRange alone rejects a
        /// bare 5, so testing only that one hid the whole feature.
        /// </summary>
        static bool IsSliderCode(string text)
        {
            if (GH_NumberSlider.HarvestRange(text, out _, out _, out _)) return true;
            var parsed = GH_Convert.ParseExpression(text, true);
            return parsed != null && parsed.IsNumeric;
        }

        static bool TryPoint(string text, out Point3d point)
        {
            point = Point3d.Unset;
            return GH_Convert.ToPoint3d(text, ref point, GH_Conversion.Both);
        }

        static string Describe(Point3d point) =>
            point.X.ToString("0.###") + ", " + point.Y.ToString("0.###") + ", " + point.Z.ToString("0.###");

        /// <summary>
        /// Builds the slider Grasshopper would build and reads back what it settled on, so
        /// the row shows the real range: 5 becomes 0 to 10, 0.25 becomes 0.00 to 1.00.
        /// Falls back to the typed text rather than the row disappearing — a label that
        /// cannot be built is a cosmetic failure, not a reason to withhold the slider.
        /// </summary>
        static string DescribeSlider(string initCode)
        {
            try
            {
                var probe = new GH_NumberSlider();
                probe.SetInitCode(initCode);
                var slider = probe.Slider;
                return SliderText.Describe(
                    GH_NumberSlider.FormatNumber(slider.Value, slider.Type, slider.DecimalPlaces),
                    GH_NumberSlider.FormatNumber(slider.Minimum, slider.Type, slider.DecimalPlaces),
                    GH_NumberSlider.FormatNumber(slider.Maximum, slider.Type, slider.DecimalPlaces));
            }
            catch (Exception ex)
            {
                Log.Error("slider-preview", ex);
                return initCode;
            }
        }

        // ---------- the row ----------

        static Hit Row(Func<Guid> id, string fallbackName, string label, string category,
                       int score, Func<IGH_DocumentObject> create, bool connects) =>
            Row(id, fallbackName, label, category, score, null, create, connects);

        static Hit Row(Func<Guid> id, string fallbackName, string label, string category,
                       int score, string note, Func<IGH_DocumentObject> create, bool connects)
        {
            var entry = Entry(id(), fallbackName);
            if (entry == null) return null;
            if (!string.IsNullOrEmpty(category) &&
                !string.Equals(entry.Category, category, StringComparison.Ordinal)) return null;

            return new Hit
            {
                Component = entry,
                Port = new PortSpec { Index = 0, Name = label, NickName = label, Description = note },
                Score = score,
                FirstOfGroup = true,
                Create = create,
                Connects = connects,
            };
        }

        // Read off the objects themselves rather than written down here, so a guid can
        // never drift out of step with the class it names.
        static Guid _panel, _scribble, _slider, _point;
        static Guid PanelId() => _panel != Guid.Empty ? _panel : _panel = new GH_Panel().ComponentGuid;
        static Guid ScribbleId() => _scribble != Guid.Empty ? _scribble : _scribble = new GH_Scribble().ComponentGuid;
        static Guid SliderId() => _slider != Guid.Empty ? _slider : _slider = new GH_NumberSlider().ComponentGuid;
        static Guid PointId() => _point != Guid.Empty ? _point : _point = new Param_Point().ComponentGuid;

        static readonly Dictionary<Guid, ComponentEntry> Entries = new Dictionary<Guid, ComponentEntry>();

        /// <summary>
        /// The object's own catalog entry, for its icon, name and category. A scribble is
        /// not in the catalog at all — it is neither component nor parameter — so the proxy
        /// answers for it, and a bare name answers if even that is gone. The row is never
        /// dropped for want of an icon.
        /// </summary>
        static ComponentEntry Entry(Guid id, string fallbackName)
        {
            if (id == Guid.Empty) return null;
            if (Entries.TryGetValue(id, out var known)) return known;

            foreach (var entry in ComponentCatalog.Instance.Entries)
            {
                if (entry.Id != id) continue;
                return Entries[id] = entry;
            }

            var proxy = Instances.ComponentServer.EmitObjectProxy(id);
            var desc = proxy?.Desc;
            return Entries[id] = new ComponentEntry
            {
                Id = id,
                Name = string.IsNullOrEmpty(desc?.Name) ? fallbackName : desc.Name,
                NickName = desc?.NickName,
                Category = string.IsNullOrEmpty(desc?.Category) ? "Params" : desc.Category,
                SubCategory = desc?.SubCategory,
                Description = desc?.Description,
                Icon = proxy?.Icon,
                Exposure = proxy?.Exposure ?? GH_Exposure.primary,
            };
        }
    }
}
