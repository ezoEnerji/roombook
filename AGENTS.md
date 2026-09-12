# AGENTS.md — Project Rules

**RoomBook** — a meeting-room booking API for one office: create and cancel bookings, detect
conflicts, and search for free slots that fit a requested duration. V1 is in-memory and anonymous.
Stack: .NET 9 · ASP.NET Core Minimal API · xUnit v3 · lightweight hexagonal (Domain → Application
→ Api). The product *is* rules **BR-1…BR-9** (`docs/domain.md`) — read them before planning — and
the boundaries **FD-1…FD-6** (`docs/architecture.md`) — read them before coding.

## Operating mode

**Mode: lite** — Spec → Plan → **[GATE: human]** → Build → Independent Review → Verify
(see `workflows/README.md`). Independent review is never skipped, in any mode.

## Invariant rules (these survive bootstrap — never delete or weaken them)

1. **No spec, no code.** Every piece of work starts as a spec in `specs/active/` (from `specs/TEMPLATE.md`).
2. **Plan before build.** A human approves the plan before any code is written.
3. **The producer never verifies its own work.** Review and QA run in a separate session or a read-only subagent, working from files (diff + spec), never from the builder's chat.
4. **Evidence over claims.** "Done" requires `scripts/check` green and every acceptance criterion mapped to proof. Never claim completion without showing evidence.
5. **Tests are protected.** Weakening asserts, deleting or skipping tests to get to green is forbidden — always.
6. **Proposal rule.** Every question, option, or finding comes with your own recommendation and rationale. The human decides; nothing is applied without approval.
7. **Shipped specs are immutable.** Files under `specs/done/` are never edited.
8. **Uncertainty is surfaced, not assumed.** On ambiguity or a docs/code conflict: stop and use the matching recovery ramp (`prompts/recovery/`).

## Where things live

| What | Where |
|---|---|
| Architecture & boundaries | `docs/architecture.md` |
| Domain language & business rules | `docs/domain.md` |
| Coding conventions | `docs/conventions.md` |
| Testing rules | `docs/testing.md` |
| Security rules | `docs/security.md` |
| Git & branching rules | `docs/git.md` |
| Decisions with rationale (ADRs) | `docs/decisions/` |
| Roles (who may do what) | `docs/roles/` |
| Specs & plans | `specs/active/` · `specs/plans/` · shipped → `specs/done/` |
| Processes & gates | `workflows/` |
| Reusable prompts & recovery ramps | `prompts/` |
| The single verification command | `scripts/check` |
