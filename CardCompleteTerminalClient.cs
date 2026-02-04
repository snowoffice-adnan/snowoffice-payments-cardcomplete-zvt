using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Snowoffice.Payments.CardCompleteZvt.Abstractions;
using Snowoffice.Payments.CardCompleteZvt.Internal;
using Snowoffice.Payments.CardCompleteZvt.Models;
using Snowoffice.Payments.CardCompleteZvt.Zvt;
using Snowoffice.Payments.CardCompleteZvt.Zvt.Models;
using System.Text;

namespace Snowoffice.Payments.CardCompleteZvt;

public sealed class CardCompleteTerminalClient : ICardCompleteTerminalClient
{
    private readonly ILogger<CardCompleteTerminalClient> _log;

    private TcpNetworkDeviceCommunication? _device;
    private ZvtClient? _client;

    private readonly ReceiptBuffer _receipts = new();
    private int _lastReceiptNumber;
    private string? _evidenceDir;
    public string? CurrentEvidenceDirectory => _evidenceDir;

    private CardCompleteT4Watchdog? _t4;
    private CardCompleteTraceLogger? _trace;

    public event EventHandler<TerminalStatusEventArgs>? StatusInformationReceived;
    public event EventHandler<string>? IntermediateStatusInformationReceived;
    public event EventHandler<string>? PrintLineReceived;
    public event EventHandler<CardCompleteReceiptBundle>? ReceiptUpdated;

