# Conventions

> Filled at bootstrap. Every rule here is either enforced by tooling (preferred) or checked in
> review. Aspirations don't belong here.

## Language & framework versions

- C# on **.NET 9** (`net9.0`). Retargeting requires an ADR.
- ASP.NET Core **Minimal API** (no MVC controllers). `System.Text.Json` for serialisation.
- **xUnit v3** for tests; `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) for time.
- Shared build settings live in `Directory.Build.props` (created by S-001): `Nullable=enable`,
  `TreatWarningsAsErrors=true`, `LangVersion=latest`.
- Package versions are pinned. Floating versions (`*`, `9.*`) are forbidden.

## Naming

- Projects `RoomBook.<Layer>`, test projects `RoomBook.<Layer>.Tests`, solution `RoomBook.sln`.
- One top-level type per file; file name equals type name.
- Types and members `PascalCase`; parameters and locals `camelCase`; private fields `_camelCase`.
- Asynchronous methods end in `Async`. Storage ports are named `I<Thing>Repository`.
- API DTOs are records named `<UseCase>Request` / `<UseCase>Response` (e.g. `CreateBookingRequest`).
- Tests: `Method_Scenario_ExpectedResult` — e.g. `Create_WhenOverlappingSameRoom_ReturnsConflict`.
- Branches and commits: see `docs/git.md`.
- The vocabulary in `docs/domain.md` is binding: never introduce a synonym for a domain term in code.

## Error handling

The one blessed pattern: **rule violations are values, not exceptions.**

- `Domain` and `Application` return `Result` / `Result<T>` carrying a stable error code and a
  human-readable message. A business rule never throws.
- Exceptions are reserved for programmer errors and infrastructure failures. They are logged and
  surfaced as `500` with no internal detail.
- The API adapter translates error codes into **RFC 9457 `ProblemDetails`** with an extra `code` member.
- One code, one status — no case-by-case decisions:

| Code | Rule | Status |
|---|---|---|
| `request.invalid` | malformed body, unknown JSON member, field limit violation | 400 |
| `room.not_found` / `booking.not_found` | unknown identifier | 404 |
| `booking.overlap` | BR-2 | 409 |
| `booking.cancel_after_start` | BR-9 | 409 |
| `booking.outside_business_hours` | BR-1 | 422 |
| `booking.duration_out_of_range` | BR-4 | 422 |
| `booking.attendees_exceed_capacity` | BR-6 | 422 |
| `booking.too_far_in_future` | BR-7 | 422 |
| `booking.start_in_past` | BR-8 | 422 |

Two rules are deliberately absent from the table. **BR-3** permits back-to-back bookings, so it has
nothing to reject — its counterpart in the table is `booking.overlap` (BR-2). **BR-5** is a format
contract: a missing, malformed or non-UTC timestamp is `request.invalid` at the API edge. In the same
way, `attendeeCount < 1` is a field-limit violation (`request.invalid`), while exceeding the room's
capacity is the domain rule BR-6. Cancelling an already-cancelled booking is `booking.not_found`.

- Never leaked to clients: stack traces, exception type names, configuration values, or the organizer
  name of somebody else's booking.

## Data rules

- Identifiers: `Guid` created with `Guid.CreateVersion7()` (time-ordered). Never sequential integers.
- Timestamps: `DateTimeOffset` in UTC everywhere; ISO-8601 with `Z` on the wire (BR-5).
  Durations: `TimeSpan`.
- "Now" is owned by the `Application` layer through an injected `TimeProvider` and handed to the domain
  as a value; reading `DateTime.UtcNow` anywhere is a forbidden dependency (FD-3).
- Business-hour arithmetic converts UTC into the room's IANA time zone. UTC is storage and transport
  only; local time is a calculation, never a stored value.
- JSON: `camelCase` members; unknown members are rejected (`JsonUnmappedMemberHandling.Disallow`).
- Nullable reference types are enabled; domain types express absence explicitly, not with `null`.
- Domain value types and DTOs are immutable `record`s; collections are exposed read-only.
- Field limits: `title` ≤ 200 characters, `organizer` ≤ 100 characters, `attendeeCount` ≥ 1,
  `room.capacity` ≥ 1, availability queries span at most 31 days, request bodies at most 32 KB.
- The 15-minute grid belongs to availability search results only. Booking times are never rounded,
  snapped or realigned — `10:07` is a legitimate start (`docs/domain.md`, explicit non-rules).

## Enforced by tooling

- Compiler: nullable analysis plus `TreatWarningsAsErrors` (the `build` step of `scripts/check`).
- `dotnet format --verify-no-changes` with `.editorconfig` (both created by S-001) — style is never a
  review topic.
- Architecture tests assert FD-1…FD-6, including the banned-package and ambient-clock rules.
- Every rejection rule carries an accepting and a rejecting test; BR-3 and BR-5 are tested differently
  (`docs/testing.md`).
- `dotnet list package --vulnerable` runs in CI.

The three items above that need a test project or a formatter config become live when S-001 uncomments
the steps in `scripts/check.conf`. Until that lands, `scripts/check` is a green no-op and these rules
bind review only — which is exactly why no code may merge before S-001.
