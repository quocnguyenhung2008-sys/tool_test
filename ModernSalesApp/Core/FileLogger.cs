using System.Text;
using System.IO;

namespace ModernSalesApp.Core;

public sealed class FileLogger : ILogger
{
    private readonly object _lock = new();
    private readonly string _logFilePath;

    public FileLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    public void Info(string message)
    {
        Write("INFO", message, null);
    }

    public void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message, ex);
    }

    private void Write(string level, string message, Exception? ex)
    {
        var sb = new StringBuilder();
        sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
        sb.Append(" ");
        sb.Append(level);
        sb.Append(" ");
        sb.Append(message);
        if (ex != null)
        {
            sb.AppendLine();
            sb.Append(ex.ToString());
        }
        sb.AppendLine();

        lock (_lock)
        {
            File.AppendAllText(_logFilePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
