using System;
using System.Windows.Forms;
using Grasshopper;
using Rhino;

namespace WireDrop.UI
{
    /// <summary>
    /// A WireDrop menu in Grasshopper's own menu bar, so the settings can be switched
    /// without editing Grasshopper's settings file by hand.
    ///
    /// The editor window does not exist yet when the plugin loads, so the menu waits on
    /// Rhino's idle ticks until it does. Each item reads its setting when the menu opens
    /// rather than once, so a change made elsewhere is never shown stale.
    /// </summary>
    internal static class SettingsMenu
    {
        static bool _added;

        public static void AddWhenReady()
        {
            RhinoApp.Idle += OnIdle;
        }

        static void OnIdle(object sender, EventArgs e)
        {
            var editor = Instances.DocumentEditor;
            if (editor?.MainMenuStrip == null) return;

            RhinoApp.Idle -= OnIdle;
            if (_added) return;
            _added = true;

            Log.Guard("settings-menu", () => editor.MainMenuStrip.Items.Add(Build()));
        }

        static ToolStripMenuItem Build()
        {
            var menu = new ToolStripMenuItem("WireDrop");

            var enabled = Toggle(menu, "Enabled",
                                 "Open the panel when a wire is dropped on empty canvas.",
                                 () => Settings.Enabled, v => Settings.Enabled = v);
            menu.DropDownItems.Add(new ToolStripSeparator());
            Toggle(menu, "Click to Place",
                   "Keep a placed component on the cursor until you click, instead of " +
                   "putting it down where the wire was dropped.",
                   () => Settings.FollowCursor, v => Settings.FollowCursor = v);
            Toggle(menu, "Splice on Wire",
                   "Right-click a wire to open the panel on components that fit between its ends.",
                   () => Settings.SpliceOnWire, v => Settings.SpliceOnWire = v);
            Toggle(menu, "Highlight Targets",
                   "Outline nearby components that could take the wire while it is dragged.",
                   () => Settings.HighlightTargets, v => Settings.HighlightTargets = v);
            Toggle(menu, "Read Values",
                   "Look at the value on a generic or text port to sharpen the ranking.",
                   () => Settings.ReadValues, v => Settings.ReadValues = v);

            // The rest only mean anything while WireDrop is on.
            menu.DropDownOpening += (s, e) =>
            {
                foreach (ToolStripItem item in menu.DropDownItems)
                    if (item != enabled && item is ToolStripMenuItem) item.Enabled = Settings.Enabled;
            };

            return menu;
        }

        static ToolStripMenuItem Toggle(ToolStripMenuItem menu, string text, string tip,
                                        Func<bool> get, Action<bool> set)
        {
            var item = new ToolStripMenuItem(text) { ToolTipText = tip };
            item.Click += (s, e) => Log.Guard("settings-toggle", () => set(!get()));
            menu.DropDownOpening += (s, e) => item.Checked = get();
            menu.DropDownItems.Add(item);
            return item;
        }
    }
}
