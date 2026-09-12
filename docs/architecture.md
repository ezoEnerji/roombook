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
- Direct in-process calls only. No events, queues or background processing in V1 — introducing one is
  an ADR, not an implementation detail.

## Forbidden dependencies (make them testable)

Asserted by architecture tests that run inside `scripts/check`:

- **FD-1** `RoomBook.Domain` references no project and no NuGet package — BCL only.
- **FD-2** `RoomBook.Domain` and `RoomBook.Application` contain no ASP.NET Core types (`HttpContext`,
  `IResult`, `ControllerBase`, `ProblemDetails`) and no `Microsoft.AspNetCore.*` reference.
- **FD-3** No production code reads the ambient clock (`DateTime.Now/UtcNow`,
  `DateTimeOffset.Now/UtcNow`); "now" arrives through an injected `TimeProvider`.
- **FD-4** No service locator and no static mutable state — dependencies arrive by constructor injection.
- **FD-5** Domain types are never serialised into an HTTP body; the API owns its own DTO records.
- **FD-6** Banned packages: MediatR and CQRS infrastructure, AutoMapper and other auto-mappers,
  Newtonsoft.Json, EF Core or any ORM (V1), FluentAssertions. Any new dependency requires an ADR.

Direction is additionally enforced by the compiler: project references only point inward, so a reverse
reference cannot compile. The tests exist for the rules the compiler cannot see (FD-3, FD-4, FD-6).

## Deliberately out of scope

Persistent storage, authentication and authorisation (V2 — see `docs/security.md`), multi-office and
multi-tenancy, recurring bookings, editing a booking, notifications and calendar sync, caching,
messaging/queues, and horizontal scaling.
