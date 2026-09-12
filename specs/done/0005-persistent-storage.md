# Spec 0005 — Persistent storage

- Status: Shipped
- Mode: lite (from AGENTS.md at creation time)
- Plan: `specs/plans/0005-plan.md`

## Intent

ADR-0001 claims that replacing the in-memory store with a database is one adapter's work and touches
no rule. Nobody has tested that claim, so today it is an aspiration with a good argument behind it.
This slice tests it — and losing the test would be more informative than passing it.

Everything the product does today it must still do after the swap, with one addition a user would
notice immediately: **a booking survives a restart.** Right now stopping the process throws away
every reservation in the office.

The measure of success here is unusual: it is what **does not** change. The domain and application
projects should come out of this slice byte-identical, and every existing test should pass without
being touched. A change in either place is a finding about ADR-0001, not a task to be quietly done.

Deliberately not in this slice: authentication, multi-office, reporting, connection resilience and
retries, performance tuning, and any change to the HTTP contract.

## Requirements

- A booking made before a restart is still there afterwards, and so is its identifier.
- Rooms still exist on a fresh installation with the identifiers they already publish, so a client
  that stored one still finds its room.
- Every rule behaves exactly as it does today — BR-1 through BR-9 — and the HTTP contract is
  unchanged: the same routes, the same bodies, the same codes and statuses.
- Two callers racing for the same window still produce exactly one booking. The guarantee moves from
  a lock inside one process to the database, which is the only place it can live once more than one
  process serves the API.
- **The cancellation gap recorded in `docs/architecture.md` is closed:** BR-9's judgement and the
  removal happen as one indivisible step against a freshly read instant, rather than three steps with
  a window between them.
- Cancelling still frees the room immediately, and the availability search still never proposes a
  window that a booking request would then refuse.
- A fresh installation needs no manual setup step: starting the application is enough to have a usable
  store, and `./scripts/check` needs no database service running.

## Constraints & out of scope

- **`RoomBook.Domain` and `RoomBook.Application` do not change.** This is the experiment, not a
  convenience: if either must change, that is the finding and it belongs in the report rather than in
  a quiet commit. *(Outcome: both changed, in exactly two places — a rehydration entry point in the
  domain and the instant on the cancellation port. AC-2 therefore fails on purpose and stays failed;
  ADR-0005 carries the reasoning.)*
- FD-6 in `docs/architecture.md` bans EF Core and any ORM "in V1", and `docs/security.md` requires an
  ADR for any new dependency. Both are decided explicitly at the plan gate and recorded in an ADR —
  this slice cannot proceed by treating them as formalities.
- Existing tests are not rewritten to fit the new store. They may gain setup, but an assertion that
  has to change is a behaviour change, and behaviour is what this slice promises not to touch.
- The in-memory adapter's future is decided at the gate: kept as a test double, or deleted so that
  one store cannot drift from the other.
- **SQLite through `Microsoft.Data.Sqlite`, with hand-written SQL.** One small first-party dependency,
  no service to install, real transactions — and FD-6's ban on ORMs stays exactly as written, because
  a driver is not an ORM. The dependency itself still needs an ADR.
- **The in-memory adapter is deleted.** Two stores mean two behaviours to keep in sync, and a reviewer
  would rightly ask which one the tests prove.
- **Atomicity comes from conditional writes**: the insert happens only if nothing overlaps, the delete
  only if the booking has not started. The rules stay in the domain; the SQL condition is how the
  decision becomes indivisible, and when it refuses, the adapter reports the code the domain would.
- **The experiment's first result, found while planning:** making BR-2 atomic needs no change to the
  port, because an overlap is judged from the candidate alone. Making BR-9 atomic **does** — the
  conditional delete needs the current instant, the port does not carry it, and an adapter reading the
  clock itself would break FD-3. So `RoomBook.Application` changes by exactly one signature, and
  ADR-0001's claim — "one adapter's work, nothing else changes" — turns out to hold for reads and
  writes but not for moving an invariant into the store. That is recorded in this slice's ADR rather
  than smoothed over; a claim that survives every test was never a claim worth making.
- Out of scope: authentication, multi-office, migrations beyond creating the schema, retries and
  connection resilience, query tuning, and any new endpoint.

## Acceptance criteria

- [ ] AC-1 — A booking created before the host stops is returned by `GET /bookings/{id}` after a new
      host starts against the same store.
- [ ] AC-2 — `git diff main...HEAD -- src/RoomBook.Domain` is **empty**: no rule moves, changes or
      learns about storage. In `RoomBook.Application` the **only** permitted change is the storage
      port carrying the current instant, which is the finding described below — anything else there
      fails this criterion.
- [ ] AC-3 — Every test that exists today passes with its assertions unchanged.
- [ ] AC-4 — Concurrent identical create requests produce exactly one booking, enforced by the store:
      with the process-level lock gone, a parallel test still yields one `201` and the rest `409`.
- [ ] AC-5 — Cancelling is one indivisible step: a booking whose start has passed is refused with
      `409` even when the decision and the write are attempted together, and the implementation has no
      read-then-write window in which the start could pass.
- [ ] AC-6 — The three seeded rooms keep the identifiers they publish today, on a store created from
      nothing.
- [ ] AC-7 — Every endpoint answers exactly as before: the existing HTTP tests are the proof, and they
      run against the real store rather than a substitute.
- [ ] AC-8 — `./scripts/check` passes with no database service installed or running, and a fresh
      checkout needs no setup command.
- [ ] AC-9 — The availability search's promise still holds: every proposed window is accepted by a
      booking request.
- [ ] AC-10 — Tests do not share a store: each one starts from a known state and leaves nothing behind
      that another test can see.
- [ ] AC-11 — Restarting twice does not duplicate the seeded rooms.

## Definition of Done
- [x] Every acceptance criterion mapped to proof — including **AC-2, whose proof is that it fails**:
      the two diffs are in the pull request and the reasoning is in ADR-0005
- [x] `scripts/check` green — 207 tests, with no database service installed
- [x] Independent review done; real findings fixed, the rest accepted with written rationale — one
      review round in a separate read-only session, which found a real defect
- [x] Docs / ADRs updated — ADR-0005, the module table, the FD-6 clause, the closed known limit, and
      the out-of-scope lists that had gone stale
- [x] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 1 — a note recording the outcome next to the constraint it contradicts. No criterion was loosened: AC-2 is left failed rather than rewritten to match what happened |
| Fix rounds | 1 |
| Review findings | 11: one major, six minor, four nits. 7 fixed, 4 accepted with written rationale, 0 rejected as noise |
| Regressions introduced | 0 — all 201 inherited tests passed against the new store with their assertions untouched |
| Bugs escaped to production | 0 — not deployed. One defect reached the pull request and was caught by review (below) |

## The defect this slice produced, and why it matters

`RemoveAsync` read "zero rows deleted" as "the booking has started", when it also means "somebody
else deleted it first" — in which case the truthful answer is `404`. **The cause was not a missing
insight: the plan already said to ask again in the same transaction, and the code skipped it.** I had
even written "reporting not found here would be cheaper and untrue" in a comment while doing the
mirror image of that.

Worth recording because it is the failure mode a plan cannot prevent on its own, and the reason the
producer does not review its own work.

## What the experiment answered

ADR-0001's claim held for all nine business rules, all six endpoints and all 201 existing tests, and
failed in exactly two structural places — a rehydration entry point in the domain, and the current
instant on the cancellation port. Both are recorded in ADR-0005 with the alternatives that were
rejected and why. The reasoning in ADR-0001 was sound; its scope was a little too confident.
