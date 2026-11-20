using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Channels;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Systems.Timing;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using static Highbyte.DotNet6502.Systems.Tests.SystemConfigurerTests;

namespace Highbyte.DotNet6502.Systems.Tests.Recording;

public partial class HostRecordingTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e999")]
    [InlineData("")]
    public void Rejects_invalid_emulation_speed(string text)
    {
        Assert.False(EmulationSpeed.TryParse(text, out _));
    }

    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    public async Task Speed_change_preserves_held_hook_and_wakes_once_on_reply(string hook)
    {
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var (host, _) = await BuildHostAsync();
        var script = AddScript(host, $$"""
            function on_{{hook}}_frame()
                if emu.framecount() ~= 1 then return end
                emu.hold_frame()
                assert(http.get("http://127.0.0.1:{{((IPEndPoint)server.LocalEndpoint).Port}}/").ok)
                input.key_press("a")
            end
            """);
        var queue = Channel.CreateUnbounded<Action>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await host.Start();
            host.SetSpeed(EmulationSpeed.Unthrottled);
            host.StartPacing(action => queue.Writer.TryWrite(action));
            (await queue.Reader.ReadAsync(timeout.Token))();
            using var peer = await server.AcceptTcpClientAsync(timeout.Token);
            var hold = Assert.IsType<ScriptFrameHold>(script.FrameHold);
            long expected = hook == "before" ? 0 : 1;
            Assert.Equal(expected, host.CompletedGuestFrames);

            host.SetSpeed(EmulationSpeed.Normal);
            (await queue.Reader.ReadAsync(timeout.Token))();
            await Task.Delay(100, timeout.Token);
            Assert.False(queue.Reader.TryRead(out _));
            Assert.Same(hold, script.FrameHold);
            Assert.Equal(expected, host.CompletedGuestFrames);

            await SendHttpReplyAsync(peer, timeout.Token);
            while (host.CompletedGuestFrames == expected)
                (await queue.Reader.ReadAsync(timeout.Token))();
            Assert.Equal(expected + 1, host.CompletedGuestFrames);
            Assert.True(host.CurrentRunningSystem!.InputInjector!.IsKeyDown("a"));
            Assert.Null(script.FrameHold);
        }
        finally { host.Close(); }
    }

    [Theory]
    [InlineData(C64.SystemName)]
    [InlineData(GenericComputer.SystemName)]
    public async Task Paced_hosts_reproduce_all_121_snapshot_boundaries(string name)
    {
        var systems = new SystemList();
        systems.AddSystem(name == C64.SystemName ? new RecordingConfigurer() : new GenericConfigurer());
        var host = new RecordingHost(systems, NullLoggerFactory.Instance);
        var input = new TestInput();
        host.SetContexts(() => input);
        host.InitInputHandlerContext();
        await host.SelectSystem(name);
        await host.Start();
        using var snapshot = new MemoryStream();
        await host.SaveSnapshotAsync(snapshot);
        List<string>? reference = null;
        try
        {
            foreach (var text in new[] { "1", "0.5", "2", "unthrottled" })
            {
                Assert.True(EmulationSpeed.TryParse(text, out var speed));
                snapshot.Position = 0;
                await host.LoadSnapshotAsync(snapshot);
                var machine = host.CurrentRunningSystem!;
                var boundaries = new List<string> { Digest(machine) };
                void QueueInput()
                {
                    byte value = (byte)(boundaries.Count * 37);
                    if (name == C64.SystemName)
                    {
                        input.Keys.Clear();
                        if ((value & 1) == 1) input.Keys.Add(HostKey.KeyA);
                    }
                    else
                        host.EnqueueRemoteAction(() => machine.Mem[0xd030] = value); // Generic key input register
                }
                QueueInput();
                host.FrameCompleted = () =>
                {
                    boundaries.Add(Digest(machine));
                    if (boundaries.Count == 121) host.Pause();
                    else QueueInput();
                };
                var queue = Channel.CreateUnbounded<Action>();
                host.SetSpeed(speed);
                await host.Start();
                host.StartPacing(action => queue.Writer.TryWrite(action));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (host.EmulatorState == EmulatorState.Running)
                    (await queue.Reader.ReadAsync(timeout.Token))();
                Assert.Equal(121, boundaries.Count);
                if (reference == null) reference = boundaries;
                else Assert.Equal(reference, boundaries);
            }
        }
        finally { host.Close(); }
    }

    private static string Digest(ISystem machine)
    {
        if (machine is C64 c64)
            return Convert.ToHexString(c64.CreateRecordingAdapter().ComputeStateDigest());

        using var content = new MemoryStream();
        var writer = new SnapshotModuleWriter(content);
        var context = new SnapshotCaptureContext(machine, new SnapshotSaveOptions());
        foreach (var module in ((ISystemSnapshotProvider)machine).GetSnapshotModules())
            module.Capture(writer, context);
        return Convert.ToHexString(SHA256.HashData(content.ToArray()));
    }

    private sealed class GenericConfigurer : ISystemConfigurer
    {
        public string SystemName => GenericComputer.SystemName;
        public Task<List<string>> GetConfigurationVariants(ISystemConfig config) => Task.FromResult<List<string>>(["test"]);
        public Task<IHostSystemConfig> GetNewHostSystemConfig() => Task.FromResult<IHostSystemConfig>(new TestHostSystemConfig());
        public Task PersistHostSystemConfig(IHostSystemConfig config) => Task.CompletedTask;
        public Task<ISystem> BuildSystem(string variant, ISystemConfig config)
        {
            var system = new GenericComputerBuilder(new GenericComputerConfig(), NullLoggerFactory.Instance)
                .WithCPU().WithMemory().WithStartAddress(0xC000).Build();
            system.Mem.StoreData(0xC000, new byte[] { 0xAD, 0x30, 0xD0, 0x8D, 0x00, 0x40, 0x4C, 0x00, 0xC0 });
            return Task.FromResult<ISystem>(system);
        }
        public Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig config)
            => Task.FromResult(new SystemRunner(system));
    }
}
