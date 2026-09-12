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
| `src/RoomBook.Api/Infrastructure` | The V1 storage adapter: in-memory implementations of the ports plus seeded room data. | `InMemoryBookingRepository`, `InMemoryRoomRepository` |

The storage adapter sits inside the API project only while it is in-memory. When a database arrives it
moves to its own `src/RoomBook.Infrastructure` project — the ports do not change, which is the whole
point of having them.

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
| `GET /bookings` | List bookings |
| `DELETE /bookings/{id}` | Cancel a booking (BR-9) |
| `GET /rooms` | List rooms |
| `GET /availability` | Search free slots that fit a requested duration |

## Forbidden dependencies (make them testable)

To be asserted by architecture tests, which start running inside `scripts/check` when S-001 activates
its steps. Until then these rules are binding on review, not yet machine-checked:

- **FD-1** `RoomBook.Domain` references no project and no NuGet package — BCL only.
- **FD-2** `RoomBook.Domain` and `RoomBook.Application` contain no ASP.NET Core types (`HttpContext`,
  `IResult`, `ControllerBase`, `ProblemDetails`) and no `Microsoft.AspNetCore.*` reference.
- **FD-3** No production code reads the ambient clock (`DateTime.Now/UtcNow`,
  `DateTimeOffset.Now/UtcNow`). The current instant enters through the `TimeProvider` injected into
  `Application` and travels onward as a value.
- **FD-4** No service locator and no static mutable state — dependencies arrive by constructor injection.
- **FD-5** Domain types are never serialised into an HTTP body; the API owns its own DTO records.
- **FD-6** Banned packages: MediatR and CQRS infrastructure, AutoMapper and other auto-mappers,
  Newtonsoft.Json, EF Core or any ORM (V1), FluentAssertions. Any new dependency requires an ADR.

The tests cover FD-1…FD-6. Reference *direction* is additionally enforced by the compiler — project
references only point inward, so a reverse reference cannot compile — which makes FD-3, FD-4 and FD-6
the rules that would otherwise go unnoticed until review.

## Deliberately out of scope

Persistent storage, authentication and authorisation (V2 — see `docs/security.md`), multi-office and
multi-tenancy, recurring bookings, editing a booking, notifications and calendar sync, caching,
messaging/queues, and horizontal scaling.
