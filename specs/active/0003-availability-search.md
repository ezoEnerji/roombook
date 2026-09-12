# Spec 0003 — Availability search

- Status: Draft
- Mode: lite (from AGENTS.md at creation time)
- Plan: `specs/plans/0003-plan.md`

## Intent

RoomBook can now refuse a bad booking, but it cannot yet answer the question people actually ask:
*where does my meeting fit?* Today a caller has to guess a window, get refused, and guess again. This
slice replaces guessing with an answer — the system proposes windows it would accept.

Success looks like this: a caller says "I need an hour sometime this week, for six people", and gets
back candidate windows that are genuinely bookable — inside the room's opening hours, clear of every
existing booking, aligned to the quarter hour on the room's own clock, earliest first. The strongest
promise in `docs/domain.md` becomes testable here: **a slot the search returns is never refused by a
subsequent booking request.**

Deliberately not in this slice: cancelling a booking (BR-9), listing existing bookings, ranking
candidates by anything other than time, and reserving a proposed slot atomically ("hold" semantics).

## Requirements

- Someone can ask where a meeting of a given length fits inside a window of time, optionally narrowing
  the question to one room, and optionally saying how many people will attend.
- The answer contains only windows the system would accept: clear of every existing booking, inside
  that room's opening hours, and within the booking rules that apply to a new booking.
- Candidate windows are exactly the requested length, aligned to the quarter hour on the room's own
  clock, and ordered earliest first so the obvious choice is the first one.
- When the caller names an attendee count, rooms that cannot hold that many people are left out
  entirely — a proposal that would be refused on capacity is not a proposal.
- When nothing fits, the answer says so plainly rather than failing.
- The search window is bounded, and a question the rules could never answer — a length no booking may
  have — is refused rather than answered with an empty list, because those two answers mean different
  things to a caller.

## Constraints & out of scope

- The search respects the same rules as creating a booking: BR-1 (opening hours, in the room's zone),
  BR-2 (no overlap), BR-3 (touching is allowed, so a candidate may begin exactly when a booking ends),
  BR-4 (length), BR-6 (capacity, when an attendee count is given), BR-7 (horizon) and BR-8 (the past).
- Storage stays in memory (ADR-0001) and the API stays anonymous (ADR-0002).
- "Now" arrives through the injected `TimeProvider`; no rule reads the ambient clock (FD-3).
- The window the caller asks about spans at most 31 days (`docs/security.md`).
- A proposed slot is **not** held or reserved. Two callers can be shown the same slot, and the one who
  books it first wins — BR-2 is what makes that safe.
- **One candidate per free stretch.** For each room, each contiguous run of free time inside opening
  hours yields its *earliest* fitting candidate, not every quarter hour that would fit. Returning all
  of them is arithmetically complete and practically useless: a single free day would produce over a
  hundred near-identical answers and a week-long question would hit the cap before reaching Tuesday.
- The caller asks with a length in minutes (`durationMinutes`) and an explicit window (`from`, `to` as
  UTC instants). Both ends of the window are required — a default would answer a question the caller
  did not ask.
- **At most 50 candidates** in one answer, earliest first. If more exist the answer is truncated; there
  is no paging in this slice.
- Out of scope: cancelling, listing bookings, recurring searches, ranking by room size or preference,
  suggesting alternatives in other buildings, and any caching of results.

## Acceptance criteria

- [ ] AC-1 — A search for a length inside a window returns candidate windows for every room, each one
      exactly the requested length, inside that room's opening hours, and clear of existing bookings.
      Each contiguous free stretch contributes its earliest fitting candidate and no more.
- [ ] AC-2 — Candidates are aligned to the quarter hour on the room's local clock (`:00`, `:15`, `:30`,
      `:45`) and ordered earliest first; ties between rooms break by room name, ordinally.
- [ ] AC-3 — Narrowing the search to one room returns candidates for that room only; an unknown room
      is refused with `404` and `room.not_found`.
- [ ] AC-4 — Naming an attendee count excludes rooms whose capacity is below it, and includes a room
      whose capacity equals it.
- [ ] AC-5 — **Every returned candidate is accepted by a booking request.** Proven by taking each
      candidate the search proposes and creating it, with no refusals.
- [ ] AC-6 — A candidate never overlaps an existing booking, and a candidate may begin exactly when an
      existing booking ends (BR-3).
- [ ] AC-7 — When nothing fits — a fully booked day, or a window entirely outside opening hours — the
      answer is `200` with an empty list, never `404` and never an error.
- [ ] AC-8 — A requested length outside the bookable range is refused with `422` and
      `booking.duration_out_of_range`, because "no booking may be 5 minutes long" and "nothing is free"
      are different answers.
- [ ] AC-9 — A window longer than 31 days is refused with `400` and `request.invalid`, and so is a
      window whose end is not after its start.
- [ ] AC-10 — A window that reaches into the past yields candidates only from now onwards; a window
      entirely in the past yields an empty list.
- [ ] AC-11 — A window reaching beyond the 90-day horizon yields candidates only up to the horizon.
- [ ] AC-12 — An existing booking that does not sit on the grid (10:07–11:07) does not shift the grid:
      candidates stay on the quarter hour and simply skip the occupied time.
- [ ] AC-13 — On a day when the room's zone changes offset, candidates are still aligned to the room's
      local quarter hour and still inside its local opening hours.
- [ ] AC-14 — An answer carries at most 50 candidates, applied *after* ordering so the earliest ones
      survive truncation.
- [ ] AC-15 — Time-dependent behaviour is proven with a controlled clock; no test reads the real time.

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
