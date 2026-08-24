using WireDrop.UI;
using Xunit;

public class KeyMapTests
{
    [Fact]
    public void ArrowsMoveTheSelection()
    {
        Assert.Equal(PanelAction.MoveUp, KeyMap.Resolve(KeyMap.VkUp));
        Assert.Equal(PanelAction.MoveDown, KeyMap.Resolve(KeyMap.VkDown));
    }

    [Fact]
    public void EnterAccepts() =>
        Assert.Equal(PanelAction.Accept, KeyMap.Resolve(KeyMap.VkEnter));

    [Fact]
    public void EscapeCancels() =>
        Assert.Equal(PanelAction.Cancel, KeyMap.Resolve(KeyMap.VkEscape));

    [Fact]
    public void TabTogglesScope() =>
        Assert.Equal(PanelAction.ToggleScope, KeyMap.Resolve(KeyMap.VkTab));

    [Fact]
    public void LeftAndRightWalkCategories()
    {
        Assert.Equal(PanelAction.PreviousCategory, KeyMap.Resolve(KeyMap.VkLeft));
        Assert.Equal(PanelAction.NextCategory, KeyMap.Resolve(KeyMap.VkRight));
    }

    [Fact]
    public void PageKeysJump()
    {
        Assert.Equal(PanelAction.PageUp, KeyMap.Resolve(KeyMap.VkPageUp));
        Assert.Equal(PanelAction.PageDown, KeyMap.Resolve(KeyMap.VkPageDown));
    }

    [Theory]
    [InlineData(65)]  // A
    [InlineData(90)]  // Z
    [InlineData(48)]  // 0
    [InlineData(32)]  // space
    [InlineData(8)]   // backspace
    public void TypingKeysAreLeftToTheSearchField(int keyCode)
    {
        Assert.Equal(PanelAction.None, KeyMap.Resolve(keyCode));
        Assert.False(KeyMap.IsPanelKey(keyCode));
    }

    [Theory]
    [InlineData(KeyMap.VkUp)]
    [InlineData(KeyMap.VkDown)]
    [InlineData(KeyMap.VkEnter)]
    [InlineData(KeyMap.VkTab)]
    [InlineData(KeyMap.VkEscape)]
    [InlineData(KeyMap.VkLeft)]
    [InlineData(KeyMap.VkRight)]
    public void PanelKeysAreClaimedFromTheTextBox(int keyCode) =>
        Assert.True(KeyMap.IsPanelKey(keyCode));
}
