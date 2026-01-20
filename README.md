# PR Review Challenge – C# Calendar Scheduler + Google Calendar (45–60 min)

**Scenario:** A teammate opened a PR that adds a calendar scheduler which exports events to **Google Calendar**.

Your task: **Review the PR** and provide feedback before merge.

## What you do
1. Open the PR diff at `pr/002-calendar-scheduler/patch.diff` or compare `src/CalendarApp/` with `pr/002-calendar-scheduler/branch_proposed/src/CalendarApp/`.
2. Write your review in `REVIEW_NOTES.md`:
   - Start with a short **summary**: key risks, must-fix vs follow-ups, and a safe rollout plan.
   - Add **comments** like `src/CalendarApp/CalendarScheduler.cs:87 → <comment>`.
   - Propose a patch or test to clarify a point.

⏱️ **Timebox:** 45–60 minutes. Prioritize the biggest risks.

## What we score (0–3 each, /15 total)
- Correctness and edge cases (recurrence, time zones)
- Reliability and failure modes (HTTP, retries, backoff)
- Security and compliance (secrets, PII, tokens)
- Observability and testing (metrics, logs, test cases)
- Communication and prioritization (must-fix vs follow-up)

## Run tests
```bash
dotnet restore
dotnet test -v minimal
```
Visible tests cover expansion basics; our CI adds hidden tests (ex: timezone boundaries, RRule correctness).

## Deliverable
Commit `REVIEW_NOTES.md` (and any fix). Send a patch or repo link.