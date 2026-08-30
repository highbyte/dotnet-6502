using System.Globalization;
using System.Text;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Oric.Render;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Microsoft.Extensions.Logging.Abstractions;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class OricLuaScriptingTests
{
    [Fact]
    public void OricGlobalExposesBasicAndJoystickInformation()
    {
        var oric = new OricMachine(new Highbyte.DotNet6502.Systems.Oric.OricConfig
        {
            JoystickInterface = OricJoystickInterface.PASE,
        }, NullLoggerFactory.Instance);
        oric.LoadBasicTap(File.ReadAllBytes(GetSamplePath("HelloWorld")));
        WriteScreenText(oric, "READY");
        var adapter = BuildAdapter(oric);

        var state = Run(adapter, """
            assert(oric.basic_started())
            assert(string.find(oric.get_basic_source(), "HELLO WORLD") ~= nil)
            assert(oric.joystick_interface() == "pase")
            oric.print_text("A")
            """);

        oric.ExecuteOneFrame();

        Assert.Equal(AdapterCoroutineState.Dead, state.CoroutineState);
        Assert.Equal((byte)('A' | 0x80), oric.Mem[OricMachine.KeyboardCharacterLatchAddress]);
    }

    [Fact]
    public void LoadTapAcceptsByteTableAndUsesOneBasedFileNumber()
    {
        var oric = new OricMachine();
        var first = BuildTap(name: "FIRST", startAddress: 0x0600, payload: [0x11]);
        var second = BuildTap(name: "SECOND", startAddress: 0x0700, payload: [0xaa, 0xbb]);
        var adapter = BuildAdapter(oric);
        var luaBytes = ToLuaTable([.. first, .. second]);

        var state = Run(adapter, $$"""
            local file = oric.load_tap({{luaBytes}}, 2, false)
            assert(file.name == "SECOND")
            assert(file.type == "machinecode")
            assert(file.autorun == false)
            assert(file.start == 0x0700)
            assert(file["end"] == 0x0701)
            """);

        Assert.Equal(AdapterCoroutineState.Dead, state.CoroutineState);
        Assert.Equal(0xaa, oric.Mem[0x0700]);
        Assert.Equal(0xbb, oric.Mem[0x0701]);
    }

    [Fact]
    public void TapeTransportFunctionsExposeStatusAndMetadata()
    {
        var oric = new OricMachine();
        var tap = BuildTap(name: "DEMO", startAddress: 0x0800, payload: [0xea]);
        var adapter = BuildAdapter(oric);

        var state = Run(adapter, $$"""
            local status = oric.insert_tape({{ToLuaTable(tap)}})
            assert(status.inserted)
            assert(status.position == 0)
            assert(status.length == {{tap.Length}})
            assert(status.at_end == false)
            assert(#status.files == 1)
            assert(status.files[1].name == "DEMO")
            assert(status.files[1].start == 0x0800)
            oric.rewind_tape()
            assert(oric.tape_status().position == 0)
            oric.eject_tape()
            assert(oric.tape_status().inserted == false)
            """);

        Assert.Equal(AdapterCoroutineState.Dead, state.CoroutineState);
        Assert.False(oric.Tape.IsInserted);
    }

    [Theory]
    [InlineData("{ [1] = 0x16, [3] = 0x24 }")]
    [InlineData("{ 0x16, 256, 0x24 }")]
    [InlineData("{ 0x16, 1.5, 0x24 }")]
    [InlineData("{ 0x16, 0x16, value = 0x24 }")]
    public void TapFunctionsRejectInvalidByteTables(string luaTable)
    {
        var adapter = BuildAdapter(new OricMachine());

        var state = Run(adapter, $"oric.insert_tape({luaTable})");

        Assert.Equal(AdapterCoroutineState.RuntimeError, state.CoroutineState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public void LoadTapRejectsInvalidFileNumbers(double fileNumber)
    {
        var adapter = BuildAdapter(new OricMachine());
        var tap = BuildTap(name: "DEMO", startAddress: 0x0800, payload: [0xea]);

        var value = fileNumber.ToString(CultureInfo.InvariantCulture);
        var state = Run(adapter, $"oric.load_tap({ToLuaTable(tap)}, {value})");

        Assert.Equal(AdapterCoroutineState.RuntimeError, state.CoroutineState);
    }

    [Fact]
    public void SystemSpecificProxiesAreClearedWhenTheRunningSystemChanges()
    {
        var oric = new OricMachine(new Highbyte.DotNet6502.Systems.Oric.OricConfig
        {
            JoystickInterface = OricJoystickInterface.IJK,
        }, NullLoggerFactory.Instance);
        WriteScreenText(oric, "READY");
        var adapter = BuildAdapter(oric);

        adapter.OnSystemStarted(new GenericComputer());
        var oricState = Run(adapter, """
            assert(oric.basic_started() == false)
            assert(oric.joystick_interface() == "none")
            """);

        var c64 = C64.BuildC64(new C64Config
        {
            C64Model = "C64NTSC",
            Vic2Model = "NTSC",
            LoadROMs = false,
        }, NullLoggerFactory.Instance);
        c64.Mem[0x2b] = (byte)(C64.BASIC_LOAD_ADDRESS & 0xff);
        c64.Mem[0x2c] = (byte)(C64.BASIC_LOAD_ADDRESS >> 8);
        adapter.OnSystemStarted(c64);
        Assert.Equal(AdapterCoroutineState.Dead, Run(adapter, "assert(c64.basic_started())").CoroutineState);

        adapter.OnSystemStarted(oric);
        var c64State = Run(adapter, "assert(c64.basic_started() == false)");

        Assert.Equal(AdapterCoroutineState.Dead, oricState.CoroutineState);
        Assert.Equal(AdapterCoroutineState.Dead, c64State.CoroutineState);
    }

    [Theory]
    [InlineData("example_oric_basic_readwrite.lua")]
    [InlineData("example_oric_download_and_run_tap.lua")]
    public void SharedOricExamplesCompile(string fileName)
    {
        var adapter = BuildAdapter(new OricMachine());
        var path = Path.Combine("../../../../../resources/scripts/shared", fileName);

        var handle = adapter.LoadScript(File.ReadAllText(path), fileName);

        Assert.NotNull(handle);
    }

    private static MoonSharpScriptingEngineAdapter BuildAdapter(ISystem system)
    {
        var adapter = new MoonSharpScriptingEngineAdapter(NullLoggerFactory.Instance, "test");
        adapter.InitializeVm(
            hostApp: null,
            enqueueAction: (_, _) => { },
            new ScriptingConfig
            {
                AllowFileIO = false,
                AllowHttpRequests = false,
                AllowStore = false,
                AllowTcpClient = false,
            },
            NullLogger.Instance,
            getFrameCount: () => 0,
            getElapsedSeconds: () => 0);
        adapter.OnSystemStarted(system);
        return adapter;
    }

    private static AdapterScriptState Run(MoonSharpScriptingEngineAdapter adapter, string script)
    {
        var handle = adapter.LoadScript(script, "test.lua");
        Assert.NotNull(handle);
        return adapter.InitialResume(handle);
    }

    private static void WriteScreenText(OricMachine oric, string text)
    {
        for (var index = 0; index < text.Length; index++)
            oric.Mem[(ushort)(OricRasterizer.TextScreenAddress + index)] = (byte)text[index];
    }

    private static string ToLuaTable(byte[] data) => $"{{{string.Join(',', data)}}}";

    private static byte[] BuildTap(string name, ushort startAddress, byte[] payload)
    {
        var endAddress = (ushort)(startAddress + payload.Length - 1);
        var bytes = new List<byte>
        {
            OricTapParser.SyncByte,
            OricTapParser.SyncByte,
            OricTapParser.SyncByte,
            OricTapParser.HeaderMarker,
            0x00,
            0x00,
            OricTapFile.MachineCodeFileType,
            0x00,
            (byte)(endAddress >> 8),
            (byte)endAddress,
            (byte)(startAddress >> 8),
            (byte)startAddress,
            0x00,
        };
        bytes.AddRange(Encoding.ASCII.GetBytes(name));
        bytes.Add(0);
        bytes.AddRange(payload);
        return bytes.ToArray();
    }

    private static string GetSamplePath(string sampleName)
        => Path.Combine("../../../../../samples/Basic/Oric/Text/Build", $"{sampleName}.tap");
}
