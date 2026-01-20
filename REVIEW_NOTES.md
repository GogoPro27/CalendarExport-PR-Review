# Code Review Notes

After carefully reviewing the existing codebase, my understanding is that this PR attempts to introduce an API and service layer for exporting newly created events to Google Calendar. However, it took some time to piece this together, mainly because the implementation in this PR makes the overall flow harder to follow than necessary.

The main source of confusion is `CalendarScheduler.cs`, which contains a large amount of redundant logic. Much of the functionality implemented there already exists in the codebase and could be reused. For example, `GoogleCalendarExporter.cs` is clearly designed to handle exporting events to Google Calendar in a structured and safe way, yet `CalendarScheduler.cs` reimplements the same responsibility in a more error prone and unsafe way, instead of building on the existing solution.

In addition to that, the API layer itself does not perform any input validation and blindly indexes into the request body, which can easily lead to runtime exceptions and undefined behavior when malformed or incomplete input is provided. It also instantiates its dependencies directly and always returns a synchronous success response, making failures hard to detect and the code difficult to test or reason about.

Despite much of the implementation being redundant with existing code, the PR is still unsafe to merge in its current form due to several critical blockers.

The most significant risks are related to security (a hard coded Google access token that should never be committed), incorrect time and recurrence formatting, unreliable http behavior, and a complete lack of observability and proper error handling.

Besides that, the overall design does not follow fundamental software design principles, particularly the SOLID principles. Responsibilities are tightly coupled, dependencies are not injected, and the code is structured in a way that makes extension, testing, and maintenance unnecessarily difficult.

**Must fix before merging:** security issues, correct handling of time and recurrence, proper async and HTTP behavior, and making sure errors are not silently ignored. These are the minimum changes needed so the code actually works as expected, is safe to run, and does not fail in hidden or unpredictable ways.

**Follow-ups:** general design cleanup, better separation of responsibilities, dependency injection, input validation, and broader test coverage. These are not strictly required to get the feature working, but they are important to make the code easier to maintain, test, and extend in the future.

**This PR should not be deployed until all must-fix items are addressed and verified.**

## Minimal must fix issues

### `src/CalendarApp/CalendarScheduler.cs:12` -> shared static mutable state
- This is not thread safe, stores untyped objects and leaks state across requests.
- If our goal is to have an in-memory storage, I would suggest moving it to an instance field and introduce a lock, so writes are guarded.

### `src/CalendarApp/CalendarScheduler.cs:14` -> Hard coded Google OAuth access token
- The secret must never be committed to git and it should be injected through configuration. This can be done by having a `.env` file where the access token will be set as a variable.

### `src/CalendarApp/CalendarScheduler.cs:21` -> `DateTime.Parse(start)`
- This way the parsing defaults to local time and it doesn't consider other time zones which means that it interprets dates differently based on server locale (non deterministic) - big problem when it comes to scheduling events in a calendar that others might attend to as well :)

Proposed fix:
```csharp
DateTimeOffset eventStart = DateTimeOffset.ParseExact(
    start,
    "yyyy-MM-ddTHH:mm:ssK", // ISO-8601 with time zone
    CultureInfo.InvariantCulture,
    DateTimeStyles.RoundtripKind
);
```

### `src/CalendarApp/CalendarScheduler.cs:33-34` -> Google Calendar format violation
- `.ToString("s")` is not RFC3339. It should be: `.ToString("o")`

### `src/CalendarApp/CalendarScheduler.cs:27` -> Recurrence rule construction
- The `rule["freq"]` accepts raw strings with no validation.
- The recurrence must be validated first so no different enumerations go through than what Google accepts. If it is not any of those, it should throw an exception.

### `src/CalendarApp/CalendarScheduler.cs:35` -> `recurrence != "none"` is case sensitive
- After the validation has been done, this variable should be stored in an enumerated variable and used like that.

### `src/CalendarApp/CalendarScheduler.cs:55` -> sync http call
- `http.send(req)` blocks threads and ignores cancellation, async/await must be used.

### `src/CalendarApp/CalendarScheduler.cs:53-64` -> errors swallowed
- All exceptions are caught and ignored. This makes it harder for testing and really bad for user experience if something goes wrong.
- Method always returns "ok" even on failure, which additionally brakes observability. That return statement should be brought at the end of the try block.
- 400/401/500 errors from Google should be treated differently

### `src/CalendarApp/Api.cs:11-22` -> unchecked dictionary access
- Blind indexing (`body["..."]`) can throw at runtime, validation, error responses and null/format checks should be added.

## Follow-up issues (non blocking but also important)

