using System.Collections.Concurrent;
using Highbyte.DotNet6502.Systems.Commodore64.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64;

public class HayesModemTransportTests
{
    [Fact]
    public async Task At_Command_Returns_Ok()
    {
        var transport = CreateTransport();

        await SendAsciiAsync(transport, "AT\r");

        Assert.Equal("\r\nOK\r\n", DrainAscii(transport));
    }

    [Fact]
    public async Task Atdt_Connects_Using_Dial_Target_And_Enters_Data_Mode()
    {
        FakeDataTransport? connectedTransport = null;
        var transport = CreateTransport((host, port) =>
        {
            connectedTransport = new FakeDataTransport(host, port);
            return connectedTransport;
        });

        await SendAsciiAsync(transport, "ATDTexample.com:6400\r");

        Assert.NotNull(connectedTransport);
        Assert.Equal("example.com", connectedTransport!.Host);
        Assert.Equal(6400, connectedTransport.Port);
        Assert.Equal("\r\nCONNECT 1200\r\n", DrainAscii(transport));
        Assert.True(transport.IsCarrierDetected);
        Assert.True(transport.IsDataSetReady);

        await transport.SendAsync((byte)'X');

        Assert.Equal((byte)'X', Assert.Single(connectedTransport.SentBytes));
    }

    [Fact]
    public async Task Lost_Carrier_Queues_No_Carrier_And_Returns_To_Command_Mode()
    {
        FakeDataTransport? connectedTransport = null;
        var transport = CreateTransport((host, port) =>
        {
            connectedTransport = new FakeDataTransport(host, port);
            return connectedTransport;
        });

        await SendAsciiAsync(transport, "ATDTexample.com:6400\r");
        Assert.Equal("\r\nCONNECT 1200\r\n", DrainAscii(transport));

        connectedTransport!.IsConnectedValue = false;

        Assert.True(transport.TryDequeueReceivedByte(out var firstByte));
        Assert.Equal("\r\nNO CARRIER\r\n", DrainAsciiIncludingFirstByte(transport, firstByte));
        Assert.False(transport.IsCarrierDetected);
        Assert.False(transport.IsDataSetReady);
    }

    [Fact]
    public async Task Invalid_Dial_Target_Returns_Error()
    {
        var transport = CreateTransport();

        await SendAsciiAsync(transport, "ATDTnot-a-target\r");

        Assert.Equal("\r\nERROR\r\n", DrainAscii(transport));
    }

    private static HayesModemTransport CreateTransport(Func<string, int, ISwiftLinkTransport>? dialTransportFactory = null)
    {
        dialTransportFactory ??= (host, port) => new FakeDataTransport(host, port);
        return new HayesModemTransport(dialTransportFactory, NullLogger<HayesModemTransport>.Instance);
    }

    private static async Task SendAsciiAsync(ISwiftLinkTransport transport, string text)
    {
        foreach (var ch in text)
            await transport.SendAsync((byte)ch);
    }

    private static string DrainAscii(ISwiftLinkTransport transport)
    {
        var chars = new List<char>();
        while (transport.TryDequeueReceivedByte(out var value))
            chars.Add((char)value);
        return new string(chars.ToArray());
    }

    private static string DrainAsciiIncludingFirstByte(ISwiftLinkTransport transport, byte firstByte)
    {
        var chars = new List<char> { (char)firstByte };
        while (transport.TryDequeueReceivedByte(out var value))
            chars.Add((char)value);
        return new string(chars.ToArray());
    }

    private sealed class FakeDataTransport : ISwiftLinkTransport
    {
        public FakeDataTransport(string host, int port)
        {
            Host = host;
            Port = port;
            IsConnectedValue = true;
        }

        public string Host { get; }
        public int Port { get; }
        public bool IsConnectedValue { get; set; }
        public List<byte> SentBytes { get; } = new();
        public ConcurrentQueue<byte> ReceivedBytes { get; } = new();

        public bool IsConnected => IsConnectedValue;
        public bool IsCarrierDetected => IsConnectedValue;
        public bool IsDataSetReady => IsConnectedValue;

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnectedValue = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnectedValue = false;
            return ValueTask.CompletedTask;
        }

        public bool TryDequeueReceivedByte(out byte value)
            => ReceivedBytes.TryDequeue(out value);

        public ValueTask SendAsync(byte value, CancellationToken cancellationToken = default)
        {
            SentBytes.Add(value);
            return ValueTask.CompletedTask;
        }

        public void Reset()
        {
            SentBytes.Clear();
            while (ReceivedBytes.TryDequeue(out _))
            {
            }
        }

        public void Dispose()
        {
            IsConnectedValue = false;
        }
    }
}
