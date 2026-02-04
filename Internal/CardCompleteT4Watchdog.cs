namespace Snowoffice.Payments.CardCompleteZvt.Internal;

internal sealed class CardCompleteT4Watchdog : IDisposable
{
    private readonly TimeSpan _timeout;
    private readonly Action _onTimeout;
    private Timer? _timer;
    private int _fired;

    public CardCompleteT4Watchdog(TimeSpan timeout, Action onTimeout)
    {
        _timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : timeout;
        _onTimeout = onTimeout ?? throw new ArgumentNullException(nameof(onTimeout));
    }

    public void Start()
    {
        Interlocked.Exchange(ref _fired, 0);
        Reset();
    }

    /// <summary>
    /// Reset timeout (call this on every IntermediateStatusInformationReceived / 04 FF).
    /// </summary>
    public void Reset()
    {
        _timer?.Dispose();
        _timer = new Timer(_ =>
        {
            if (Interlocked.Exchange(ref _fired, 1) == 1)
                return;

            _onTimeout();
        }, null, _timeout, Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();
}
