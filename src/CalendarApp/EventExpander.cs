using System;
using System.Collections.Generic;

namespace CalendarApp
{
    public sealed class EventExpander : IEventExpander
    {
        public IEnumerable<(DateTimeOffset start, DateTimeOffset end)> Expand(CalendarEvent ev, DateTimeOffset windowStart, DateTimeOffset windowEnd)
        {
            var rule = ev.Rule;
            var occurrenceStart = ev.Start;
            var duration = ev.Duration;
            int emitted = 0;

            bool InWindow(DateTimeOffset s) => s <= windowEnd && (s + duration) >= windowStart;

            if (rule.Kind == RecurrenceKind.None)
            {
                if (InWindow(occurrenceStart))
                    yield return (occurrenceStart, occurrenceStart + duration);
                yield break;
            }

            while (true)
            {
                if (rule.Count.HasValue && emitted >= rule.Count.Value) yield break;
                if (rule.Until.HasValue && occurrenceStart > rule.Until.Value) yield break;

                if (InWindow(occurrenceStart))
                {
                    yield return (occurrenceStart, occurrenceStart + duration);
                    emitted++;
                }

                occurrenceStart = Next(occurrenceStart, rule);
                if (occurrenceStart > windowEnd && !rule.Count.HasValue && !rule.Until.HasValue) yield break;
            }
        }

        private static DateTimeOffset Next(DateTimeOffset dt, ScheduleRule r) => r.Kind switch
        {
            RecurrenceKind.Daily => dt.AddDays(r.Interval),
            RecurrenceKind.Weekly => dt.AddDays(7 * r.Interval),
            RecurrenceKind.Monthly => dt.AddMonths(r.Interval),
            RecurrenceKind.Yearly => dt.AddYears(r.Interval),
            _ => dt
        };
    }
}
