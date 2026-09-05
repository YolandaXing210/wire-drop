using System;
using System.Collections.Generic;
using System.Linq;
using WireDrop.Ranking;
using Xunit;

public class ShortcutMapTests
{
    static readonly string[] Symbols = { "+", "-", "*", "/", "\\", "%", "&", "<", ">", "=" };

    [Theory]
    [InlineData("\"hello", "hello")]
    [InlineData("\"hello\"", "hello")]
    [InlineData("//a note", "a note")]
    [InlineData("\"", "")]                 // an empty panel is a useful thing to ask for
    public void QuotesAndCommentsMakeAPanel(string text, string expected)
    {
        Assert.True(ShortcutMap.TryPanel(text, out var content));
        Assert.Equal(expected, content);
    }

    [Theory]
    [InlineData("//")]                     // nothing after the slashes
    [InlineData("/x")]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsNotAPanel(string text) => Assert.False(ShortcutMap.TryPanel(text, out _));

    [Fact]
    public void TildeMakesAScribble()
    {
        Assert.True(ShortcutMap.TryScribble("~check this", out var content));
        Assert.Equal("check this", content);
    }

    [Theory]
    [InlineData("~")]                      // a scribble with nothing written on it
    [InlineData("note")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsNotAScribble(string text) => Assert.False(ShortcutMap.TryScribble(text, out _));

    [Fact]
    public void EverySymbolNamesOneComponentOfItsOwn()
    {
        var ids = new List<string>();
        foreach (var symbol in Symbols)
        {
            var id = ShortcutMap.Component(symbol);
            Assert.True(Guid.TryParse(id, out _), symbol + " -> " + (id ?? "null"));
            ids.Add(id);
        }
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("f(")]
    [InlineData("f(x+1)")]
    public void StartingAnExpressionOpensTheComponentForIt(string text)
    {
        Assert.True(Guid.TryParse(ShortcutMap.Component(text), out _));
    }

    [Theory]
    [InlineData("++")]                     // a symbol only counts on its own
    [InlineData("5")]
    [InlineData("F(")]                     // Grasshopper's test is case-sensitive
    [InlineData("add")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseNamesNoComponent(string text) => Assert.Null(ShortcutMap.Component(text));

    [Theory]
    [InlineData("3,4")]
    [InlineData("1,2,3")]
    [InlineData("0,0")]
    [InlineData("3, 4")]                   // a space after the comma is natural to type
    public void TwoNumbersAcrossACommaLookLikeAPoint(string text) =>
        Assert.True(ShortcutMap.LooksLikePoint(text));

    [Theory]
    [InlineData("5")]
    [InlineData("hello")]
    [InlineData("3,")]
    [InlineData(",4")]
    [InlineData("a,b")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseDoesNot(string text) => Assert.False(ShortcutMap.LooksLikePoint(text));
}
