# Testing

> Filled at bootstrap.

## The contract
- Every acceptance criterion maps to at least one test (criterion ↔ test map lives in the plan).
- Tests assert **behavior**, not implementation details or mere status codes.
- The whole suite runs inside `scripts/check` — one command, everywhere. (S-001 is what makes this
  true: it creates the projects and uncomments the steps in `scripts/check.conf`.)

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
the rules. Naming follows `Method_Scenario_ExpectedResult` (`docs/conventions.md`). All three projects
are created by S-001, which is also where `scripts/check` stops being a no-op and starts running them.

## What must be tested

- **Every rejection rule** (BR-1, BR-2, BR-4, BR-6, BR-7, BR-8, BR-9): at least one accepting and one
  rejecting case each, with the rejecting case asserting the specific error code from the mapping table
  in `docs/conventions.md`.
- **BR-3 and BR-5 are tested differently**, because neither rejects anything. BR-3 needs an *accepting*
  test — a booking starting exactly when another ends is created successfully — and its neighbouring
  rejection is already covered by BR-2's overlap test. BR-5 is tested at the API edge: a malformed,
  missing or non-UTC timestamp comes back as `request.invalid`, and responses serialise as ISO-8601 `Z`.
- **The decided boundaries**, each one accepting or rejecting with no room for interpretation:

| Case | Expected |
|---|---|
| Booking ends exactly at closing time (17:00–18:00, close 18:00) | accepted (BR-1) |
| Booking starts exactly at closing time | rejected (BR-1) |
| Booking starts exactly when another in the same room ends | accepted (BR-3) |
| Duration exactly 15 minutes / exactly 4 hours | accepted (BR-4) |
| Duration 14 minutes / 4 hours 1 minute | rejected (BR-4) |
| `start == now` | rejected (BR-8) |
| `start == now + 90 days` | accepted (BR-7) |
| `start == now + 90 days + 1 minute` | rejected (BR-7) |
| Cancel exactly at the booking's start | rejected (BR-9) |
| Cancel the same booking twice | second call is `booking.not_found` |
| Create at `10:07` with a valid duration | accepted — no grid alignment on create |
- **Time-zone behaviour**: a room whose business hours are evaluated in a non-UTC zone, including a
  daylight-saving transition day.
- **Availability search**: returned slots align to the 15-minute grid, are ordered earliest-first, and
  never include a slot that a subsequent create would reject.
- **Forbidden dependencies** FD-1…FD-6, as executable assertions.
- **The HTTP contract**: each error code maps to its documented status; a successful create returns
  `201` with a `Location` header, a cancel returns `204`, reads return `200` with an empty array when
  nothing matches; unknown JSON members are rejected; timestamps are serialised as ISO-8601 `Z`.

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