For this section I'd start from the API layer and work my way down.

### `src/CalendarApp/Api.cs:9` -> Use a proper request object instead of a raw dictionary
In a real .NET API, I'd accept a strongly typed object (for example a `CalendarEvent`, or a small request DTO) and let the framework map the JSON body into it. That way we can also validate it properly and return a clear error message if the request is missing fields or has wrong formats.

### `src/CalendarApp/Api.cs:11` -> Don't create `new CalendarScheduler()` inside the API
I'd rely on dependency injection and inject the scheduler into the API layer. I'd also introduce an interface (like `ICalendarScheduler`) so later we can swap implementations more easily (different environments, different behavior, etc.).

### `src/CalendarApp/Api.cs:13` -> Don't pass every field as a separate parameter
Once the API receives a typed object, I'd pass that object down to the scheduler, instead of extracting strings and passing them one by one. It makes the call simpler and removes a lot of room for mistakes.

---

### `src/CalendarApp/CalendarScheduler.cs:11` -> Don't duplicate Google export logic here
This class currently repeats work that already exists in `GoogleCalendarExporter.cs`. I'd keep the scheduler focused on "creating/saving the event" and let exporting be handled by the exporter through the interface:

```csharp
private readonly ICalendarExporter _exporter;
```

This also keeps the door open for other exporters later (not only Google).

### `src/CalendarApp/CalendarScheduler.cs:12` -> Move "saving events" out into a small storage/repository
Instead of keeping the in-memory list inside the scheduler, I'd create a tiny "repository" layer responsible for storing events, with an interface so we can later plug in a real database if needed. And yes, it should still be protected with a lock since it's shared state.

---

### `src/CalendarApp/CalendarScheduler.cs:13` -> Create a dedicated `GoogleCalendarClient` and hide HTTP details there
Instead of building raw requests in the exporter, I'd extract that into a client class so the exporter just does:
```csharp
await _client.CreateEventAsync(payload, ct);
```

Also, the URL string can be stored in a clearly named variable to make it easier to read, and logging that's specific to "talking to Google" can live inside the client.
This makes it easy for adding retries and backoff later at the client level as well.

### `src/CalendarApp/CalendarScheduler.cs:15` -> `CalendarId` shouldn't be a static hidden setting
Even if "primary" is the default, it shouldn't be hardcoded as global state. I'd inject it from configuration (same as the token), so later we can support multiple calendars or per-user calendars.

### `src/CalendarApp/CalendarScheduler.cs:20-64` -> Replace most of this with exporter + repository calls
Once responsibilities are split properly, the scheduler becomes much simpler: create/validate the event, store it, export it.

```csharp
await _exporter.ExportAsync(ev, ct);
_eventRepository.Save(ev);
```

This also means the exporter receives its dependencies (client + token + calendar id + logger) through injection, instead of pulling them from static fields.

---

### Small improvements after moving export responsibility fully into `GoogleCalendarExporter`

#### `src/CalendarApp/GoogleCalendarExporter.cs:35-48` -> replaced by:
```csharp
await _client.CreateEventAsync(payload, ct);
```

#### `src/CalendarApp/GoogleCalendarExporter.cs:26` -> Add basic validation
Even small guard checks (title present, duration valid, etc.) would help prevent sending garbage to Google.

#### `src/CalendarApp/GoogleCalendarExporter.cs:27` -> Consider a small DTO instead of anonymous payload
Not a must fix, but it makes testing and future changes easier.

---

## Cleanup
- `src/CalendarApp/ConsoleLogger.cs` -> remove unused imports
- `src/CalendarApp/GoogleCalendarExporter.cs` -> remove unused imports
- `src/CalendarApp/EventExpander.cs` -> remove unused imports
- `src/CalendarApp/Interfaces.cs` -> remove unused imports

## Tests

There are already some basic tests in place under `CalendarApp.Tests`, mainly covering recurrence expansion logic in `EventExpander`. These tests are a good starting point and help verify that simple daily and monthly recurrence cases behave as expected.

That said, given the scope of this PR, additional tests should be added to cover areas that are currently untested but critical:
- **Time handling and time zones** – tests for non-UTC offsets and DST boundaries, since calendar behavior is very sensitive to these cases.
- **Recurrence validation** – tests for invalid or unsupported recurrence values, case-insensitive inputs, and edge cases like using both Count and Until.
- **Error handling** – tests that verify failures from the Google Calendar API are not swallowed and are properly surfaced.
- **API input validation** – once the API layer accepts a typed request object, tests should verify that missing or malformed input is rejected early.
