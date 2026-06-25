using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Audio;

public class SidPotRegisterTests
{
    [Theory]
    [InlineData(SidAddr.POTX)]
    [InlineData(SidAddr.POTY)]
    public void Pot_Registers_Return_Idle_Value_When_No_Paddle_Or_Mouse_Is_Connected(ushort address)
    {
        var c64 = C64.BuildC64(new C64Config
        {
            LoadROMs = false,
            C64Model = "C64PAL",
            Vic2Model = "PAL"
        }, NullLoggerFactory.Instance);

        Assert.Equal(0xff, c64.Mem.Read(address));
    }
}
