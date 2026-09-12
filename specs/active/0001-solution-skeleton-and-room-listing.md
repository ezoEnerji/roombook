# Spec 0001 — Solution skeleton and room listing

- Status: In progress
- Mode: lite (from AGENTS.md at creation time)
- Plan: `specs/plans/0001-plan.md`

## Intent

RoomBook has documentation but no code, and `scripts/check` is a green no-op — so nothing in this
repository can currently be verified, and every rule the team agreed on is enforced by memory alone.
This slice closes that gap with the smallest piece of product that exercises the whole shape: a caller
can ask RoomBook which rooms exist and what constrains booking them, and anyone can prove the project's
health with one command.

Success looks like this: `./scripts/check` runs a real format, build and test pass and is green both
locally and in CI; the layer boundaries are checked by machine instead of by review; and one working
read path exists from HTTP through a use case and a storage port into the domain, so the next spec adds
booking rules into a structure that already holds.

Deliberately not in this slice: bookings in any form, availability search, and booking rules
BR-1…BR-9. The only domain rule here is the one that decides whether a Room may exist at all.

## Requirements

- Someone integrating with RoomBook can obtain the list of bookable rooms, and for each room the facts
  they need in order to book it without being rejected later: how many people it holds, the time zone
  its hours are expressed in, and when it opens and closes.
- The listing is predictable: the same rooms come back in the same order on every call, and an
  installation with no rooms answers "there are none" rather than failing.
- A Room that could never be booked successfully cannot exist in the first place. A Room holds at least
  one person, its time zone is one the system can actually resolve, and it opens before it closes.
- Anyone — human or agent — can verify the entire project with a single command, and that command fails
  when one of the agreed layer boundaries is violated.

## Constraints & out of scope

- `net9.0`; layering, naming, error contract and data rules follow `docs/architecture.md` and
  `docs/conventions.md` without exception. Boundaries FD-1…FD-6 apply from the first commit.
- Storage is in memory (ADR-0001): no database, no ORM, nothing survives a restart.
- No authentication (ADR-0002); the read path is anonymous by design.
- Rooms are fixed seed data, decided here: **Ada** (4 people, 09:00–18:00), **Boğaziçi** (12,
  09:00–18:00) and **Kapadokya** (24, 08:30–17:30), all in `Europe/Istanbul`. They are not configurable
  and not writable through the API; creating, editing and deleting rooms is out of scope. Their
  identifiers are fixed literal `Guid` values so a client may store one and use it after a restart —
  `Guid.CreateVersion7()` remains the rule for entities created at runtime, such as bookings.
- **Business hours are recurring wall-clock facts, not instants.** They are stored and returned as local
  times (`09:00`, `18:00`) together with the room's IANA time zone, never converted to UTC. BR-5 governs
  instants — a booking's start and end — and `docs/domain.md` is amended to say so, because converting
  an opening time to UTC would silently shift it on daylight-saving days.
- Out of scope: bookings, availability search, BR-1…BR-9, pagination, filtering, sort options,
  OpenAPI/Swagger UI, logging infrastructure beyond framework defaults, containers, deployment.

## Acceptance criteria

- [ ] AC-1 — `./scripts/check` runs format, build and test steps (it is no longer a no-op) and exits
      green; the same command is green in CI on the pull request.
- [ ] AC-2 — Every forbidden dependency FD-1…FD-6 is expressed as a pure inspection rule, and each rule
      is tested twice: it passes on the real code and detects a violation. A rule that has only ever
      been seen to pass is not evidence that it works. Detection is proven against a synthetic input
      *and*, for the rules that read compiled output, against an assembly that breaks them on purpose —
      otherwise a broken metadata reader would keep every rule silently green.
- [ ] AC-3 — Listing rooms returns `200` with all three seeded rooms, each carrying an identifier, a
      name, a capacity, an IANA time zone, an opening time and a closing time as local `HH:mm` values,
      serialised in `camelCase`.
- [ ] AC-4 — Ordering is deterministic: rooms come back sorted by name using culture-independent
      (ordinal) comparison, identically on every call and on every machine.
- [ ] AC-5 — With no rooms in storage, listing returns `200` with an empty array — never `404`.
- [ ] AC-6 — A Room with a capacity below 1 is rejected at construction.
- [ ] AC-7 — A Room with a time zone the system cannot resolve is rejected at construction.
- [ ] AC-8 — A Room whose opening time is not before its closing time is rejected at construction.
- [ ] AC-9 — Room identifiers are stable across restarts, so a client may store one and use it later.
      The published identifier of each seeded room is asserted by value; "the two calls agree with
      each other" is not proof, because both could drift together.

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
