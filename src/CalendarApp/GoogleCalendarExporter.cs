using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CalendarApp
{
    public sealed class GoogleCalendarExporter : ICalendarExporter
    {
        private readonly HttpClient _http;
        private readonly string _accessToken;
        private readonly string _calendarId;
        private readonly ILogger _logger;

        public GoogleCalendarExporter(HttpClient http, string accessToken, string calendarId, ILogger logger)
        {
            _http = http;
            _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
            _calendarId = calendarId ?? "primary";
            _logger = logger;
        }

        public async Task ExportAsync(CalendarEvent ev, CancellationToken ct = default)
        {
            var payload = new
            {
                summary = ev.Title,
                start = new { dateTime = ev.Start.ToString("o") },
                end = new { dateTime = ev.Start.Add(ev.Duration).ToString("o") },
                recurrence = BuildRRule(ev)
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(_calendarId)}/events");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            req.Content = JsonContent.Create(payload, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            _logger.Info($"Exporting event '{ev.Title}' at {ev.Start}");

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.Error($"Google Calendar API error: {(int)resp.StatusCode} {resp.ReasonPhrase} – {body}");
                throw new HttpRequestException($"Failed to export event: {(int)resp.StatusCode}");
            }
        }

        private static string[]? BuildRRule(CalendarEvent ev)
        {
            if (ev.Rule.Kind == RecurrenceKind.None) return null;
            var r = ev.Rule;
            var freq = r.Kind switch
            {
                RecurrenceKind.Daily => "DAILY",
                RecurrenceKind.Weekly => "WEEKLY",
                RecurrenceKind.Monthly => "MONTHLY",
                RecurrenceKind.Yearly => "YEARLY",
                _ => "DAILY"
            };
            var parts = $"FREQ={freq};INTERVAL={Math.Max(r.Interval,1)}";
            if (r.Count is int c) parts += $";COUNT={c}";
            if (r.Until is DateTimeOffset u) parts += $";UNTIL={u.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}";
            return new[] { "RRULE:" + parts };
        }
    }
}
