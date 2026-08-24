using WireDrop.UI;
using Xunit;

public class LayoutMetricsTests
{
    // Grasshopper's Standard font is around 12-13px tall by default; a user who raises
    // the font size, or a high-DPI display, pushes these numbers up.
    static LayoutMetrics Default() => LayoutMetrics.From(13, 11);
    static LayoutMetrics Large() => LayoutMetrics.From(26, 22);

    [Fact]
    public void RowsGrowWithTheFont()
    {
        Assert.True(Large().RowH > Default().RowH);
        Assert.True(Large().ListH > Default().ListH);
        Assert.True(Large().Pad > Default().Pad);
    }

    [Fact]
    public void EveryRowHasRoomForItsText()
    {
        foreach (var h in new[] { 9, 11, 13, 16, 20, 26, 34 })
        {
            var m = LayoutMetrics.From(h, h - 2);
            Assert.True(m.RowH > h, $"row {m.RowH} must exceed line height {h}");
            Assert.True(m.HeadH >= h - 2);
            Assert.True(m.ChipH >= h - 2);
        }
    }

    [Fact]
    public void TinyFontsStillGetLegibleRows()
    {
        var m = LayoutMetrics.From(1, 1);
        Assert.True(m.RowH >= 20);
        Assert.True(m.ChipH >= 15);
        Assert.True(m.FootH >= 19);
        Assert.True(m.Pad >= 6);
    }

    [Fact]
    public void ListIsAWholeNumberOfRows()
    {
        var m = Default();
        Assert.Equal(0, m.ListH % m.RowH);
        Assert.Equal(LayoutMetrics.VisibleRows, m.ListH / m.RowH);
    }

    [Fact]
    public void IconNeverOutgrowsItsRow()
    {
        foreach (var h in new[] { 9, 13, 20, 34 })
        {
            var m = LayoutMetrics.From(h, h - 2);
            Assert.True(m.IconSize <= m.RowH);
            Assert.InRange(m.IconSize, 16, 24);
        }
    }

    [Fact]
    public void PageJumpMovesAFullScreen()
    {
        var m = Default();
        Assert.Equal(LayoutMetrics.VisibleRows, m.ListH / m.RowH);
    }
}
