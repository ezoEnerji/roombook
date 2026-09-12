# Spec 0002 — Create a booking

- Status: Shipped
- Mode: lite (from AGENTS.md at creation time)
- Plan: `specs/plans/0002-plan.md`

## Intent

This is the product. Everything shipped so far — the layers, the boundary rules, the room listing —
exists so that this slice can be written with confidence: a person reserves a room for a window of
time, and RoomBook refuses anything that would double-book the room or break the office's rules,
saying precisely which rule was broken.

Success looks like this: a caller can reserve a room and read the reservation back; an overlapping
reservation is refused with a reason a machine can act on, while two reservations that merely touch
are both accepted; and every refusal names exactly one rule. The rules that depend on the current
time are proven with a clock the tests control, so they are as reliable in December as in June.

Deliberately not in this slice: cancelling a reservation (BR-9), listing reservations, and searching
for free slots. Those are separate specs and each one is smaller once this exists.

## Requirements

- Someone can reserve a specific room for a specific window of time, recording a title, the name of
  the person the reservation belongs to, and how many people will attend.
- A reservation that overlaps an existing reservation in the same room is refused. Two reservations
  that merely touch — one ending exactly when the other starts — are both allowed.
- A reservation is refused when it falls outside the room's opening hours, is shorter than fifteen
  minutes or longer than four hours, is for more people than the room holds, starts in the past or at
  the current instant, or starts more than ninety days from now.
- Every refusal carries exactly one machine-readable reason, so a caller can tell "the room is taken"
  apart from "your request breaks a rule" without reading prose meant for humans.
- A successful reservation can be read back afterwards using the identifier it was given.
- The system never stores two overlapping reservations for the same room, even when two requests for
  the same window arrive at the same moment.
- Opening hours are judged in the room's own time zone, including on the days when that zone changes
  offset.

## Constraints & out of scope

- Rules BR-1, BR-2, BR-3, BR-4, BR-6, BR-7 and BR-8 are all in scope and are the point of this slice.
  BR-5 (UTC on the wire) and the error contract in `docs/conventions.md` are honoured as written.
- A reservation request carries the window as two instants — a start and an end, UTC ISO-8601. The
  duration is derived from them, so there is no second representation to contradict the first.
- The attendee count is required. A default would quietly disable BR-6, which is the opposite of
  what a rule is for.
- **One rule at a time, in a fixed order.** When a request breaks several rules, the refusal names the
  first one in this sequence: request shape → the room exists → BR-8 (the past) → BR-7 (the horizon) →
  BR-4 (duration) → BR-1 (opening hours) → BR-6 (capacity) → BR-2 (overlap). Context-free checks come
  first and the only check that has to consult other reservations comes last. The order is documented,
  not incidental, so the tests are stable.
- **The no-overlap rule is enforced where the data is,** as a single "store this unless it overlaps"
  operation. A check followed by a separate write leaves a window in which two callers both pass the
  check, and BR-2 is an invariant rather than a validation.
- Storage stays in memory (ADR-0001) and the API stays anonymous (ADR-0002); the person a reservation
  belongs to is free text supplied by the caller, not an authenticated identity.
- "Now" is supplied by an injected `TimeProvider`; tests control it. Reading the ambient clock is a
  forbidden dependency (FD-3).
- Out of scope: cancelling (BR-9), listing reservations, availability search, editing a reservation,
  recurring reservations, notifications, and any change to how rooms are created or seeded.

## Acceptance criteria

- [x] AC-1 — A valid reservation is created: the response is `201` with a `Location` header and a body
      carrying the new identifier and the values that were sent.
- [x] AC-2 — The created reservation can be read back by its identifier and matches what was stored.
- [x] AC-3 — A reservation overlapping an existing one in the same room is refused with `409` and the
      code `booking.overlap`; the same window in a *different* room is accepted.
- [x] AC-4 — Touching reservations are accepted in both directions: one starting exactly when another
      ends, and one ending exactly when another starts (BR-3).
- [x] AC-5 — Opening hours (BR-1): a reservation ending exactly at closing time is accepted; one
      starting at closing time, or reaching past it, is refused with `422` and
      `booking.outside_business_hours`.
- [x] AC-6 — Duration (BR-4): exactly fifteen minutes and exactly four hours are accepted; fourteen
      minutes and four hours plus one minute are refused with `422` and
      `booking.duration_out_of_range`.
- [x] AC-7 — Attendees (BR-6): a count equal to the room's capacity is accepted; one above it is
      refused with `422` and `booking.attendees_exceed_capacity`. A count below one is a malformed
      request: `400` with `request.invalid`.
- [x] AC-8 — The past (BR-8): a start earlier than now is refused with `422` and
      `booking.start_in_past`, and so is a start exactly equal to now.
- [x] AC-9 — The horizon (BR-7): a start exactly ninety days ahead is accepted; ninety days plus one
      minute is refused with `422` and `booking.too_far_in_future`.
- [x] AC-10 — A reservation for a room that does not exist is refused with `404` and `room.not_found`.
- [x] AC-11 — A malformed request is refused with `400` and `request.invalid`: a body that is not
      valid JSON, an unknown member, a missing required field, a title over 200 characters, an
      organizer name over 100 characters, and a timestamp that is not UTC ISO-8601.
- [x] AC-12 — Every refusal is an RFC 9457 `ProblemDetails` document carrying the `code` member, and
      no response ever contains a stack trace, an exception type name or a configuration value.
- [x] AC-13 — When two requests for the same room and window are handled concurrently, exactly one
      succeeds and the other is refused with `booking.overlap`.
- [x] AC-14 — Opening hours are judged in the room's time zone, proven on a day when that zone changes
      offset: the same UTC window is inside business hours before the transition and outside it after.
- [x] AC-15 — Every time-dependent rule (BR-7, BR-8) is proven with a controlled clock; no test reads
      the real time.
- [x] AC-16 — Reading a reservation that does not exist is refused with `404` and
      `booking.not_found`.
- [x] AC-17 — A request that breaks several rules at once is refused with the code the documented
      order selects — for example a reservation that is both in the past and six hours long comes
      back as `booking.start_in_past`, not `booking.duration_out_of_range`.
- [x] AC-18 — A request body over the documented 32 KB limit is refused with `413` and
      `request.too_large`, and a body under it is judged on its contents instead. Added during
      triage: the limit was written in `docs/security.md` at bootstrap and had never been
      implemented, so it was a rule with no proof.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation) — table in the
      pull request; 114 tests
- [x] `scripts/check` green — locally and in CI
- [x] Independent review done; real findings fixed, noise rejected with written rationale — one review
      round and one narrow re-review, both in separate read-only sessions
- [x] Docs / ADRs updated if behavior or architecture changed — the precedence order,
      `request.too_large` → 413, `GET /bookings/{id}`, the attendee-count double check, and ADR-0004
- [x] Spec moved to `specs/done/` (it becomes immutable there) — in the commit that closes this slice

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 1 — AC-18 was *added* during triage, when the review found a documented 32 KB body limit that had never been implemented |
| Fix rounds | 2 — the review findings, then a correction after the re-review showed the body limit only worked under Kestrel |
| Review findings: real / noise | 7 real / 2 noise across both rounds |
| Regressions introduced | 0 — S-001's tests stayed green throughout, including the room contract |
| Bugs escaped to production | 0 — not deployed |
