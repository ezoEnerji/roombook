# ADR 0004 — `Microsoft.Extensions.TimeProvider.Testing` for the test clock

- Status: Accepted
- Date: 2026-09-12

## Context

Three rules depend on the current instant: a booking must start after now (BR-8), at most ninety days
ahead (BR-7), and may only be cancelled before it starts (BR-9, still to come). Tests that read the
real clock would pass or fail depending on the day they run, so `docs/conventions.md` requires the
instant to arrive through an injected `TimeProvider` and FD-3 forbids reading the ambient clock.

.NET ships the `TimeProvider` abstraction but no test double for it. `docs/security.md` requires an ADR
for **any** new NuGet package, which is what brings this decision here rather than into a commit
message.

## Decision

The HTTP test suite takes `Microsoft.Extensions.TimeProvider.Testing` (9.10.0, pinned) and uses
`FakeTimeProvider` to fix "now" while the application under test runs.

The domain test suite takes no such dependency, because the domain does not hold a clock at all: rules
receive the instant as a plain `DateTimeOffset`, so a constant in the test is already deterministic.

## Consequences

What it buys: the time-dependent rules are tested at their exact boundaries — start equal to now,
start exactly ninety days ahead — with no tolerance windows and no seasonal flakiness. The package is
first-party, test-only, and never reaches a shipped assembly, so it cannot influence production
behaviour.

What it costs: one more dependency to keep current, and a second way of expressing "now" in tests
(a constant in domain tests, a fake provider in HTTP tests) that a reader has to notice. Anyone adding
a test project that exercises the host has to remember the package.

## Alternatives considered

- **A hand-written `TimeProvider` subclass** overriding `GetUtcNow`. Five lines, no dependency, no ADR.
  It lost because `docs/conventions.md` already named `FakeTimeProvider` as the project's answer to
  test clocks, and deviating from a written decision to save one file trades a documented convention
  for a private one. It remains the obvious fallback if the package ever becomes a burden.
- **Passing the instant into the HTTP layer as a request field.** Would make production behaviour
  depend on caller-supplied time — an obvious way to book the past.
- **Tolerance-based assertions** ("within a minute of now"). This is exactly how a boundary rule like
  BR-7 stops being tested at its boundary.

## Revisit triggers

- The package stops being maintained or starts pulling in transitive dependencies.
- We need to control time in the domain suite as well, which would mean the domain has grown a clock
  and FD-3 has been broken.
- Timer-based behaviour appears (reminders, expiry), where a fake clock's timer support matters and
  the hand-rolled alternative would no longer be equivalent.
