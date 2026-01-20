using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CalendarApp
{
    public enum RecurrenceKind { None, Daily, Weekly, Monthly, Yearly }

    public sealed record ScheduleRule(
        RecurrenceKind Kind,
        int Interval = 1,
        int? Count = null,
        DateTimeOffset? Until = null
    );

    public sealed record CalendarEvent(
        string Title,
        DateTimeOffset Start,
        TimeSpan Duration,
        ScheduleRule Rule
    );

    public interface IEventExpander
    {
        IEnumerable<(DateTimeOffset start, DateTimeOffset end)> Expand(CalendarEvent ev, DateTimeOffset windowStart, DateTimeOffset windowEnd);
    }

    public interface ICalendarExporter
    {
        Task ExportAsync(CalendarEvent ev, CancellationToken ct = default);
    }

    public interface ILogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception? ex = null);
    }
}