    public string EvidenceRootDirectory { get; set; } =
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 "KIOSK", "cardcomplete", "evidence");

    public bool IsConnected => _device?.IsConnected == true;

    public CardCompleteTerminalClient(ILogger<CardCompleteTerminalClient>? log = null)
    {
        _log = log ?? NullLogger<CardCompleteTerminalClient>.Instance;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task ConnectAsync(CardCompleteConnectionOptions options, CancellationToken ct = default)
    {
        if (IsConnected) return;

        if (string.IsNullOrWhiteSpace(options.IpAddress))
            throw new ArgumentException("IpAddress is required", nameof(options));

        _log.LogInformation("Connecting to CardComplete terminal {Ip}:{Port}", options.IpAddress, options.Port);

        _device = new TcpNetworkDeviceCommunication(
            ipAddress: options.IpAddress.Trim(),
            port: options.Port,
            enableKeepAlive: options.EnableKeepAlive,
            logger: NullLogger<TcpNetworkDeviceCommunication>.Instance);

        if (_trace == null || string.IsNullOrWhiteSpace(_evidenceDir))
            StartEvidence(null);

        _trace?.Info($"CONNECT ip={options.IpAddress} port={options.Port} keepAlive={options.EnableKeepAlive}");

        _device.DataSent += OnDeviceDataSent;
        _device.DataReceived += OnDeviceDataReceived;

        try
        {
            var ok = await _device.ConnectAsync();
            if (!ok || !_device.IsConnected)
                throw new InvalidOperationException("Terminal connection failed");
        }
        catch
        {
            _device.DataSent -= OnDeviceDataSent;
            _device.DataReceived -= OnDeviceDataReceived;
            throw;
        }

        if (options.PostConnectDelay > TimeSpan.Zero)
            await Task.Delay(options.PostConnectDelay, ct);

        _log.LogInformation("Terminal connected");
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        try
        {
            StopT4Watchdog();
            ResetBuffers();
            UnhookClientEvents();

            _t4?.Dispose();
            _t4 = null;

            _trace?.Info("=== Evidence ended ===");
            _trace = null;

            _client?.Dispose();
            _client = null;

            if (_device != null)
            {
                _device.DataSent -= OnDeviceDataSent;
                _device.DataReceived -= OnDeviceDataReceived;

                try { await _device.DisconnectAsync(); } catch { /* ignore */ }
                (_device as IDisposable)?.Dispose();
                _device = null;
            }
        }
        finally
        {
            // nothing
        }
    }

    public async Task LoginAsync(CardCompleteRegistrationOptions options, CancellationToken ct = default)
    {
        EnsureConnected();

        if (_trace == null || string.IsNullOrWhiteSpace(_evidenceDir))
            StartEvidence(null);

        // Create client with the configured password/language/encoding
        _client?.Dispose();
        _client = new ZvtClient(
            deviceCommunication: _device!,
            logger: NullLogger<ZvtClient>.Instance,
            clientConfig: new ZvtClientConfig
            {
                Password = options.Password,
                Encoding = ZvtEncoding.CodePage437,
                Language = options.Language == CardCompleteLanguage.German ? Language.German : Language.English,
            });

        HookClientEvents();

        var regCfg = new RegistrationConfig
        {
            SendIntermediateStatusInformation = true,
            ActivateTlvSupport = true,
            ReceiptPrintoutGeneratedViaPaymentTerminal = true,
            AllowStartPaymentViaPaymentTerminal = false,
            AllowAdministrationViaPaymentTerminal = false
        };

        _trace?.Info("CMD 0600 Registration");
        var resp = await _client.RegistrationAsync(regCfg, ct);
        _trace?.Info($"Registration result: {resp.State} {resp.ErrorMessage}");

        if (resp.State != CommandResponseState.Successful)
            throw new InvalidOperationException($"Registration failed: {resp.State} {resp.ErrorMessage}");
    }

    public async Task<CardCompletePaymentResult> PurchaseAsync(decimal amount, CancellationToken ct = default)
    {
        EnsureReady();

        ResetBuffers();
        StartT4WatchdogForTransaction("purchase");

        _log.LogInformation("Purchase (06 01) amount={Amount}", amount);

        _trace?.Info("CMD 0601 Purchase");
        var resp = await _client!.PaymentAsync(amount, ct);

        StopT4Watchdog();

        if (resp.State == CommandResponseState.Successful)
        {
            return new CardCompletePaymentResult
            {
                Success = true,
                ReceiptNumber = _lastReceiptNumber,
                Receipts = _receipts.Snapshot()
            };
        }

        return new CardCompletePaymentResult
        {
            Success = false,
            ErrorMessage = string.IsNullOrWhiteSpace(resp.ErrorMessage) ? resp.State.ToString() : resp.ErrorMessage,
            ReceiptNumber = _lastReceiptNumber,
            Receipts = _receipts.Snapshot()
        };
    }

    public async Task<CardCompleteReceiptBundle> RepeatReceiptAsync(CancellationToken ct = default)
    {
        EnsureReady();

        ResetBuffers();
        StartT4WatchdogForTransaction("repeat_receipt");

        _log.LogInformation("RepeatReceipt (06 20)");

        _trace?.Info("CMD 0620 RepeatReceipt");

        var resp = await _client!.RepeatLastReceiptAsync(ct);

        StopT4Watchdog();

        if (resp.State != CommandResponseState.Successful)
            throw new InvalidOperationException($"RepeatReceipt failed: {resp.State} {resp.ErrorMessage}");

        return _receipts.Snapshot();
    }

    public async Task<CardCompleteReceiptBundle> EndOfDayAsync(CancellationToken ct = default)
    {
        EnsureReady();

        ResetBuffers();
        StartT4WatchdogForTransaction("end_of_day");

        _log.LogInformation("EndOfDay (06 50)");

        _trace?.Info("CMD 0650 EndOfDay");

        var resp = await _client!.EndOfDayAsync(ct);

        StopT4Watchdog();

        if (resp.State != CommandResponseState.Successful)
            throw new InvalidOperationException($"EndOfDay failed: {resp.State} {resp.ErrorMessage}");

        return _receipts.Snapshot();
    }

    public async Task AbortAsync(CancellationToken ct = default)
    {
        if (_client == null) return;

        _log.LogInformation("Abort (06 B0)");
        _trace?.Info("CMD 06B0 Abort");
        try
        {
            var r = await _client.AbortAsync(ct);
            _trace?.Info($"Abort result: {r.State} {r.ErrorMessage}");
        }
        catch (Exception ex)
        {
            _trace?.Error($"Abort exception: {ex.Message}");
            _log.LogWarning(ex, "Abort failed (ignored).");
        }
        finally
        {
            StopT4Watchdog();
        }
    }

    public void ResetBuffers()
    {
        _receipts.Reset();
        _lastReceiptNumber = 0;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    // -------------------------
    // Internal helpers
    // -------------------------

    private void EnsureConnected()
    {
        if (_device == null || !_device.IsConnected)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
    }

    private void EnsureReady()
    {
        EnsureConnected();
        if (_client == null)
            throw new InvalidOperationException("Not registered. Call LoginAsync first.");
    }

    private void HookClientEvents()
    {
        if (_client == null) return;

        _client.IntermediateStatusInformationReceived += OnIntermediateStatus;
        _client.StatusInformationReceived += OnStatusInformation;
        _client.LineReceived += OnLineReceived;
        _client.ReceiptReceived += OnReceiptReceived;
    }

    private void UnhookClientEvents()
    {
        if (_client == null) return;

        _client.IntermediateStatusInformationReceived -= OnIntermediateStatus;
        _client.StatusInformationReceived -= OnStatusInformation;
        _client.LineReceived -= OnLineReceived;
        _client.ReceiptReceived -= OnReceiptReceived;
    }

    private void StartT4WatchdogForTransaction(string kind)
    {
        if (_trace == null || string.IsNullOrWhiteSpace(_evidenceDir))
            StartEvidence(null);

        _trace?.Info($"=== Transaction started: {kind} ===");

        _t4?.Dispose();
        _t4 = new CardCompleteT4Watchdog(TimeSpan.FromMinutes(5), onTimeout: () =>
        {
            _trace?.Error("T4 watchdog timeout fired");
            _log.LogWarning("T4 watchdog timeout fired - terminal stopped responding");
        });

        _t4.Start();
    }

    private void StopT4Watchdog()
    {
        if (_t4 == null)
            return;

        _t4.Stop();
        _trace?.Info("=== Transaction finished ===");
    }

    // -------------------------
    // Logging
    // -------------------------

    public void StartEvidence(string? label = null)
    {
        var safeLabel = string.IsNullOrWhiteSpace(label)
            ? "session"
            : Sanitize(label.Trim());

        _evidenceDir = Path.Combine(
            EvidenceRootDirectory,
            $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeLabel}_{Guid.NewGuid():N}");

        Directory.CreateDirectory(_evidenceDir);

        _trace = new CardCompleteTraceLogger(_evidenceDir);

        _trace.Info($"EvidenceDir={_evidenceDir}");
        _trace.Info("=== Evidence started ===");
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    private void OnDeviceDataSent(byte[] data)
    {
        _trace?.Info($"TX LEN={data.Length}");
        _trace?.Hex("TX", data);
    }

    private void OnDeviceDataReceived(byte[] data)
    {
        _trace?.Info($"RX LEN={data.Length}");
        _trace?.Hex("RX", data);
    }

    // -------------------------
    // Event mapping
    // -------------------------

    private void OnIntermediateStatus(string message)
    {
        // restart timeout watchdog on every 04 FF
        _t4?.Reset();

        _trace?.Info($"04FF: {message}");
        IntermediateStatusInformationReceived?.Invoke(this, message);
    }

    private void OnStatusInformation(StatusInformation s)
    {
        // also reset timeout watchdog on status infos
        _t4?.Reset();

        if (s.ReceiptNumber > 0)
            _lastReceiptNumber = s.ReceiptNumber;

        _trace?.Info($"040F: ErrCode={s.ErrorCode} Receipt={s.ReceiptNumber} Amount={s.Amount} Card={s.CardName} Text={s.AdditionalText} ErrMsg={s.ErrorMessage}");

        var args = new TerminalStatusEventArgs(
            resultCode: s.ErrorCode,
            errorMessage: s.ErrorMessage,
            additionalText: s.AdditionalText,
            amount: s.Amount,
            cardName: s.CardName,
            receiptNumber: s.ReceiptNumber);

        StatusInformationReceived?.Invoke(this, args);

        var uiText =
            !string.IsNullOrWhiteSpace(s.AdditionalText) ? s.AdditionalText :
            !string.IsNullOrWhiteSpace(s.ErrorMessage) ? s.ErrorMessage :
            null;

        if (!string.IsNullOrWhiteSpace(uiText))
            PrintLineReceived?.Invoke(this, uiText);
    }

    private void OnLineReceived(PrintLineInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Text)) return;

        _trace?.Info($"06D1: {info.Text}");
        PrintLineReceived?.Invoke(this, info.Text);
    }

    private void OnReceiptReceived(ReceiptInfo info)
    {
        if (info == null) return;

        _trace?.Info($"Receipt: {info.ReceiptType} len={info.Content?.Length ?? 0}");

        if (info.ReceiptType == ReceiptType.Merchant)
            _receipts.SetMerchant(info.Content);
        else if (info.ReceiptType == ReceiptType.Cardholder)
            _receipts.SetCardholder(info.Content);

        var snap = _receipts.Snapshot();
        ReceiptUpdated?.Invoke(this, snap);
        SaveReceipts(snap);
    }

    private void SaveReceipts(CardCompleteReceiptBundle snap)
    {
        if (string.IsNullOrWhiteSpace(_evidenceDir)) return;

        var merchantPath = Path.Combine(_evidenceDir, "receipt_merchant.txt");
        var cardholderPath = Path.Combine(_evidenceDir, "receipt_cardholder.txt");

        if (!string.IsNullOrWhiteSpace(snap.MerchantReceipt))
            File.WriteAllText(merchantPath, snap.MerchantReceipt);

        if (!string.IsNullOrWhiteSpace(snap.CardholderReceipt))
            File.WriteAllText(cardholderPath, snap.CardholderReceipt);
    }
}
