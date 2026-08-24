using System;
using WireDrop.Ranking;
using Xunit;

public class TypeCompatTests
{
    [Theory]
    [InlineData("Curve", "Curve")]
    [InlineData("Number", "Number")]
    public void SameTypeScoresExact(string a, string b) => Assert.Equal(100, TypeCompat.Score(a, b));

    [Theory]
    [InlineData("Circle", "Curve")]   // a circle IS a curve
    [InlineData("Line", "Curve")]
    [InlineData("Integer", "Number")]
    [InlineData("Surface", "Brep")]
    public void LosslessWideningLandsInTheDirectBand(string from, string to)
    {
        var score = TypeCompat.Score(from, to);
        Assert.True(score >= TypeCompat.DirectFloor, $"{from}->{to} scored {score}");
        Assert.Equal(2, TypeCompat.Band(score));
    }

    [Theory]
    [InlineData("Curve", "Geometry")]
    [InlineData("Number", "Colour")]
    [InlineData("Point", "Plane")]
    public void RealConversionsLandInTheMiddleBand(string from, string to)
    {
        var score = TypeCompat.Score(from, to);
        Assert.InRange(score, TypeCompat.ConvertFloor, TypeCompat.DirectFloor - 1);
        Assert.Equal(1, TypeCompat.Band(score));
    }

    [Fact]
    public void GenericPortsAcceptAnything()
    {
        Assert.True(TypeCompat.Score("Brep", "Generic") > 0);
        Assert.True(TypeCompat.Score("Curve", "Text") > 0);
    }

    [Fact]
    public void UnrelatedTypesDoNotConnect()
    {
        Assert.Equal(0, TypeCompat.Score("Mesh", "Number"));
        Assert.Equal(0, TypeCompat.Score("Curve", "Boolean"));
    }

    [Fact]
    public void DirectionMatters()
    {
        // Every circle is a curve; not every curve is a circle.
        Assert.True(TypeCompat.Score("Circle", "Curve") > TypeCompat.Score("Curve", "Circle"));
    }

    [Theory]
    [InlineData("GH_Curve", "Curve")]
    [InlineData("GH_Number", "Number")]
    [InlineData("GH_String", "Text")]
    [InlineData("GH_Interval", "Domain")]
    [InlineData("GH_ObjectWrapper", "Generic")]
    public void ShortNameStripsGooPrefixAndAliases(string typeName, string expected)
    {
        var type = new FakeType(typeName);
        Assert.Equal(expected, TypeCompat.ShortName(type));
    }

    [Fact]
    public void ShortNameHandlesNull() => Assert.Equal("Generic", TypeCompat.ShortName(null));

    [Fact]
    public void AngleIsTreatedAsNumber() =>
        Assert.Equal(100, TypeCompat.Score("Angle", "Number"));

    sealed class FakeType : System.Reflection.TypeDelegator
    {
        readonly string _name;
        public FakeType(string name) : base(typeof(object)) { _name = name; }
        public override string Name => _name;
    }
}
