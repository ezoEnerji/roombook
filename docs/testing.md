# Testing

> Filled at bootstrap.

## The contract
- Every acceptance criterion maps to at least one test (criterion ↔ test map lives in the plan).
- Tests assert **behavior**, not implementation details or mere status codes.
- The whole suite runs inside `scripts/check` — one command, everywhere.

## Frameworks & layout

- **xUnit v3**. Assertions are xUnit's built-ins; FluentAssertions is a banned dependency (FD-6).
- `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) supplies time in every test.
- HTTP tests use `WebApplicationFactory` against the real composition root with the in-memory adapter.

| Project | Covers |
|---|---|
| `tests/RoomBook.Domain.Tests` | Business rules BR-1…BR-9, time-slot algebra, conflict detection, availability search. The bulk of the suite. |
| `tests/RoomBook.Api.Tests` | The HTTP contract end to end: routes, DTO shapes, status codes, `ProblemDetails` bodies and `code` values. |
| `tests/RoomBook.Architecture.Tests` | Forbidden dependencies FD-1…FD-6. |

The weight sits in the domain because that is where the rules are; HTTP tests prove the contract, not
the rules. Naming follows `Method_Scenario_ExpectedResult` (`docs/conventions.md`).

## What must be tested

- **Every business rule** BR-1…BR-9: at least one accepting case and one rejecting case each, with the
  rejecting case asserting the specific error code from the mapping table in `docs/conventions.md`.
- **Boundary cases of the interval algebra**: touching endpoints (BR-3 — must be accepted), exact
  15-minute and 4-hour durations (BR-4 — accepted), one minute either side (rejected), the 90-day edge
  (BR-7), and a booking that would cross the end of business hours (BR-1).
- **Time-zone behaviour**: a room whose business hours are evaluated in a non-UTC zone, including a
  daylight-saving transition day.
- **Availability search**: returned slots align to the 15-minute grid, are ordered earliest-first, and
  never include a slot that a subsequent create would reject.
- **Forbidden dependencies** FD-1…FD-6, as executable assertions.
- **The HTTP contract**: each error code maps to its documented status; unknown JSON members are
  rejected; timestamps are serialised as ISO-8601 `Z`.

"Sufficiently tested" is defined by this list, not by a coverage percentage: a rule without an
accepting *and* a rejecting test is not done.

## Protected-tests rule
Weakening asserts, deleting, or skipping tests to reach green is forbidden. A red test triggers
`prompts/recovery/red-test.md` (R-02) — first decide what is wrong: code, test, or spec.

## Determinism

- Time comes from an injected `TimeProvider`; a test that depends on the real clock is forbidden (FD-3).
- Every test builds its own fresh in-memory repositories. No state is shared between tests.
- `Thread.Sleep` and real waiting are forbidden.
- Test data is explicit and fixed — no randomised or generated inputs.
- Flaky tests are quarantined and fixed immediately, never hidden behind skip/ignore — see R-03.
  Evidence of a fix: 5 consecutive green runs.
