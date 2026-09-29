namespace WireDrop.UI
{
    internal enum PanelAction
    {
        None,
        MoveUp,
        MoveDown,
        PageUp,
        PageDown,
        PreviousCategory,
        NextCategory,
        Accept,
        Cancel,
        ToggleScope,
    }

    /// <summary>
    /// Keyboard mapping for the panel, kept free of Windows.Forms so it can be tested
    /// without a Rhino install. Takes the raw virtual key code from KeyEventArgs.KeyCode.
    /// </summary>
    internal static class KeyMap
    {
        public const int VkTab = 9;
        public const int VkEnter = 13;
        public const int VkEscape = 27;
        public const int VkPageUp = 33;
        public const int VkPageDown = 34;
        public const int VkLeft = 37;
        public const int VkUp = 38;
        public const int VkRight = 39;
        public const int VkDown = 40;

        public static PanelAction Resolve(int keyCode) => keyCode switch
        {
            VkUp => PanelAction.MoveUp,
            VkDown => PanelAction.MoveDown,
            VkPageUp => PanelAction.PageUp,
            VkPageDown => PanelAction.PageDown,
            VkLeft => PanelAction.PreviousCategory,
            VkRight => PanelAction.NextCategory,
            VkEnter => PanelAction.Accept,
            VkEscape => PanelAction.Cancel,
            VkTab => PanelAction.ToggleScope,
            _ => PanelAction.None,
        };

        /// <summary>
        /// True for keys the search field must not swallow. A TextBox would otherwise
        /// consume the arrows for caret movement and Tab for focus navigation, and on
        /// macOS there is no ProcessCmdKey pre-pass to intercept them first.
        /// </summary>
        public static bool IsPanelKey(int keyCode) => Resolve(keyCode) != PanelAction.None;
    }
}
