using System.Net;
using System.Net.Sockets;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Recording;

public partial class HostRecordingTests
{
    private static ScriptingEngine AddScript(HostApp host, string source)
    {
        var logs = NullLoggerFactory.Instance;
        var adapter = new MoonSharpScriptingEngineAdapter(logs, "test");
        var engine = new ScriptingEngine(adapter, new ScriptingConfig
        {
            ScriptLoader = () => [("controller.lua", source)],
            EnableScriptsAtStart = true, AllowTcpClient = true, AllowStore = false, AllowFileIO = false,
        }, logs);
        host.SetScriptingEngine(engine);
        return engine;
    }

    [Fact]
    public async Task Lua_reads_exact_io_storage_slots_independently_of_cpu_banking()
    {
        var (host, _) = await BuildHostAsync();
        await host.Start();
        var c64 = Assert.IsType<C64>(host.CurrentRunningSystem);
        c64.Mem[0xd000] = 44;
        c64.Mem[0xd010] = 1;
        c64.Mem[0xd015] = 0x80;
        c64.Mem[0xd027] = 5;
        c64.WriteIOStorage(0xd040, 0x66);
        c64.WriteIOStorage(0xdfff, 0xa5);
        c64.Mem[1] = 0x30;
        c64.Mem[0xd000] = 0x99;
        var before = c64.CreateRecordingAdapter().ComputeStateDigest();
        var engine = AddScript(host, """
            assert(not pcall(c64.read_io_storage))
            for _, a in ipairs({false, "0xD000", 0, 0x400, 0xd000 + 0.5, 0x10000}) do
                assert(not pcall(c64.read_io_storage, a))
            end
            local x = c64.read_io_storage(0xd000) + 256 * (c64.read_io_storage(0xd010) % 2)
            assert(x == 300 and c64.read_io_storage(0xd001) == 0)
            assert(c64.read_io_storage(0xd040) == 0x66 and mem.read(0xd000) == 0x99)
            assert(c64.read_io_storage(0xd015) == 128 and c64.read_io_storage(0xd027) == 5)
            assert(c64.read_io_storage(0xdfff) == 0xa5)
            """);
        Assert.Equal(ScriptExecutionState.Completed, Assert.Single(engine.GetScriptStatuses()).State);
        Assert.Equal(before, c64.CreateRecordingAdapter().ComputeStateDigest());
        Assert.Equal(0x99, c64.Mem[0xd000]);

        host.Stop();
        engine.UpsertScript("absent.lua", """
            local value, reason = c64.read_io_storage(0xd000)
            assert(value == nil and type(reason) == "string")
            """);
        Assert.Equal(ScriptExecutionState.Completed, engine.GetScriptStatuses().Single(s => s.FileName == "absent.lua").State);
        host.Close();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public async Task Delayed_controller_observes_sprite_and_supplies_the_recorded_frame(int delayTicks)
    {
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var (host, _) = await BuildHostAsync();
        var engine = AddScript(host, $$"""
            local controller
            local calls = 0
            function on_before_frame()
                calls = calls + 1
                assert(calls == emu.framecount())
                emu.hold_frame()
                if not controller then
                    controller = assert(tcp.connect("127.0.0.1", {{((IPEndPoint)server.LocalEndpoint).Port}}).data)
                end
                assert(controller:send({assert(c64.read_io_storage(0xd000))}).ok)
                local reply = assert(controller:receive(1).data)
                input.joystick_set(2, "fire", reply[1] == 1)
            end
            """);
        await host.Start();
        var c64 = Assert.IsType<C64>(host.CurrentRunningSystem);
        c64.Mem[0xd000] = 44;
        using var file = new MemoryStream();
        await host.StartDemoRecordingAsync(file);
        host.RunEmulatorOneFrame();
        Assert.NotNull(engine.FrameHold);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var peer = await server.AcceptTcpClientAsync(timeout.Token);
        var expected = new List<byte[]>();
        for (int frame = 0; frame < 2; frame++)
        {
            var cycles = c64.CPU.ExecState.CyclesConsumed;
            var observed = new byte[1];
            var read = peer.GetStream().ReadExactlyAsync(observed, timeout.Token).AsTask();
            while (!read.IsCompleted)
            {
                host.RunEmulatorOneFrame();
                await Task.Delay(1, timeout.Token);
            }
            await read;
            Assert.Equal(44, observed[0]);
            for (int tick = 0; tick < delayTicks; tick++)
            {
                host.RunEmulatorOneFrame();
                Assert.Equal(cycles, c64.CPU.ExecState.CyclesConsumed);
                Assert.Equal(frame, host.DemoSession!.Frame);
            }
            await peer.GetStream().WriteAsync(new byte[] { (byte)(1 - frame) }, timeout.Token);
            while (host.DemoSession!.Frame == frame)
            {
                host.RunEmulatorOneFrame();
                await Task.Delay(1, timeout.Token);
            }
            Assert.Equal(frame == 0, c64.Cia1.Joystick.CurrentJoystickActions[2].Contains(JoystickAction.Fire));
            expected.Add(c64.CreateRecordingAdapter().ComputeStateDigest());
        }

        host.StopDemoRecording();
        peer.Close();
        file.Position = 0;
        await host.LoadDemoAsync(file);
        var replay = Assert.IsType<C64>(host.CurrentRunningSystem);
        foreach (var digest in expected)
        {
            await host.StepEmulatorFramesAsync(1);
            Assert.Equal((byte)44, replay.ReadIOStorage(0xd000));
            Assert.Equal(digest, replay.CreateRecordingAdapter().ComputeStateDigest());
        }
        host.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Http_wait_freezes_only_an_opted_in_hook_and_restore_discards_its_reply(bool hold)
    {
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var (host, _) = await BuildHostAsync();
        var engine = AddScript(host, $$"""
            local first = true
            function on_after_frame()
                if not first then return end
                first = false
                {{(hold ? "emu.hold_frame()" : "")}}
                local reply = http.get("http://127.0.0.1:{{((IPEndPoint)server.LocalEndpoint).Port}}/")
                assert(not pcall(emu.hold_frame))
                assert(reply.ok)
                input.key_press("a")
            end
            """);
        await host.Start();
        using var snapshot = new MemoryStream();
        await host.SaveSnapshotAsync(snapshot);
        host.RunEmulatorOneFrame();
        var machine = Assert.IsType<C64>(host.CurrentRunningSystem);
        var cycles = machine.CPU.ExecState.CyclesConsumed;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var peer = await server.AcceptTcpClientAsync(timeout.Token);
        host.RunEmulatorOneFrame();
        Assert.Equal(hold, cycles == machine.CPU.ExecState.CyclesConsumed);
        if (hold)
        {
            snapshot.Position = 0;
            await host.LoadSnapshotAsync(snapshot);
            Assert.Null(engine.FrameHold);
        }
        else
        {
            await SendHttpReplyAsync(peer, timeout.Token);
            while (!machine.InputInjector!.IsKeyDown("a"))
            {
                engine.ResumeCoroutines();
                await Task.Delay(1, timeout.Token);
            }
        }
        if (hold)
        {
            // Restoring cancelled the old request. A peer that already sent its reply
            // may still have a completion queued when the new machine is resumed.
            try { await SendHttpReplyAsync(peer, timeout.Token); }
            catch (IOException) { } // The cancelled request already closed the connection.
            await host.Start();
            var restored = Assert.IsType<C64>(host.CurrentRunningSystem);
            for (int i = 0; i < 4; i++) host.RunEmulatorOneFrame();
            Assert.False(restored.InputInjector!.IsKeyDown("a"));
        }
        host.Close();
    }

    [Fact]
    public async Task Paused_recording_step_awaits_controller_and_counts_each_guest_frame_once()
    {
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var (host, _) = await BuildHostAsync();
        var engine = AddScript(host, $$"""
            function on_before_frame()
                if emu.framecount() > 1 then return end
                emu.hold_frame()
                assert(http.get("http://127.0.0.1:{{((IPEndPoint)server.LocalEndpoint).Port}}/").ok)
                input.key_press("a")
            end
            function on_after_frame()
                emu.hold_frame()
                emu.yield()
            end
            """);
        await host.Start();
        host.Pause();
        using var file = new MemoryStream();
        await host.StartDemoRecordingAsync(file);
        var step = host.StepEmulatorFramesAsync(2);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var peer = await server.AcceptTcpClientAsync(timeout.Token);
        Assert.False(step.IsCompleted);
        Assert.Equal(0, host.DemoSession!.Frame);
        await SendHttpReplyAsync(peer, timeout.Token);
        await step.WaitAsync(timeout.Token);
        Assert.Equal(2, host.DemoSession.Frame);
        Assert.True(engine.FrameHold?.Finished);
        host.StopDemoRecording();
        host.Close();
    }

    [Fact]
    public async Task Recording_multistep_stops_at_first_failed_after_hook()
    {
        var (host, _) = await BuildHostAsync();
        AddScript(host, "function on_after_frame() emu.hold_frame() error('controller failed') end");
        await host.Start();
        host.Pause();
        using var file = new MemoryStream();
        await host.StartDemoRecordingAsync(file);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StepEmulatorFramesAsync(3));
        Assert.Equal(1, host.DemoSession!.Frame);
        Assert.Equal(EmulatorState.Paused, host.EmulatorState);
        host.Close();
    }

    private static async Task SendHttpReplyAsync(TcpClient client, CancellationToken token)
    {
        var response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK"u8.ToArray();
        await client.GetStream().WriteAsync(response, token);
    }

    [Fact]
    public async Task After_hook_holds_completed_frame_and_targets_next_input()
    {
        var (host, _) = await BuildHostAsync();
        var engine = AddScript(host, """
            function on_after_frame()
                if emu.framecount() ~= 1 then return end
                emu.hold_frame()
                emu.yield()
                emu.yield()
                input.joystick_set(2, "fire", true)
            end
            """);
        await host.Start();
        var c64 = Assert.IsType<C64>(host.CurrentRunningSystem);
        host.RunEmulatorOneFrame();
        var cycles = c64.CPU.ExecState.CyclesConsumed;
        Assert.True(cycles > 0);
        Assert.NotNull(engine.FrameHold);
        host.RunEmulatorOneFrame();
        Assert.Equal(cycles, c64.CPU.ExecState.CyclesConsumed);
        host.RunEmulatorOneFrame();
        Assert.True(c64.CPU.ExecState.CyclesConsumed > cycles);
        Assert.Contains(JoystickAction.Fire, c64.Cia1.Joystick.CurrentJoystickActions[2]);
        host.RunEmulatorOneFrame();
        Assert.DoesNotContain(JoystickAction.Fire, c64.Cia1.Joystick.CurrentJoystickActions[2]);
        host.Close();
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("reload")]
    [InlineData("error")]
    public async Task Interrupted_before_hook_pauses_without_consuming_input(string operation)
    {
        var (host, _) = await BuildHostAsync();
        var engine = AddScript(host, """
            assert(not pcall(emu.hold_frame))
            function on_before_frame()
                input.key_press("b")
                emu.hold_frame()
                input.key_press("a")
                emu.yield()
                assert(not pcall(emu.hold_frame))
                emu.frameadvance()
            end
            """);
        await host.Start();
        var c64 = Assert.IsType<C64>(host.CurrentRunningSystem);
        var cycles = c64.CPU.ExecState.CyclesConsumed;
        host.RunEmulatorOneFrame();
        Assert.NotNull(engine.FrameHold);
        if (operation == "disable") engine.SetScriptEnabled("controller.lua", false);
        else if (operation == "reload") engine.ReloadScript("controller.lua");
        else host.RunEmulatorOneFrame();
        Assert.Null(engine.FrameHold);
        Assert.Equal(EmulatorState.Paused, host.EmulatorState);
        Assert.False(c64.InputInjector!.IsKeyDown("a"));
        Assert.False(c64.InputInjector.IsKeyDown("b"));
        Assert.Equal(cycles, c64.CPU.ExecState.CyclesConsumed);
        host.Close();
    }
}
