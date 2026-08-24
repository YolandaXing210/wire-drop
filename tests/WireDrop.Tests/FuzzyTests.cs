using WireDrop.Ranking;
using Xunit;

public class FuzzyTests
{
    static int S(string name, string query, string nick = "", string cat = "Curve", string sub = "Division")
        => Fuzzy.Score(name, nick, cat, sub, query);

    [Fact]
    public void PrefixOutranksWordStart() =>
        Assert.True(S("Divide Curve", "div") > S("Curve Division", "div"));

    [Fact]
    public void WordStartOutranksSubstring() =>
        Assert.True(S("Curve Division", "div") > S("Subdivide", "div"));

    [Fact]
    public void SubstringOutranksSubsequence() =>
        Assert.True(S("Subdivide", "div") > S("Deconstruct Vector", "dv"));

    [Fact]
    public void NickNameMatches() =>
        Assert.True(S("Divide Curve", "dc", nick: "DC") > 0);

    [Fact]
    public void NonMatchScoresZero() =>
        Assert.Equal(0, S("Divide Curve", "zzq"));

    [Fact]
    public void EmptyQueryScoresZero() =>
        Assert.Equal(0, S("Divide Curve", ""));

    [Fact]
    public void CaseIsIgnored() =>
        Assert.Equal(S("Divide Curve", "div"), S("Divide Curve", "DIV"));
}
