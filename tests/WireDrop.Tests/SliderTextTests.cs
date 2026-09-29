using WireDrop.Ranking;
using Xunit;

public class SliderTextTests
{
    [Theory]
    [InlineData("5")]
    [InlineData("-2.5")]
    [InlineData("0<5<10")]
    [InlineData("2+3")]
    public void Text_carrying_a_number_is_offered_to_grasshoppers_parser(string text)
    {
        Assert.True(SliderText.HasDigit(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pi")]          // parses as a number, but you meant Pipe
    [InlineData("e")]
    [InlineData("curve")]
    public void Text_without_a_number_never_is(string text)
    {
        Assert.False(SliderText.HasDigit(text));
    }

    [Fact]
    public void Shows_the_value_and_the_range_grasshopper_chose()
    {
        Assert.Equal("5   0 to 10", SliderText.Describe("5", "0", "10"));
    }

    [Fact]
    public void Negatives_read_the_same_way()
    {
        Assert.Equal("-2   -10 to 0", SliderText.Describe("-2", "-10", "0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_value_means_no_label(string value)
    {
        Assert.Equal(string.Empty, SliderText.Describe(value, "0", "10"));
    }

    [Theory]
    [InlineData(null, "10")]
    [InlineData("0", null)]
    [InlineData("", "10")]
    public void A_half_known_range_is_left_off(string minimum, string maximum)
    {
        Assert.Equal("5", SliderText.Describe("5", minimum, maximum));
    }

    [Fact]
    public void A_collapsed_range_is_left_off()
    {
        Assert.Equal("0", SliderText.Describe("0", "0", "0"));
    }

    [Fact]
    public void Surrounding_space_never_reaches_the_row()
    {
        Assert.Equal("0.25   0.00 to 1.00", SliderText.Describe(" 0.25 ", " 0.00 ", " 1.00 "));
    }
}
