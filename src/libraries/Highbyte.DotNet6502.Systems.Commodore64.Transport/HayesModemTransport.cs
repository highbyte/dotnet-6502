using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.Transport;

public sealed class HayesModemTransport : ISwiftLinkTransport
{
    private const string ConnectResponse = "CONNECT 1200";
    private const string NoCarrierResponse = "NO CARRIER";
    private const string OkResponse = "OK";
    private const string ErrorResponse = "ERROR";
    private const string InfoResponse = "DOTNET6502 SWIFTLINK MODEM";
    private const string ResultCodePrefix = "\r\n";
    private const string ResultCodeSuffix = "\r\n";

    private readonly Func<string, int, ISwiftLinkTransport> _dialTransportFactory;
    private readonly ConcurrentQueue<byte> _receivedBytes = new();
    private readonly ILogger _logger;
    private readonly StringBuilder _commandBuffer = new();

    private ISwiftLinkTransport? _dataTransport;
    private ModemMode _mode = ModemMode.Command;
    private bool _lastCarrierDetected;
    public HayesModemTransport(Func<string, int, ISwiftLinkTransport> dialTransportFactory, ILogger logger)
    {
        _dialTransportFactory = dialTransportFactory;
        _logger = logger;
    }

    public bool IsConnected => IsCarrierDetected;

    public bool IsCarrierDetected => _mode == ModemMode.Data && _dataTransport?.IsConnected == true;

    public bool IsDataSetReady => IsCarrierDetected;

    public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SwiftLink Hayes modem transport ready in command mode.");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await HangUpAsync(cancellationToken);
        Reset();
    }

    public bool TryDequeueReceivedByte(out byte value)
    {
        PollCarrierState();

        if (_receivedBytes.TryDequeue(out value))
            return true;

        if (_mode == ModemMode.Data && _dataTransport != null)
        {
            if (_dataTransport.TryDequeueReceivedByte(out value))
                return true;
        }

        value = 0;
        return false;
    }

    public async ValueTask SendAsync(byte value, CancellationToken cancellationToken = default)
    {
        PollCarrierState();

        if (_mode == ModemMode.Data)
        {
            var dataTransport = _dataTransport;
            if (dataTransport == null)
                return;

            await dataTransport.SendAsync(value, cancellationToken);
            return;
        }

        if (value == (byte)'\n')
            return;

        if (value != (byte)'\r')
        {
            _commandBuffer.Append((char)value);
            return;
        }

        var command = _commandBuffer.ToString();
        _commandBuffer.Clear();

        if (command.Length == 0)
            return;

        await ExecuteCommandAsync(command, cancellationToken);
    }

    public void Reset()
    {
        _commandBuffer.Clear();
        while (_receivedBytes.TryDequeue(out _))
        {
        }

        _mode = ModemMode.Command;
        _lastCarrierDetected = false;
    }

    public void Dispose()
    {
        HangUpAsync().AsTask().GetAwaiter().GetResult();
        Reset();
    }

    private async ValueTask ExecuteCommandAsync(string command, CancellationToken cancellationToken)
    {
        var normalizedCommand = command.Trim();
        _logger.LogInformation("SwiftLink Hayes modem received command: {Command}", normalizedCommand);

        if (normalizedCommand.Equals("AT", StringComparison.OrdinalIgnoreCase))
        {
            EnqueueResponse(OkResponse);
            return;
        }

        if (normalizedCommand.Equals("ATZ", StringComparison.OrdinalIgnoreCase)
            || normalizedCommand.Equals("AT&F", StringComparison.OrdinalIgnoreCase))
        {
            await HangUpAsync(cancellationToken);
            EnqueueResponse(OkResponse);
            return;
        }

        if (normalizedCommand.Equals("ATI", StringComparison.OrdinalIgnoreCase))
        {
            EnqueueResponse(InfoResponse);
            return;
        }

        if (normalizedCommand.Equals("ATH", StringComparison.OrdinalIgnoreCase))
        {
            await HangUpAsync(cancellationToken);
            EnqueueResponse(OkResponse);
            return;
        }

        if (normalizedCommand.StartsWith("ATDT", StringComparison.OrdinalIgnoreCase))
        {
            var dialTarget = normalizedCommand[4..].Trim();
            await DialAsync(dialTarget, cancellationToken);
            return;
        }

        EnqueueResponse(ErrorResponse);
    }

    private async ValueTask DialAsync(string dialTarget, CancellationToken cancellationToken)
    {
        if (!TryParseDialTarget(dialTarget, out var host, out var port))
        {
            _logger.LogWarning("SwiftLink Hayes modem could not parse dial target: {DialTarget}", dialTarget);
            EnqueueResponse(ErrorResponse);
            return;
        }

        await HangUpAsync(cancellationToken);

        try
        {
            var dataTransport = _dialTransportFactory(host, port);
            await dataTransport.ConnectAsync(cancellationToken);

            _dataTransport = dataTransport;
            _mode = ModemMode.Data;
            _lastCarrierDetected = true;
            EnqueueResponse(ConnectResponse);

            _logger.LogInformation("SwiftLink Hayes modem connected to {Host}:{Port}.", host, port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SwiftLink Hayes modem failed to connect to {Host}:{Port}.", host, port);
            _dataTransport?.Dispose();
            _dataTransport = null;
            _mode = ModemMode.Command;
            _lastCarrierDetected = false;
            EnqueueResponse(NoCarrierResponse);
        }
    }

    private async ValueTask HangUpAsync(CancellationToken cancellationToken = default)
    {
        var dataTransport = _dataTransport;
        _dataTransport = null;
        _mode = ModemMode.Command;
        _lastCarrierDetected = false;

        if (dataTransport == null)
            return;

        try
        {
            await dataTransport.DisconnectAsync(cancellationToken);
        }
        finally
        {
            dataTransport.Dispose();
        }
    }

    private void PollCarrierState()
    {
        var isCarrierDetected = IsCarrierDetected;
        if (_lastCarrierDetected && !isCarrierDetected)
        {
            _logger.LogInformation("SwiftLink Hayes modem lost carrier.");
            _mode = ModemMode.Command;
            _dataTransport?.Dispose();
            _dataTransport = null;
            EnqueueResponse(NoCarrierResponse);
        }

        _lastCarrierDetected = isCarrierDetected;
    }

    private void EnqueueResponse(string response)
    {
        var fullResponse = $"{ResultCodePrefix}{response}{ResultCodeSuffix}";
        _logger.LogDebug("SwiftLink Hayes modem enqueuing response: {Response}", response);
        foreach (var ch in Encoding.ASCII.GetBytes(fullResponse))
            _receivedBytes.Enqueue(ch);
    }

    private static bool TryParseDialTarget(string dialTarget, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        var lastColonIndex = dialTarget.LastIndexOf(':');
        if (lastColonIndex <= 0 || lastColonIndex >= dialTarget.Length - 1)
            return false;

        host = dialTarget[..lastColonIndex];
        return int.TryParse(dialTarget[(lastColonIndex + 1)..], out port) && port is > 0 and <= 65535;
    }

    private enum ModemMode
    {
        Command,
        Data
    }
}
