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
    [InlineData("Point")]
    [InlineData("Curve")]
    [InlineData("Brep")]
    [InlineData("Mesh")]
    [InlineData("Box")]
    public void GeometryTakesGeometryAsItIs(string from)
    {
        // A geometry port is a container, not a converter: scoring these as conversions
        // dropped them out of the canvas outlines, which only draw what goes in unchanged.
        var score = TypeCompat.Score(from, "Geometry");
        Assert.True(score >= TypeCompat.DirectFloor, $"{from}->Geometry scored {score}");
        Assert.Equal(2, TypeCompat.Band(score));
    }

    [Fact]
    public void AGeometryTypeNobodyHasHeardOfCanSaySoItself()
    {
        Assert.Equal(0, TypeCompat.Score("Voxel", "Geometry"));

        TypeCompat.RegisterGeometric("Voxel");

        Assert.Equal(2, TypeCompat.Band(TypeCompat.Score("Voxel", "Geometry")));
        // and only into geometry — registering says nothing about anywhere else
        Assert.Equal(0, TypeCompat.Score("Voxel", "Curve"));
    }

    [Fact]
    public void ANonGeometryTypeStillDoesNotReachAGeometryPort()
    {
        Assert.Equal(0, TypeCompat.Score("Number", "Geometry"));
        Assert.Equal(0, TypeCompat.Score("Colour", "Geometry"));
    }

    [Fact]
    public void AVerifiedCastOutranksTheTableButNotAnExactMatch()
    {
        Assert.True(TypeCompat.ValueFloor > TypeCompat.DirectFloor);
        Assert.True(TypeCompat.ValueFloor < TypeCompat.Exact);
        Assert.Equal(2, TypeCompat.Band(TypeCompat.ValueFloor));
    }

    [Fact]
    public void TheTopBandSaysSoOnlyWhenTheValueWasRead()
    {
        Assert.NotEqual(TypeCompat.BandLabel(2, false), TypeCompat.BandLabel(2, true));
        Assert.Equal(TypeCompat.BandLabel(2), TypeCompat.BandLabel(2, false));

        // Nothing was verified about the weaker bands, so they read the same either way.
        Assert.Equal(TypeCompat.BandLabel(1, false), TypeCompat.BandLabel(1, true));
        Assert.Equal(TypeCompat.BandLabel(0, false), TypeCompat.BandLabel(0, true));
    }

    [Theory]
    [InlineData("Circle", "Curve")]   // a circle IS a curve
    [InlineData("Line", "Curve")]
    [InlineData("Integer", "Number")]
    [InlineData("Surface", "Brep")]
    [InlineData("Point", "Vector")]      // three numbers either way, nothing computed
    [InlineData("Vector", "Point")]
    [InlineData("Transform", "Matrix")]  // the same sixteen numbers under two names
    [InlineData("Matrix", "Transform")]
    [InlineData("Number", "Complex")]    // a real is a complex with no imaginary part
    public void LosslessWideningLandsInTheDirectBand(string from, string to)
    {
        var score = TypeCompat.Score(from, to);
        Assert.True(score >= TypeCompat.DirectFloor, $"{from}->{to} scored {score}");
        Assert.Equal(2, TypeCompat.Band(score));
    }

    [Theory]
    [InlineData("Mesh", "Brep")]      // meshing is a real conversion, and can lose you something
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
