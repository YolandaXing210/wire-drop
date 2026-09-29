using WireDrop.Ranking;
using Xunit;

public class SpliceScoreTests
{
    [Theory]
    [InlineData(100, 100, 100)]
    [InlineData(100, 55, 55)]
    [InlineData(55, 100, 55)]
    public void ARowIsWorthItsWorseEnd(int inScore, int outScore, int expected) =>
        Assert.Equal(expected, SpliceScore.Combine(inScore, outScore));

    [Theory]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    [InlineData(0, 0)]
    public void AnEndThatCannotBeMadeIsWorthNothing(int inScore, int outScore) =>
        Assert.Equal(0, SpliceScore.Combine(inScore, outScore));

    [Fact]
    public void TheBandFollowsTheWeakerEnd()
    {
        // A curve arriving at a curve port is no conversion at all; a brep arriving at one
        // is. Spliced between them, the row belongs under the conversion heading.
        var direct = SpliceScore.Combine(TypeCompat.Score("Curve", "Curve"),
                                         TypeCompat.Score("Curve", "Curve"));
        var converts = SpliceScore.Combine(TypeCompat.Score("Curve", "Curve"),
                                           TypeCompat.Score("Brep", "Curve"));
        Assert.Equal(2, TypeCompat.Band(direct));
        Assert.Equal(1, TypeCompat.Band(converts));
    }

    [Fact]
    public void TheBestOutputWins()
    {
        var score = SpliceScore.BestOutput(new[] { "Number", "Point", "Curve" }, "Curve", showAll: false, out var index);
        Assert.Equal(2, index);
        Assert.Equal(TypeCompat.Exact, score);
    }

    [Fact]
    public void OutputsThatReachNothingLeaveNoIndex()
    {
        // Divide Curve gives points, tangents and parameters. None of them is a curve, so
        // it has no business in the middle of one.
        var score = SpliceScore.BestOutput(new[] { "Point", "Vector", "Number" }, "Curve", showAll: false, out var index);
        Assert.Equal(0, score);
        Assert.Equal(-1, index);
    }

    [Fact]
    public void ATieGoesToTheEarlierOutput()
    {
        var score = SpliceScore.BestOutput(new[] { "Curve", "Curve" }, "Curve", showAll: false, out var index);
        Assert.Equal(0, index);
        Assert.Equal(TypeCompat.Exact, score);
    }

    [Fact]
    public void AComponentWithNoOutputsReachesNothing()
    {
        Assert.Equal(0, SpliceScore.BestOutput(new string[0], "Curve", showAll: false, out var index));
        Assert.Equal(-1, index);
        Assert.Equal(0, SpliceScore.BestOutput(null, "Curve", showAll: false, out _));
    }

    [Fact]
    public void BothEndsHaveToReachUnlessTabAsksForEverything()
    {
        Assert.True(SpliceScore.Keeps(100, 100, showAll: false));
        Assert.False(SpliceScore.Keeps(100, 0, showAll: false));
        Assert.False(SpliceScore.Keeps(0, 100, showAll: false));
        Assert.True(SpliceScore.Keeps(100, 0, showAll: true));
    }

    [Fact]
    public void TabTakesTheFirstWayOutWhenNoneReaches()
    {
        // Tab means everything here as it does everywhere else in the panel: a drop offers
        // ports the table rejects too, and Grasshopper tries the cast when the wire is made.
        var score = SpliceScore.BestOutput(new[] { "Point", "Vector" }, "Curve", showAll: true, out var index);
        Assert.Equal(0, index);
        Assert.Equal(0, score);
    }

    [Fact]
    public void WithoutTabThereIsNoFallback()
    {
        SpliceScore.BestOutput(new[] { "Point", "Vector" }, "Curve", showAll: false, out var index);
        Assert.Equal(-1, index);
    }

    [Fact]
    public void AComponentWithNoWayOutIsUnspliceableEvenUnderTab()
    {
        SpliceScore.BestOutput(new string[0], "Curve", showAll: true, out var empty);
        Assert.Equal(-1, empty);

        SpliceScore.BestOutput(null, "Curve", showAll: true, out var none);
        Assert.Equal(-1, none);
    }

    [Fact]
    public void TabDoesNotDisplaceAWayOutThatDoesReach()
    {
        var score = SpliceScore.BestOutput(new[] { "Point", "Curve" }, "Curve", showAll: true, out var index);
        Assert.Equal(1, index);
        Assert.Equal(TypeCompat.Exact, score);
    }
}
