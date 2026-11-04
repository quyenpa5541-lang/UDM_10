using System;
using System.IO;

namespace Server;

public sealed class FileLogger
{
    private readonly string _logDirectory;
    private readonly object _lock = new();

    public FileLogger(string? logDirectory = null)
    {
        _logDirectory = logDirectory ??
            Path.Combine(AppContext.BaseDirectory, "logs");

        Directory.CreateDirectory(_logDirectory);
    }

    public string Write(string level, string message)
    {
        string timestamp = DateTime.Now.ToString(
            "yyyy-MM-dd HH:mm:ss");

        string logLine =
            $"[{timestamp}] [{level}] {message}";

        string filePath = Path.Combine(
            _logDirectory,
            $"server-{DateTime.Now:yyyyMMdd}.log");

        lock (_lock)
        {
            File.AppendAllText(
                filePath,
                logLine + Environment.NewLine);
        }

        return logLine;
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message)
    {
        Write("ERROR", message);
    }

    public void Warning(string message)
    {
        Write("WARNING", message);
    }
}
