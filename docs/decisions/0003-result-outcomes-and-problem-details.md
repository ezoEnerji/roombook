# ADR 0003 — Rule violations are `Result` values mapped to RFC 9457 ProblemDetails

- Status: Accepted
- Date: 2026-09-12

## Context

Seven of the nine business rules (BR-1, BR-2, BR-4, BR-6, BR-7, BR-8, BR-9) reject bookings for reasons
a client must be able to tell apart: an overlap is a different situation from a booking outside business
hours, and a late cancellation is different again. The other two are not rejections — BR-3 permits
back-to-back bookings and BR-5 fixes the wire format. Clients need a stable, machine-readable signal; humans need a readable message; tests
need to assert the specific reason rather than "some 4xx".

Two mechanisms compete for carrying that signal: exceptions thrown by the domain and caught by
middleware, or explicit return values. The domain must also stay free of ASP.NET Core types (FD-2), so
whatever it returns cannot be an HTTP concept.

## Decision

`Domain` and `Application` return `Result` / `Result<T>` carrying a stable error code and a
human-readable message; a business rule never throws. Exceptions are reserved for programmer errors
and infrastructure failures, which surface as `500` with no internal detail.

The API adapter translates each error code into an RFC 9457 `ProblemDetails` response with an
additional `code` member. The code-to-status mapping is a fixed table in `docs/conventions.md`:
`request.invalid` → 400, `*.not_found` → 404, `booking.overlap` and `booking.cancel_after_start` → 409
(state conflicts), and the remaining rule violations → 422.

## Consequences

What it buys: rejection is part of each use case's signature, so a caller cannot forget a case the way
an uncaught exception allows. Error codes give acceptance criteria something exact to assert, which is
what makes "one accepting and one rejecting test per rejection rule" meaningful. The domain stays HTTP-free, and
the status mapping stops being re-decided per endpoint.

What it costs: `Result` plumbing is explicit work at every call site and reads more verbosely than a
throw, and C# has no built-in result type, so we own a small one. Error codes become public API — once
a client depends on `booking.overlap`, renaming it is a breaking change. The 400/422/409 distinction is
a convention clients must learn, and 422 is less widely understood than 400.

## Alternatives considered

- **Domain exceptions plus middleware.** Less ceremony at call sites, but rejection disappears from
  signatures, control flow becomes invisible, and exceptions for expected outcomes are both slower and
  harder to test exhaustively.
- **Mixed: `Result` for validation, exceptions for conflicts.** Rejected because two mechanisms means
  every new rule starts with an argument about which one applies.
- **Plain ProblemDetails without a `code` member.** Standard-compliant, but then tests and clients must
  parse prose, which makes error messages accidentally load-bearing.
- **Single status for all rule violations (400, or 409 for everything).** Simpler to explain, but it
  throws away the distinction between "your request is malformed" and "the room is taken", which is the
  one thing a booking client genuinely needs to react to.

## Revisit triggers

- A client reports that the 400/422/409 split is ambiguous or unusable in practice.
- Multiple rule violations need to be reported in a single response (today the first failure wins).
- The `Result` type starts growing features (chaining, async combinators) — that is the signal to adopt
  a maintained library instead, via its own ADR.
- Localisation of error messages becomes a requirement.
