using System;

namespace CalendarApp
{
    public sealed class ConsoleLogger : ILogger
    {
        public void Info(string message) => Console.WriteLine($"[INFO] {DateTimeOffset.UtcNow:o} {message}");
        public void Warn(string message) => Console.WriteLine($"[WARN] {DateTimeOffset.UtcNow:o} {message}");
        public void Error(string message, Exception? ex = null) => Console.WriteLine($"[ERROR] {DateTimeOffset.UtcNow:o} {message} {ex}");
    }
}
