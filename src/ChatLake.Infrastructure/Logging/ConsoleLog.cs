namespace ChatLake.Infrastructure.Logging;

/// <summary>
/// Simple console logging helper with timestamps.
/// </summary>
public static class ConsoleLog
{
    public static void Info(string category, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{category}] {message}");
    }

    public static void Warn(string category, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{category}] ⚠ {message}");
    }

    public static void Error(string category, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{category}] ✗ {message}");
    }

    public static void Success(string category, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{category}] ✓ {message}");
    }

    public static void Progress(string category, int current, int total, string? suffix = null)
    {
        var pct = total > 0 ? (current * 100 / total) : 0;
        var msg = suffix != null
            ? $"Progress: {current}/{total} ({pct}%) - {suffix}"
            : $"Progress: {current}/{total} ({pct}%)";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{category}] {msg}");
    }
}
