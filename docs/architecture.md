# Architecture

> Decided at bootstrap. This describes the architecture we chose, not an aspiration.
> Agents read this file before planning; vague answers here become vague code.

## System overview

RoomBook is a single-process HTTP API: ASP.NET Core **Minimal API** on **.NET 9** (`net9.0`), arranged
as a modular monolith in a **lightweight hexagonal (ports & adapters)** shape — a pure domain core,
thin application use cases, and adapters for HTTP and storage. The reason is narrow and deliberate:
the product's value is conflict detection and availability search, so those algorithms live in a core
with no I/O, which makes them fast and deterministic to test and lets V1's in-memory store be replaced
by a database without touching a single rule (ADR-0001). V1 is single-office, in-memory and anonymous;
persistence and identity are explicit later steps, not hidden assumptions (ADR-0002).

## Modules / components and ownership

| Module | Single responsibility | Owns |
|---|---|---|
| `src/RoomBook.Domain` | The domain model and every business rule (BR-1…BR-9): time-slot algebra, conflict detection, availability search. Pure, no I/O. | `Room`, `Booking`, `TimeSlot`, `BusinessHours`, rule outcomes and error codes |
| `src/RoomBook.Application` | Use cases orchestrating the domain behind ports: create booking, cancel booking, list bookings, list rooms, search availability. | Ports (`IBookingRepository`, `IRoomRepository`), use-case contracts, `Result` outcomes |
| `src/RoomBook.Api` | HTTP adapter and composition root: routing, DTOs, edge validation, ProblemDetails mapping, DI wiring, configuration. | HTTP contract, request/response DTOs, error-code → status mapping |
| `src/RoomBook.Infrastructure` | The storage adapter: SQLite behind the ports, the schema, and the seeded rooms. The only project with a database dependency. | `RoomBookStore`, `SqliteRoomRepository`, `SqliteBookingRepository`, `SeedRooms` |

The storage adapter moved into its own project when the database arrived (S-005, ADR-0005), as this
document said it would. The ports survived that move almost intact: every rule, every endpoint and
every test stayed as it was, and the two things that did have to change are recorded in ADR-0005 — the
domain gained a rehydration entry point, and the cancellation port gained the current instant.

## Communication rules

- Dependencies point inward only: `Api → Application → Domain`. `Domain` depends on nothing.
- Endpoints call use cases. An endpoint never touches a repository and never holds a business rule.
- Use cases reach storage only through ports, never through a concrete repository type.
- The domain never calls out: it receives plain data and returns a result. No I/O, no clock, no logging.
- **The clock belongs to `Application`.** It holds the injected `TimeProvider` and passes the current
  instant into domain functions as a plain `DateTimeOffset` value. `TimeProvider` is never injected into
  domain types — that would give the domain a service dependency it is defined not to have.
- Direct in-process calls only. No events, queues or background processing in V1 — introducing one is
  an ADR, not an implementation detail.

## HTTP surface

REST/JSON, resource-oriented — decided at bootstrap. Request and response shapes belong to the spec
that builds each route, not to this file:

| Route | Purpose |
|---|---|
| `POST /bookings` | Create a booking; rejection rules BR-1, BR-2, BR-4, BR-6, BR-7, BR-8 apply |
| `GET /bookings/{id}` | Read one booking |
| `GET /bookings` | List bookings |
| `DELETE /bookings/{id}` | Cancel a booking (BR-9) |
| `GET /rooms` | List rooms |
| `GET /availability` | Search free slots that fit a requested duration |

## Forbidden dependencies (make them testable)

To be asserted by architecture tests, which start running inside `scripts/check` when S-001 activates
its steps. Until then these rules are binding on review, not yet machine-checked:

- **FD-1** `RoomBook.Domain` references no project and no NuGet package — BCL only. The check covers
  `Directory.Build.props` as well as the project file, because a package declared there reaches the
  domain just as surely.
- **FD-2** `RoomBook.Domain` and `RoomBook.Application` contain no ASP.NET Core types (`HttpContext`,
  `IResult`, `ControllerBase`, `ProblemDetails`) and no `Microsoft.AspNetCore.*` reference.
- **FD-3** No production code reads the ambient clock (`DateTime.Now/UtcNow`,
  `DateTimeOffset.Now/UtcNow`). The current instant enters through the `TimeProvider` injected into
  `Application` and travels onward as a value.
- **FD-4** No service locator and no static mutable state — dependencies arrive by constructor injection.
- **FD-5** Domain types are never serialised into an HTTP body; the API owns its own DTO records. The
  static rule inspects contract types by name, so it cannot see an endpoint returning a domain object
  from a lambda; that half is enforced behaviourally by a test asserting the exact JSON property set.
- **FD-6** Banned packages: MediatR and CQRS infrastructure, AutoMapper and other auto-mappers,
  Newtonsoft.Json, EF Core or any ORM, FluentAssertions. Any new dependency requires an ADR. The
  SQLite driver (`Microsoft.Data.Sqlite`, ADR-0005) is allowed in `RoomBook.Infrastructure` and
  nowhere else: it is a driver, not an ORM, and the ban stands as written.

The tests cover FD-1…FD-6, and each rule is proven twice: it passes on the real code, and it detects a
violation — either in a synthetic input or in `tests/RoomBook.Architecture.Fixtures`, an assembly that
breaks FD-2 to FD-5 on purpose. Reference *direction* is additionally enforced by the compiler, since
project references only point inward, which makes FD-3, FD-4 and FD-6 the rules that would otherwise
go unnoticed until review.

Known limit: the ambient-clock check (FD-3) reads assembly metadata, so a clock reached through
reflection would not appear in it. That is an accepted gap while nothing in the product depends on
time; it should be revisited when the time-dependent rules (BR-7, BR-8, BR-9) are implemented.

~~Known limit: cancelling is three steps and the instant is read in the middle.~~ **Closed in S-005**
(ADR-0005): the store judges BR-9 and deletes in one conditional statement, against the instant the
use case read, so nothing can start in the gap. The domain still owns the rule; the SQL condition
owns the atomicity. The port gained a parameter to make that possible, which is one of the two
amendments S-005 made to ADR-0001's claim.

## Deliberately out of scope

Authentication and authorisation (V2 — see `docs/security.md`), multi-office and multi-tenancy,
recurring bookings, editing a booking, notifications and calendar sync, caching, messaging/queues, and
horizontal scaling. Persistent storage left this list in S-005; schema migrations took its place and
are the first thing the next schema change will need (ADR-0005).
