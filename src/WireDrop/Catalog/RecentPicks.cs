using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;

namespace WireDrop.Catalog
{
    /// <summary>
    /// Remembers what you picked, keyed by the type you dragged, so the same wire
    /// offers the same answer next time. Stored in Grasshopper's own settings.
    /// </summary>
    internal static class RecentPicks
    {
        const int Limit = 12;
        const string Prefix = "WireDrop.Recent.";

        static string Key(string dragType, bool fromInput) =>
            Prefix + (fromInput ? "in." : "out.") + (dragType ?? "Generic");

        public static List<Guid> For(string dragType, bool fromInput)
        {
            var result = new List<Guid>();
            try
            {
                var raw = Instances.Settings.GetValue(Key(dragType, fromInput), string.Empty);
                if (string.IsNullOrEmpty(raw)) return result;
                foreach (var part in raw.Split(';'))
                    if (Guid.TryParse(part, out var g)) result.Add(g);
            }
            catch (Exception ex) { Log.Error("recent-read", ex); }
            return result;
        }

        public static void Record(string dragType, bool fromInput, Guid id)
        {
            try
            {
                var list = For(dragType, fromInput);
                list.RemoveAll(g => g == id);
                list.Insert(0, id);
                if (list.Count > Limit) list.RemoveRange(Limit, list.Count - Limit);
                Instances.Settings.SetValue(Key(dragType, fromInput),
                    string.Join(";", list.Select(g => g.ToString())));
                Instances.Settings.WritePersistentSettings();
            }
            catch (Exception ex) { Log.Error("recent-write", ex); }
        }
    }
}
