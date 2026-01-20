using System;
using System.Linq;
using CalendarApp;
using Xunit;

namespace CalendarApp.Tests
{
    public class ExpansionTests
    {
        [Fact]
        public void Expands_Daily_With_Count()
        {
            var ev = new CalendarEvent(
                "Standup",
                new DateTimeOffset(2025, 1, 1, 9, 0, 0, TimeSpan.FromHours(1)),
                TimeSpan.FromMinutes(15),
                new ScheduleRule(RecurrenceKind.Daily, Interval: 1, Count: 3)
            );
            var expander = new EventExpander();
            var items = expander.Expand(ev, ev.Start, ev.Start.AddDays(10)).ToList();
            Assert.Equal(3, items.Count);
            Assert.Equal(ev.Start.AddDays(2), items.Last().start);
        }

        [Fact]
        public void Expands_Monthly_Until()
        {
            var start = new DateTimeOffset(2025, 1, 31, 10, 0, 0, TimeSpan.Zero);
            var ev = new CalendarEvent(
                "Billing",
                start,
                TimeSpan.FromHours(1),
                new ScheduleRule(RecurrenceKind.Monthly, Interval: 1, Until: start.AddMonths(3))
            );
            var expander = new EventExpander();
            var items = expander.Expand(ev, start, start.AddMonths(6)).ToList();
            Assert.True(items.Count >= 3);
        }
    }
}
