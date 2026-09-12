# Git

> Set at bootstrap. Trunk-based with short-lived branches; loosen consciously, not accidentally.

## Branching
- `main` is the only long-lived branch and is always releasable.
- `feat/S-<nnn>-<short-name>` for new behavior — **no branch without a spec.** The `S-<nnn>` is the
  spec number in `specs/active/`.
- Fixes: `fix/S-<nnn>-<short-name>`; refactors: `refactor/S-<nnn>-<short-name>`;
  incidents: `incident/<yyyy-mm-dd>-<short-name>`.
- A small bug fix taken through `workflows/bug-fix.md` has no spec, so it has no number: it uses
  `fix/<short-name>`, and the pull request carries the **report** — expected versus actual, and how it
  was found — where a spec link would otherwise go. A fix that changes behaviour is a feature and
  gets a spec like any other.
- Branches are short-lived: rebase on `main` rather than letting a branch age.

## Commits
- Conventional Commits with the spec reference in the subject:
  `feat(booking): reject overlapping bookings (S-002)`.
- Agent commits follow the same standard: the agent writes the message, the human approves.
- A commit message describes behavior change, never "fixed review comments".

## Forbidden
- Direct commits or pushes to `main` — everything goes through a pull request.
- Force push and history rewriting on `main` or any shared branch. Undo = `git revert` (recovery R-11).
- Editing files under `specs/done/` — shipped specs are immutable (invariant 7). A PR that touches them
  is rejected.
- Merging with a red or unrun `scripts/check`.

## Pull requests
- PR template checklist completed; `scripts/check` green in CI; **squash-merge** so each spec lands as
  one commit on `main`.
- Evidence of independent review (a separate session or read-only subagent working from the diff and
  the spec) is part of the PR — the producer's own confirmation does not count (invariant 3).
- The PR body maps every acceptance criterion to its proof.
