# Spec 0004 — Cancel and list bookings

- Status: Draft
- Mode: lite (from AGENTS.md at creation time)
- Plan: `specs/plans/0004-plan.md`

## Intent

A booking that cannot be cancelled is worse than no booking: the room stays blocked until the process
restarts, and the only way to free it is to lose everything. And a caller who creates a booking gets
an identifier back with no way to see what else exists. This slice closes both gaps and, with them,
the last business rule that is written down but not implemented — BR-9.

Success looks like this: a plan changes, the booking is cancelled, and the room is immediately
available again — to the booking endpoint and to the availability search alike. Meanwhile anyone can
see what is booked in a window, so the API can be used without remembering identifiers.

Deliberately not in this slice: editing a booking (cancel and create instead), recurring bookings,
notifying anyone that a booking disappeared, and any form of ownership — which is precisely what
makes cancellation the sharpest argument for the authentication deferred in ADR-0002.

## Requirements

- Someone can cancel a booking that has not started yet. The room becomes free immediately.
- Cancelling a booking at or after the moment it starts is refused: a meeting in progress is not
  something the system pretends never happened.
- Cancelling a booking that does not exist — including one already cancelled — says so, and says it
  the same way both times.
- A cancelled booking leaves no trace: it disappears from the list, from a direct read, and from the
  time the availability search treats as occupied.
- Someone can see the bookings in a window of time, optionally narrowed to one room, ordered by when
  they start.
- A window with nothing in it is an empty answer, not a failure.
- **Anyone can cancel anyone's booking in this version.** That is the accepted risk of an anonymous
  API (ADR-0002) and it is recorded here so no one mistakes it for an oversight.

## Constraints & out of scope

- BR-9 as already written in `docs/domain.md`: cancellation is permitted only before the start, it
  removes the booking permanently, and a missing booking is a not-found rather than a rule violation.
- Storage stays in memory (ADR-0001) and the API stays anonymous (ADR-0002).
- "Now" arrives through the injected `TimeProvider` (FD-3).
- **Listing asks with an explicit window** (`from`, `to` as UTC instants), capped at 31 days, exactly
  as the availability search does. One rule for windows across the API is worth more than a
  convenience default on one endpoint, and hidden defaults were rejected once already in S-003.
- **A list carries at most 200 bookings**, truncated after ordering. The availability cap is 50
  because suggestions are more useful when there are few of them; bookings are facts, and silently
  hiding a fact is worse than a long answer — so the cap is higher and exists only to bound the
  response.
- Out of scope: editing, recurrence, notifications, ownership and authorisation, audit trails of
  cancelled bookings, and filtering by organizer — a free-text name filter would turn a booking list
  into a people search, which is not something an anonymous API should offer.

## Acceptance criteria

- [ ] AC-1 — Cancelling a booking that has not started returns `204` with no body.
- [ ] AC-2 — After cancelling, the same window can be booked again in the same room.
- [ ] AC-3 — Cancelling at exactly the booking's start is refused with `409` and
      `booking.cancel_after_start`; so is cancelling after it has started.
- [ ] AC-4 — Cancelling the same booking twice returns `404` and `booking.not_found` the second time.
- [ ] AC-5 — Cancelling an identifier that never existed returns `404` and `booking.not_found` — the
      same answer as the previous case, because the caller's situation is the same.
- [ ] AC-6 — A cancelled booking is gone from a direct read (`404`) and from the list.
- [ ] AC-7 — The availability search proposes the freed time again after a cancellation.
- [ ] AC-8 — Listing returns the bookings whose time touches the window, ordered by start and then by
      room name, ordinally.
- [ ] AC-9 — Listing narrowed to one room returns that room's bookings only; an unknown room is
      refused with `404` and `room.not_found`.
- [ ] AC-10 — A window with no bookings returns `200` and an empty array.
- [ ] AC-11 — A malformed listing query is refused with `400` and `request.invalid`: a missing `from`
      or `to`, an end not after the start, a window longer than 31 days, a non-UTC instant, and a
      `roomId` that is not a GUID.
- [ ] AC-12 — A list carries at most 200 bookings, truncated after ordering so the earliest survive.
- [ ] AC-13 — The BR-9 boundary is proven with a controlled clock: cancelling one minute before the
      start succeeds, and at exactly the start does not.
- [ ] AC-14 — Every response body carries exactly the documented fields, and every refusal is an
      RFC 9457 problem document with its `code`.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise | |
| Regressions introduced | |
| Bugs escaped to production | |
