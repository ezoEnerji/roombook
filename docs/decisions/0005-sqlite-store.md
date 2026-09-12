# ADR 0005 — SQLite as the store, and two amendments to ADR-0001

- Status: Accepted
- Date: 2026-09-12

## Context

Until this slice, everything lived in a dictionary: stopping the process threw away every reservation
in the office. ADR-0001 argued that replacing that dictionary with a database would be one adapter's
work and would touch no rule — and until somebody did it, that was an argument rather than a fact.

Two constraints shaped the choice. `docs/security.md` requires an ADR for any dependency, and FD-6 in
`docs/architecture.md` bans EF Core and any ORM "in V1". Acceptance criterion AC-8 of the spec
requires `./scripts/check` to pass with no database service installed, which rules out anything that
expects a server to be running.

## Decision

**SQLite through `Microsoft.Data.Sqlite` (9.0.20, pinned), with hand-written SQL.** The store lives in
a new `src/RoomBook.Infrastructure` project — the move `docs/architecture.md` promised when a database
arrived. FD-6's ban on ORMs stays exactly as written: a driver is not an ORM, and no mapping layer was
added.

Instants are stored as UTC ticks and opening hours as minutes from midnight. Neither reaches the wire,
and both compare exactly; text would only compare correctly while every value happened to be the same
fixed-width UTC format.

The two invariants that cannot be judged from a request alone are enforced by **conditional writes**:
the insert runs only if nothing in that room overlaps (BR-2), and the delete only if the booking has
not started (BR-9). The rules still live in the domain; the SQL conditions exist so that no clock and
no second caller can overtake the answer between deciding and writing.

## Consequences

What it buys: a booking survives a restart, which is the first thing a user would have complained
about. The no-overlap guarantee moves from a lock inside one process to the database — the only place
it can live once more than one process serves the API. The cancellation gap that
`docs/architecture.md` recorded as a known limit is closed. And `./scripts/check` still needs nothing
installed, so the verification contract did not get more expensive.

What it costs: hand-written SQL is more code than an ORM would be, and the schema now has to be kept
in step with the domain by hand. SQLite serialises writers, so the concurrency tests take longer than
they did against a dictionary. There is no migration tooling: the schema is created if absent, which
is enough for one schema version and will need a real answer the first time a column changes.

## The two amendments to ADR-0001

ADR-0001 claimed that swapping the store would be "one new adapter plus one DI line, with zero changes
to domain code". Measured against `main`, the claim held for every business rule, every endpoint and
all 201 existing tests — none of which changed. It failed in exactly two structural places:

1. **The domain needed a way back in.** `Booking.Create` judges the rules against "now", so using it
   to rebuild a stored booking would reject every booking the moment it starts. `Booking.Rehydrate`
   exists so a store can hand a value back without re-judging history. Persistence ignorance means
   the domain knows nothing about the store; it does not mean the store can reconstruct a value
   without being given an entry point.
   <br>Two alternatives existed and both were worse: calling `Create` with a fabricated instant — say
   one minute before the booking starts — would "validate" stored history against a time that never
   happened and would hide a row that genuinely broke a rule; and an internal constructor with
   `InternalsVisibleTo` would couple the domain to an adapter by name. So the honest phrasing is that
   the domain needed *an* entry point and this is the one we chose, not that no other mechanism could
   have been made to work.
2. **The port had to learn about time.** Making BR-2 atomic needed nothing new — an overlap is judged
   from the candidate alone. Making BR-9 atomic needed the current instant inside the store, so
   `RemoveAsync` gained a `nowUtc` parameter. The alternative was an adapter reading the clock itself,
   which FD-3 forbids for good reasons.

Both are recorded here rather than smoothed over, because a claim that survives every test was never
a claim worth making. ADR-0001's reasoning was sound and its scope was slightly too confident.

## Alternatives considered

- **EF Core with SQLite.** Migrations and less SQL, but it would have meant lifting FD-6's ORM ban on
  the same day it was first tested, and pulling in a large dependency to avoid writing six queries.
  Worth revisiting if the schema starts changing often.
- **PostgreSQL with Npgsql.** Closest to a production deployment, and the only option with a real
  exclusion constraint — which could enforce BR-2 declaratively instead of with a conditional insert.
  It lost on AC-8: CI and every developer machine would need a running service, and the verification
  contract would stop being a single command.
- **Keeping the in-memory adapter alongside the new one.** Rejected: two stores mean two behaviours to
  keep in step, and the tests would then prove the wrong one.
- **`SERIALIZABLE` transactions with read-then-write.** Correct, but it trades conditional writes for
  retry handling, and SQLite's single-writer model makes the conditional form both simpler and enough.

## Revisit triggers

- The schema changes: that is when migration tooling stops being optional.
- More than one process serves the API, or the store moves off the same machine — at which point
  SQLite's single-writer model becomes the bottleneck rather than a convenience.
- A rule needs to be expressed declaratively in the database (an exclusion constraint for BR-2), which
  is the strongest argument for PostgreSQL.
- Hand-written SQL starts being copied between repositories rather than written once.
