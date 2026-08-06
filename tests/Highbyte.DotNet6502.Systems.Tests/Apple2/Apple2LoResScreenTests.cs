using Highbyte.DotNet6502.Systems.Apple2.Video;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Apple2LoResScreenTests
{
    [Fact]
    public void The_Low_Nibble_Is_The_Upper_Block_And_The_High_Nibble_The_Lower()
    {
        Assert.Equal(0x01, Apple2LoResScreen.GetColorIndex(0xF1, upperBlock: true));
        Assert.Equal(0x0F, Apple2LoResScreen.GetColorIndex(0xF1, upperBlock: false));
    }

    [Fact]
    public void The_Block_Grid_Covers_The_280_By_192_Display()
    {
        Assert.Equal(280, Apple2LoResScreen.BlockColumns * Apple2LoResScreen.BlockPixelWidth);
        Assert.Equal(192, Apple2LoResScreen.BlockRows * Apple2LoResScreen.BlockPixelHeight);
    }

    [Fact]
    public void The_Palette_Has_16_Colors_With_Black_First_And_White_Last()
    {
        Assert.Equal(16, Apple2LoResScreen.Palette.Length);
        Assert.Equal(System.Drawing.Color.FromArgb(255, 0, 0, 0), Apple2LoResScreen.Palette[0]);
        Assert.Equal(System.Drawing.Color.FromArgb(255, 255, 255, 255), Apple2LoResScreen.Palette[15]);
    }
}
