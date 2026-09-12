# Domain

> The shared language between business, humans, and agents.
> If a term isn't here, expect the AI to invent its own meaning for it.

## Ubiquitous language

| Term | Meaning | Notes / not to be confused with |
|---|---|---|
| Room | A bookable meeting room. Has a name, a capacity, an IANA time zone and BusinessHours. | Not a building or a floor. V1 serves one office, but every Room still carries its own time zone and hours. |
| Booking | A reservation of one Room for one TimeSlot by an Organizer, with a title and an attendee count. | Always "Booking" — never "Reservation", "Event" or "Meeting", in code, specs or conversation. |
| TimeSlot | A half-open interval in UTC: `[start, end)`. | "Slot" on its own is not a term. The half-open form is what makes back-to-back bookings legal (BR-3). |
| Conflict | Two Bookings in the same Room whose TimeSlots intersect. | Touching endpoints (`a.end == b.start`) is **not** a Conflict. |
| BusinessHours | The local-time window in which a Room may be booked; default 09:00–18:00 in that Room's time zone. | Evaluated in the Room's time zone, never in UTC (BR-1). |
| Organizer | The person a Booking belongs to; free-text name in V1. | Not a `User` entity — V1 has no identity model at all (see `docs/security.md`). |
| AvailableSlot | A candidate TimeSlot produced by an availability search: fits the requested duration and breaks no business rule. | Never stored; computed per request. Not a Booking. |
| Availability search | Given a duration, a date-time window and optionally a Room, returns AvailableSlots aligned to a 15-minute grid, earliest first. | Not a free/busy dump — it answers "where does my meeting fit?". |

## Business rules

- **BR-1** A Booking's TimeSlot lies entirely within its Room's BusinessHours, evaluated in that Room's
  time zone. Consequently a Booking starts and ends on the same local day.
- **BR-2** Two Bookings in the same Room never overlap.
- **BR-3** Back-to-back is allowed: a Booking may start exactly when another one in the same Room ends.
- **BR-4** A Booking's duration is at least 15 minutes and at most 4 hours.
- **BR-5** All times are UTC, ISO-8601 with `Z`, both on the wire and in storage.
- **BR-6** `attendeeCount` is at least 1 and never exceeds `room.capacity`.
- **BR-7** A Booking starts at most 90 days after "now".
- **BR-8** A Booking starts after "now" — the past is not bookable.
- **BR-9** A Booking may be cancelled only before its start; cancelling at or after the start is rejected.

Every rule maps to exactly one error code and one HTTP status — the table lives in
`docs/conventions.md` (error handling), so the mapping has a single source of truth.

## Key domain invariants

- No Room ever holds two overlapping Bookings — not via creation, not via any future move or import path.
- A stored Booking satisfies every business rule that applied when it was created. Rules are enforced
  *before* persistence; stored data is never "repaired" afterwards.
- "Now" never comes from the ambient clock. It is supplied by an injected `TimeProvider`, so every
  time-dependent rule (BR-7, BR-8, BR-9) is deterministically testable.
- Calendar reasoning (business hours, local days) happens in the Room's time zone; UTC is the storage
  and transport format only. Mixing the two is the bug class this project cares about most.
- An availability search never returns a slot that a subsequent create would reject.

## Deliberately out of scope in V1

Persistent storage, authentication and per-user ownership, multi-office/multi-tenancy, recurring
bookings, editing an existing Booking (cancel and create instead), attendee invitations,
notifications, external calendar sync, and room equipment/features.
