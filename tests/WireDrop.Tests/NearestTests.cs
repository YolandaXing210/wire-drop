using System.Collections.Generic;
using System.Drawing;
using WireDrop.UI;
using Xunit;

public class NearestTests
{
    static List<int> Pick(PointF cursor, int max, params RectangleF[] bounds)
    {
        var chosen = new List<int>();
        Nearest.Pick(bounds, cursor, max, chosen);
        return chosen;
    }

    static RectangleF Box(float x, float y) => new RectangleF(x, y, 10, 10);

    [Fact]
    public void KeepsOnlyAsManyAsAsked()
    {
        var boxes = new RectangleF[20];
        for (int i = 0; i < boxes.Length; i++) boxes[i] = Box(i * 100, 0);

        Assert.Equal(10, Pick(new PointF(0, 0), 10, boxes).Count);
    }

    [Fact]
    public void TakesTheClosestAndOrdersThemNearestFirst()
    {
        var chosen = Pick(new PointF(0, 0), 2, Box(300, 0), Box(100, 0), Box(200, 0));

        Assert.Equal(new[] { 1, 2 }, chosen);
    }

    [Fact]
    public void DistanceIsToTheNearestEdgeNotTheCentre()
    {
        // A wide component whose near edge is beside the cursor is near, however far away
        // its middle is: what gets outlined is the body, so the body is what counts.
        var wide = new RectangleF(10, 0, 1000, 10);
        var small = Box(100, 0);

        Assert.Equal(new[] { 0 }, Pick(new PointF(0, 0), 1, wide, small));
    }

    [Fact]
    public void ACursorInsideABoxIsAsNearAsItGets()
    {
        Assert.Equal(new[] { 1 }, Pick(new PointF(5, 5), 1, Box(100, 0), Box(0, 0)));
    }

    [Fact]
    public void TiesGoToTheEarlierBox()
    {
        Assert.Equal(new[] { 0 }, Pick(new PointF(0, 0), 1, Box(100, 0), Box(0, 100)));
    }

    [Fact]
    public void FewerCandidatesThanAskedForReturnsThemAll()
    {
        Assert.Equal(2, Pick(new PointF(0, 0), 10, Box(0, 0), Box(50, 0)).Count);
    }

    [Fact]
    public void NothingToChooseFromChoosesNothing()
    {
        Assert.Empty(Pick(new PointF(0, 0), 10));

        var chosen = new List<int>();
        Nearest.Pick(null, new PointF(0, 0), 10, chosen);
        Assert.Empty(chosen);
    }

    [Fact]
    public void AskingForNoneChoosesNone()
    {
        Assert.Empty(Pick(new PointF(0, 0), 0, Box(0, 0)));
    }

    [Fact]
    public void TheListIsClearedBeforeItIsFilled()
    {
        // The caller reuses one list every frame. Leftovers would outline components that
        // are no longer near anything.
        var chosen = new List<int> { 7, 8, 9 };

        Nearest.Pick(new[] { Box(0, 0) }, new PointF(0, 0), 10, chosen);

        Assert.Equal(new[] { 0 }, chosen);
    }
}
