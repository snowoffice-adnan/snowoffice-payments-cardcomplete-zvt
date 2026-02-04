using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Sockets;

namespace Snowoffice.Payments.CardCompleteZvt.Zvt;

/// <summary>
/// TcpNetwork DeviceCommunication (pure System.Net.Sockets)
/// </summary>
public sealed class TcpNetworkDeviceCommunication : IDeviceCommunication
{
    private readonly string _ipAddress;
    private readonly int _port;
    private readonly bool _enableKeepAlive;
    private readonly ILogger<TcpNetworkDeviceCommunication> _logger;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _rxCts;
    private Task? _rxLoop;

    public event Action<byte[]>? DataReceived;
    public event Action<byte[]>? DataSent;
    public event Action<ConnectionState>? ConnectionStateChanged;

    public TcpNetworkDeviceCommunication(
        string ipAddress,
        int port = 20007,
        bool enableKeepAlive = false,
        ILogger<TcpNetworkDeviceCommunication>? logger = null)
    {
        _ipAddress = ipAddress;
        _port = port;
        _enableKeepAlive = enableKeepAlive;
        _logger = logger ?? NullLogger<TcpNetworkDeviceCommunication>.Instance;
    }

    public bool IsConnected => _tcpClient?.Connected == true;

    public string ConnectionIdentifier => _ipAddress;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation($"{nameof(ConnectAsync)} - IpAddress:{_ipAddress}, Port:{_port}");

            _tcpClient = new TcpClient();

            // Connect with cancellation
            using (cancellationToken.Register(() => _tcpClient.Dispose()))
            {
                await _tcpClient.ConnectAsync(_ipAddress, _port);
            }

            _stream = _tcpClient.GetStream();

            if (_enableKeepAlive)
            {
                try
                {
                    _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "KeepAlive could not be enabled (ignored).");
                }
            }

            ConnectionStateChanged?.Invoke(ConnectionState.Connected);

            StartReceiveLoop();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(ConnectAsync)} failed");
            ConnectionStateChanged?.Invoke(ConnectionState.Disconnected);
            return false;
        }
    }

    public Task<bool> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation($"{nameof(DisconnectAsync)}");

            StopReceiveLoop();

            _stream?.Dispose();
            _stream = null;

            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;

            ConnectionStateChanged?.Invoke(ConnectionState.Disconnected);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(DisconnectAsync)} failed");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_stream == null || !IsConnected)
            return false;

        DataSent?.Invoke(data);

        _logger.LogDebug($"{nameof(SendAsync)} - {BitConverter.ToString(data)}");

        try
        {
            await _stream.WriteAsync(data, 0, data.Length, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(SendAsync)} failed");
            ConnectionStateChanged?.Invoke(ConnectionState.Disconnected);
            return false;
        }
    }

    private void StartReceiveLoop()
    {
        StopReceiveLoop();

        if (_stream == null) return;

        _rxCts = new CancellationTokenSource();
        var ct = _rxCts.Token;

        _rxLoop = Task.Run(async () =>
        {
            var buffer = new byte[4096];

            try
            {
                while (!ct.IsCancellationRequested && _stream != null && IsConnected)
                {
                    var read = await _stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (read <= 0)
                        break;

                    var data = new byte[read];
                    Buffer.BlockCopy(buffer, 0, data, 0, read);

                    _logger.LogDebug($"{nameof(DataReceived)} - {BitConverter.ToString(data)}");
                    DataReceived?.Invoke(data);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Receive loop failed");
            }
            finally
            {
                ConnectionStateChanged?.Invoke(ConnectionState.Disconnected);
            }
        }, ct);
    }

    private void StopReceiveLoop()
    {
        try { _rxCts?.Cancel(); } catch { }
        _rxCts?.Dispose();
        _rxCts = null;
        _rxLoop = null;
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
    }
}
