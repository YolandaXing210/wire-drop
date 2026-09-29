using WireDrop.UI;
using Xunit;

public class ScrollBarTests
{
    // A list of 600 rows of 24px viewed through a 14-row window.
    const int Total = 600 * 24, Viewport = 14 * 24, Track = 14 * 24;

    [Fact]
    public void NoScrollbarWhenEverythingFits()
    {
        Assert.False(ScrollBar.Needed(100, 200));
        Assert.Equal(0, ScrollBar.MaxScroll(100, 200));
    }

    [Fact]
    public void ThumbShrinksAsTheListGrows()
    {
        var shortList = ScrollBar.ThumbHeight(Viewport * 2, Viewport, Track);
        var longList = ScrollBar.ThumbHeight(Viewport * 20, Viewport, Track);
        Assert.True(shortList > longList);
    }

    [Fact]
    public void ThumbNeverDisappearsOnAHugeList()
    {
        var h = ScrollBar.ThumbHeight(1_000_000, Viewport, Track);
        Assert.True(h >= System.Math.Min(ScrollBar.MinThumb, Track));
    }

    [Fact]
    public void ThumbNeverOutgrowsItsTrack() =>
        Assert.True(ScrollBar.ThumbHeight(Viewport + 1, Viewport, Track) <= Track);

    [Fact]
    public void TopOfListPutsThumbAtTopOfTrack() =>
        Assert.Equal(0, ScrollBar.ThumbTop(Total, Viewport, Track, 0));

    [Fact]
    public void BottomOfListPutsThumbAtBottomOfTrack()
    {
        var max = ScrollBar.MaxScroll(Total, Viewport);
        var top = ScrollBar.ThumbTop(Total, Viewport, Track, max);
        Assert.Equal(ScrollBar.Travel(Total, Viewport, Track), top);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(240)]
    [InlineData(3000)]
    [InlineData(13000)]
    public void DraggingTheThumbRoundTripsToTheSameScroll(int scroll)
    {
        var top = ScrollBar.ThumbTop(Total, Viewport, Track, scroll);
        var back = ScrollBar.ScrollFromThumbTop(Total, Viewport, Track, top);
        // Rounding to whole pixels costs at most one step of the scroll-per-pixel ratio.
        var tolerance = ScrollBar.MaxScroll(Total, Viewport) / ScrollBar.Travel(Total, Viewport, Track) + 1;
        Assert.InRange(back, scroll - tolerance, scroll + tolerance);
    }

    [Fact]
    public void DraggingPastTheEndsClampsRatherThanOverscrolling()
    {
        var max = ScrollBar.MaxScroll(Total, Viewport);
        Assert.Equal(0, ScrollBar.ScrollFromThumbTop(Total, Viewport, Track, -500));
        Assert.Equal(max, ScrollBar.ScrollFromThumbTop(Total, Viewport, Track, 99999));
    }

    [Fact]
    public void ScrollIsMonotonicInThumbPosition()
    {
        var previous = -1;
        for (var top = 0; top <= ScrollBar.Travel(Total, Viewport, Track); top += 7)
        {
            var scroll = ScrollBar.ScrollFromThumbTop(Total, Viewport, Track, top);
            Assert.True(scroll >= previous, "dragging down must never scroll up");
            previous = scroll;
        }
    }

    [Fact]
    public void AListThatFitsPinsTheThumbAtZero()
    {
        Assert.Equal(0, ScrollBar.ThumbTop(100, 200, Track, 50));
        Assert.Equal(0, ScrollBar.ScrollFromThumbTop(100, 200, Track, 80));
    }
}
