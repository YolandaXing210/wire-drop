using System;
using System.Linq;
using WireDrop.Ranking;
using Xunit;

public class CategoryOrderTests
{
    static string[] Sorted(params string[] input)
    {
        var copy = (string[])input.Clone();
        Array.Sort(copy, CategoryOrder.Compare);
        return copy;
    }

    [Fact]
    public void FollowsTheRibbonOrderNotTheAlphabet()
    {
        Assert.Equal(
            new[] { "Params", "Maths", "Sets", "Vector", "Curve", "Surface", "Mesh",
                    "Intersect", "Transform", "Display" },
            Sorted("Display", "Transform", "Intersect", "Mesh", "Surface", "Curve",
                   "Vector", "Sets", "Maths", "Params"));
    }

    [Fact]
    public void OrderIsIndependentOfHowManyResultsEachHas()
    {
        // The same set in any starting order lands the same way.
        var a = Sorted("Curve", "Params", "Mesh");
        var b = Sorted("Mesh", "Curve", "Params");
        Assert.Equal(a, b);
        Assert.Equal(new[] { "Params", "Curve", "Mesh" }, a);
    }

    [Fact]
    public void ThirdPartyCategoriesFollowTheCoreOnes()
    {
        var sorted = Sorted("PanelingTools", "Curve", "Weaverbird", "Params");
        Assert.Equal(new[] { "Params", "Curve", "PanelingTools", "Weaverbird" }, sorted);
    }

    [Fact]
    public void ThirdPartyCategoriesAreAlphabeticalAmongThemselves() =>
        Assert.Equal(new[] { "Alpha", "Beta", "Zeta" }, Sorted("Zeta", "Alpha", "Beta"));

    [Fact]
    public void ParamsIsAlwaysFirst() =>
        Assert.Equal("Params", Sorted("Curve", "Display", "Params", "Mesh").First());

    [Fact]
    public void MatchingIgnoresCase() =>
        Assert.Equal(CategoryOrder.Of("Curve"), CategoryOrder.Of("curve"));

    [Fact]
    public void UnknownCategoriesRankLast()
    {
        Assert.True(CategoryOrder.Of("Weaverbird") > CategoryOrder.Of("Display"));
        Assert.Equal(int.MaxValue, CategoryOrder.Of("Weaverbird"));
    }

    [Fact]
    public void EmptyIsHandled() => Assert.Equal(int.MaxValue, CategoryOrder.Of(null));
}
