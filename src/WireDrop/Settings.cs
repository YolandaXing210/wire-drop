using System;
using Grasshopper;

namespace WireDrop
{
    internal static class Settings
    {
        const string EnabledKey = "WireDrop.Enabled";

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
