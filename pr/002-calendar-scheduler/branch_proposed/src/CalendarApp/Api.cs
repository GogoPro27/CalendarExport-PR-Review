// Fake API controller with no validation, no DI, no async, no status handling
using System;
using System.Collections.Generic;

namespace CalendarApp
{
    public class Api // not ASP.NET, just placeholder
    {
        public string PostSchedule(Dictionary<string, string> body)
        {
            var sched = new CalendarScheduler();
            // blindly index into body, no checks
            return sched.Schedule(
                body["title"],
                body["start"],
                body["durationMinutes"],
                body["recurrence"],
                body.ContainsKey("count") ? body["count"] : "",
                body.ContainsKey("until") ? body["until"] : ""
            );
        }
    }
}
