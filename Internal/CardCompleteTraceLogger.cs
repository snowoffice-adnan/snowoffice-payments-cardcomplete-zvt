using System.Text;

namespace Snowoffice.Payments.CardCompleteZvt.Internal;

internal sealed class CardCompleteTraceLogger
{
    private readonly string _filePath;
    private readonly object _gate = new();

    public CardCompleteTraceLogger(string directory, string fileName = "transport.log")
    {
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KIOSK", "cardcomplete", "evidence");

        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, fileName);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    public void Hex(string direction, byte[] data)
    {
        if (data == null) return;
        Write("HEX", $"{direction}: {BitConverter.ToString(data)}");
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {level} | {message}{Environment.NewLine}";
        lock (_gate)
        {
            File.AppendAllText(_filePath, line, Encoding.UTF8);
        }
    }
}
