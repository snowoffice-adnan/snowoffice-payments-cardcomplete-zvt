namespace Snowoffice.Payments.CardCompleteZvt.Models;

public sealed class CardCompleteConnectionOptions
{
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 20007;

    public bool EnableKeepAlive { get; set; } = true;

    /// <summary>
    /// Optional delay after TCP connect
    /// </summary>
    public TimeSpan PostConnectDelay { get; set; } = TimeSpan.FromMilliseconds(200);
}
