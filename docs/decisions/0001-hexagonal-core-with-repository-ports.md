# ADR 0001 — Lightweight hexagonal core with repository ports and an in-memory V1 adapter

- Status: Accepted
- Date: 2026-09-12

## Context

RoomBook's value is concentrated in two algorithms: detecting conflicts between bookings in the same
room, and searching for free slots that fit a requested duration. Both are pure computations over
intervals, business hours and time zones, and both are where the bugs will be — off-by-one interval
comparisons, UTC/local confusion, daylight-saving edges.

V1 deliberately stores everything in memory: no database, no migrations, no infrastructure. A database
is expected later, and it must not force the rules to be rewritten. At the same time the project is
small enough that heavy layering would cost more than it returns.

## Decision

We structure the API as a modular monolith in a lightweight hexagonal shape: `RoomBook.Domain` holds
the model and all rules with no I/O and no NuGet packages, `RoomBook.Application` holds use cases that
reach storage only through `IBookingRepository` / `IRoomRepository` ports, and `RoomBook.Api` is the
HTTP adapter and composition root. V1's in-memory implementations of those ports live inside the API
project and move to their own `RoomBook.Infrastructure` project when a real database arrives.

Direction is enforced twice: by project references (a reverse reference cannot compile) and by
architecture tests for the rules the compiler cannot see (FD-1…FD-6 in `docs/architecture.md`).

## Consequences

What it buys: the rules are tested in milliseconds without a host or a database, so the accepting and
rejecting test per business rule is cheap enough to be non-negotiable. Swapping in a database becomes
one new adapter plus one DI line, with zero changes to domain code. Boundaries are machine-checked, so
review time goes to behavior instead of layering arguments.

What it costs: three projects and a solution instead of one file — more ceremony for the first
endpoint. Ports add an indirection that is pure overhead while the only implementation is a dictionary.
Some data-access optimisations a database would offer (query-level filtering, indexes) cannot be
expressed through the current port shape and will require the ports to be revisited.

## Alternatives considered

- **Single project, folders only.** Fewest files and fastest start, but nothing prevents the HTTP layer
  from leaking into the rules; the "in-memory now, database later" plan then becomes a rewrite. Lost on
  the fact that the only enforcement left would be review discipline.
- **Classic layered controller → service → repository.** Familiar, but the service layer tends to
  accumulate rules next to I/O, which is precisely what must stay separable here.
- **Vertical slices per endpoint.** Attractive for CRUD-shaped features, but the conflict and
  availability logic is shared across create and search, so slicing would duplicate it or create a
  shared folder that is a domain layer under another name.
- **Direct dictionary access with no ports.** Rejected because the tests would then bind to the storage
  implementation, making the database migration a test rewrite as well.

## Revisit triggers

- A database (or any out-of-process store) is introduced — extract `RoomBook.Infrastructure`.
- A use case needs storage-side filtering or pagination that the ports cannot express.
- A second bounded context appears (for example rooms and equipment become independently managed).
- Architecture tests start being skipped or weakened to let a change land.
