using System;
using Grasshopper;

namespace WireDrop
{
    internal static class Settings
    {
        const string EnabledKey = "WireDrop.Enabled";
        const string FollowKey = "WireDrop.FollowCursor";
        const string HighlightKey = "WireDrop.HighlightTargets";

        /// <summary>
        /// Whether components that could take the wire are outlined while it is dragged.
        /// </summary>
        public static bool HighlightTargets
        {
            get { try { return Instances.Settings.GetValue(HighlightKey, true); } catch { return true; } }
            set
            {
                try
                {
                    Instances.Settings.SetValue(HighlightKey, value);
                    Instances.Settings.WritePersistentSettings();
                }
                catch (Exception ex) { Log.Error("settings-write", ex); }
            }
        }

        /// <summary>
        /// Whether a placed object stays on the cursor until you click. Off puts it down
        /// at the point the wire was dropped, which is what it did before.
        /// </summary>
        public static bool FollowCursor
        {
            get { try { return Instances.Settings.GetValue(FollowKey, true); } catch { return true; } }
            set
            {
                try
                {
                    Instances.Settings.SetValue(FollowKey, value);
                    Instances.Settings.WritePersistentSettings();
                }
                catch (Exception ex) { Log.Error("settings-write", ex); }
            }
        }

        public static bool Enabled
        {
            get { try { return Instances.Settings.GetValue(EnabledKey, true); } catch { return true; } }
            set
            {
                try
                {
                    Instances.Settings.SetValue(EnabledKey, value);
                    Instances.Settings.WritePersistentSettings();
                }
                catch (Exception ex) { Log.Error("settings-write", ex); }
            }
        }
    }
}
