using Highbyte.DotNet6502.Systems.Apple2.Video;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2CharSetTests
{
    [Theory]
    [InlineData(0x00, Apple2TextAttribute.Inverse)]
    [InlineData(0x3F, Apple2TextAttribute.Inverse)]
    [InlineData(0x40, Apple2TextAttribute.Flash)]
    [InlineData(0x7F, Apple2TextAttribute.Flash)]
    [InlineData(0x80, Apple2TextAttribute.Normal)]
    [InlineData(0xC1, Apple2TextAttribute.Normal)]
    [InlineData(0xFF, Apple2TextAttribute.Normal)]
    public void Attribute_Comes_From_The_Top_Two_Bits(byte screenByte, Apple2TextAttribute expected)
    {
        Assert.Equal(expected, Apple2CharSet.GetAttribute(screenByte));
    }

    [Theory]
    [InlineData(0xC1, 'A')]   // normal video 'A' — what Applesoft writes
    [InlineData(0x81, 'A')]   // the second copy of the glyph set in the normal range
    [InlineData(0x01, 'A')]   // inverse 'A'
    [InlineData(0x41, 'A')]   // flashing 'A'
    [InlineData(0xA0, ' ')]   // normal space
    [InlineData(0x20, ' ')]   // inverse space
    [InlineData(0xC0, '@')]
    [InlineData(0xDA, 'Z')]
    [InlineData(0xB0, '0')]
    [InlineData(0xBF, '?')]
    public void Screen_Byte_Maps_To_The_Glyph_The_Character_Generator_Draws(byte screenByte, char expected)
    {
        Assert.Equal((byte)expected, Apple2CharSet.ToAscii(screenByte));
        Assert.Equal(expected.ToString(), Apple2CharSet.ScreenCodeToUnicode(screenByte));
    }

    [Fact]
    public void The_Character_Generator_Holds_Exactly_Sixty_Four_Distinct_Glyphs()
    {
        var glyphs = new HashSet<byte>();
        for (var index = 0; index < Apple2CharSet.GlyphCount; index++)
            glyphs.Add(Apple2CharSet.GlyphIndexToAscii((byte)index));

        Assert.Equal(Apple2CharSet.GlyphCount, glyphs.Count);

        // The set is ASCII $40-$5F followed by ASCII $20-$3F.
        Assert.Equal((byte)0x40, Apple2CharSet.GlyphIndexToAscii(0x00));
        Assert.Equal((byte)0x5F, Apple2CharSet.GlyphIndexToAscii(0x1F));
        Assert.Equal((byte)0x20, Apple2CharSet.GlyphIndexToAscii(0x20));
        Assert.Equal((byte)0x3F, Apple2CharSet.GlyphIndexToAscii(0x3F));
    }

    [Theory]
    [InlineData('A', Apple2TextAttribute.Normal, 0xC1)]
    [InlineData('A', Apple2TextAttribute.Inverse, 0x01)]
    [InlineData('A', Apple2TextAttribute.Flash, 0x41)]
    [InlineData(' ', Apple2TextAttribute.Normal, 0xA0)]
    [InlineData('?', Apple2TextAttribute.Normal, 0xBF)]
    public void FromAscii_Encodes_The_Requested_Attribute(char ascii, Apple2TextAttribute attribute, byte expected)
    {
        Assert.Equal(expected, Apple2CharSet.FromAscii((byte)ascii, attribute));
    }

    [Fact]
    public void Lowercase_Is_Folded_To_Uppercase_Because_There_Are_No_Lowercase_Glyphs()
    {
        Assert.Equal(Apple2CharSet.FromAscii((byte)'A'), Apple2CharSet.FromAscii((byte)'a'));
    }

    [Fact]
    public void FromAscii_And_ToAscii_Round_Trip_Over_The_Printable_Range()
    {
        for (var ascii = 0x20; ascii <= 0x5F; ascii++)
        {
            foreach (var attribute in new[] { Apple2TextAttribute.Normal, Apple2TextAttribute.Inverse, Apple2TextAttribute.Flash })
            {
                var screenByte = Apple2CharSet.FromAscii((byte)ascii, attribute);
                Assert.Equal(attribute, Apple2CharSet.GetAttribute(screenByte));
                Assert.Equal((byte)ascii, Apple2CharSet.ToAscii(screenByte));
            }
        }
    }
}
