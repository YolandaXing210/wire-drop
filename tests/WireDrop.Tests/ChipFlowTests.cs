using System.Linq;
using WireDrop.UI;
using Xunit;

public class ChipFlowTests
{
    const int ChipH = 18, Gap = 4, PadX = 8, PadY = 5;

    static System.Drawing.Rectangle[] Arrange(int[] widths, int available = 380) =>
        ChipFlow.Arrange(widths, available, ChipH, Gap, PadX, PadY);

    [Fact]
    public void ChipsThatFitStayOnOneRow()
    {
        var r = Arrange(new[] { 60, 60, 60 });
        Assert.All(r, x => Assert.Equal(PadY, x.Y));
        Assert.Equal(3, r.Length);
    }

    [Fact]
    public void OverflowWrapsInsteadOfBeingDropped()
    {
        // Twelve wide chips cannot fit one row; every one must still be placed.
        var widths = Enumerable.Repeat(90, 12).ToArray();
        var r = Arrange(widths);
        Assert.Equal(12, r.Length);
        Assert.True(r.Select(x => x.Y).Distinct().Count() > 1, "expected more than one row");
    }

    [Fact]
    public void NothingSpillsPastTheRightEdge()
    {
        var r = Arrange(Enumerable.Repeat(90, 12).ToArray(), available: 380);
        Assert.All(r, x => Assert.True(x.Right <= 380 - PadX, $"chip ends at {x.Right}"));
    }

    [Fact]
    public void RowsAreEvenlySpaced()
    {
        var r = Arrange(Enumerable.Repeat(120, 9).ToArray());
        var ys = r.Select(x => x.Y).Distinct().OrderBy(y => y).ToArray();
        for (int i = 1; i < ys.Length; i++)
            Assert.Equal(ChipH + Gap, ys[i] - ys[i - 1]);
    }

    [Fact]
    public void HeightCoversEveryRow()
    {
        var r = Arrange(Enumerable.Repeat(90, 12).ToArray());
        var h = ChipFlow.Height(r, ChipH, PadY);
        Assert.All(r, x => Assert.True(x.Bottom <= h));
    }

    [Fact]
    public void AChipWiderThanThePanelIsClampedNotLost()
    {
        var r = Arrange(new[] { 900 }, available: 380);
        Assert.Single(r);
        Assert.True(r[0].Right <= 380 - PadX);
    }

    [Fact]
    public void EmptyInputHasNoChips() =>
        Assert.Empty(ChipFlow.Arrange(new int[0], 380, ChipH, Gap, PadX, PadY));

    [Fact]
    public void MoreChipsNeverMeansLessHeight()
    {
        var few = ChipFlow.Height(Arrange(new[] { 60, 60 }), ChipH, PadY);
        var many = ChipFlow.Height(Arrange(Enumerable.Repeat(60, 30).ToArray()), ChipH, PadY);
        Assert.True(many > few);
    }
}
