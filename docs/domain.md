# Domain

> The shared language between business, humans, and agents.
> If a term isn't here, expect the AI to invent its own meaning for it.

## Ubiquitous language

| Term | Meaning | Notes / not to be confused with |
|---|---|---|
| Room | A bookable meeting room. Has a name, a capacity (at least 1), an IANA time zone and BusinessHours. | Not a building or a floor. V1 serves one office, but every Room still carries its own time zone and hours. |
| Booking | A reservation of one Room for one TimeSlot by an Organizer, with a title and an attendee count. | Always "Booking" — never "Reservation", "Event" or "Meeting", in code, specs or conversation. |
| TimeSlot | A half-open interval in UTC: `[start, end)`. | "Slot" on its own is not a term. The half-open form is what makes back-to-back bookings legal (BR-3). |
| Conflict | Two Bookings in the same Room whose TimeSlots intersect. | Touching endpoints (`a.end == b.start`) is **not** a Conflict. |
| BusinessHours | The half-open local-time window `[open, close)` in which a Room may be booked; default `[09:00, 18:00)` in that Room's time zone. | Evaluated in the Room's time zone, never in UTC (BR-1). Half-open like TimeSlot: a Booking may *end* exactly at `close`, but may not *start* there. |
| Organizer | The person a Booking belongs to; free-text name in V1. | Not a `User` entity — V1 has no identity model at all (see `docs/security.md`). |
| AvailableSlot | A candidate result of an availability search: the Room plus a TimeSlot that fits the requested duration and breaks no rejection rule. | Never stored; computed per request. Not a Booking. |
| Availability search | Given a duration, a date-time window and optionally a Room, returns AvailableSlots aligned to a 15-minute grid, earliest first. | Not a free/busy dump — it answers "where does my meeting fit?". |

## Business rules

**BR-1, BR-2, BR-4, BR-6, BR-7, BR-8 and BR-9 are rejection rules:** each one rejects a request and maps
to exactly one error code and one HTTP status. That table lives in `docs/conventions.md`, so the mapping
has a single source of truth. **BR-3 and BR-5 are not rejection rules** — BR-3 *permits* a shape a naive
implementation would reject, and BR-5 is a format contract enforced at the API edge. The distinction
drives how each rule is tested (`docs/testing.md`).

- **BR-1** A Booking's TimeSlot lies within its Room's BusinessHours, evaluated in that Room's time
  zone: `open ≤ startLocal` and `endLocal ≤ close`. A Booking that ends exactly at closing time is valid
  (17:00–18:00 against an 18:00 close); one that starts at closing time is not. A Booking therefore
  starts and ends on the same local day.
- **BR-2** Two Bookings in the same Room never overlap.
- **BR-3** Back-to-back is allowed: a Booking may start exactly when another one in the same Room ends.
- **BR-4** A Booking's duration is at least 15 minutes and at most 4 hours, both bounds inclusive.
- **BR-5** All times are UTC, ISO-8601 with `Z`, both on the wire and in storage.
- **BR-6** `attendeeCount` never exceeds `room.capacity`. (A value below 1 is a field-limit violation
  rejected at the API edge, not a capacity question — see `docs/conventions.md`.)
- **BR-7** A Booking starts at most 90 days after "now", inclusive: exactly 90 days ahead is accepted.
- **BR-8** A Booking starts strictly after "now": `start == now` is rejected, and so is anything earlier.
- **BR-9** A Booking may be cancelled only before its start; cancelling at or after the start is
  rejected. Cancelling removes the Booking permanently — the slot becomes free immediately, and
  cancelling the same Booking again is a not-found, not a second success.

## Explicit non-rules

Written down because their absence is a decision, not an omission:

- The 15-minute grid applies **only to availability search output**. Creating a Booking at 10:07 is
  valid as long as every rejection rule holds; times are never rounded, snapped or realigned.
- There is no minimum notice period beyond BR-8, and no limit on how many Bookings one Organizer holds.
- A cancelled Booking leaves no record. V1 keeps no history and no audit trail.

## Key domain invariants

- No Room ever holds two overlapping Bookings — not via creation, not via any future move or import path.
- A stored Booking satisfies every business rule that applied when it was created. Rules are enforced
  *before* persistence; stored data is never "repaired" afterwards.
- "Now" never comes from the ambient clock. The Application layer owns the `TimeProvider` and passes the
  current instant into the domain as a plain value; the domain itself has no clock at all
  (`docs/architecture.md`). This is what makes BR-7, BR-8 and BR-9 deterministically testable.
- Calendar reasoning (business hours, local days) happens in the Room's time zone; UTC is the storage
  and transport format only. Mixing the two is the bug class this project cares about most.
- An availability search never returns a slot that a subsequent create would reject.

## Deliberately out of scope in V1

Persistent storage, authentication and per-user ownership, multi-office/multi-tenancy, recurring
bookings, editing an existing Booking (cancel and create instead), attendee invitations,
notifications, external calendar sync, and room equipment/features.
