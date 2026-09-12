# Security

> Filled at bootstrap. Baseline rules agents must honor in every plan and review.

## Secrets
- Secrets never enter the repo, specs, prompts, or chat. `.env` is gitignored.
- Agents never print secret values, even when debugging.
- V1 has no secrets at all — no database, no identity provider, no third-party API — so no `.env`
  exists yet. The first change that introduces a configuration secret creates `.env.example`
  alongside it; until then there is deliberately nothing to template.
- Configuration comes from environment variables or `dotnet user-secrets`; `appsettings*.json` holds
  no credentials.

## Input & output
- All external input is validated at the API edge: shape, field limits, ranges and timestamp format.
  The domain independently re-validates every rejection rule (BR-1, BR-2, BR-4, BR-6, BR-7, BR-8, BR-9)
  and never trusts the caller. Purely structural checks — field lengths, `attendeeCount ≥ 1`, ISO-8601
  format — stay at the edge, because a malformed value never reaches a domain type in the first place.
- Field limits: `title` ≤ 200 characters, `organizer` ≤ 100 characters, `attendeeCount` ≥ 1 and
  ≤ `room.capacity`, `room.capacity` ≥ 1, availability queries span at most 31 days, request bodies at
  most 32 KB.
- Unknown JSON members are rejected rather than ignored, so a typo can never be silently accepted.
- CORS is disabled by default; a wildcard (`*`) origin is forbidden. Allowed origins are configuration.
- Error responses carry an error code and a short message only — never stack traces, exception type
  names, internal identifiers or configuration values (`docs/conventions.md`).
- Logging is structured and PII-free: log `bookingId`, `roomId` and the error code. Organizer names and
  free-text titles are personal data and are never logged, at any level.

## AuthN / AuthZ

**V1 has no authentication — this is an accepted risk, not an oversight.** The API trusts every caller,
so any client on the network can create or cancel any booking. It is therefore only fit for local
development and a trusted internal network. Exposing it to the internet without authentication is a
release blocker.

**Target posture (V2, ADR-0002):** JWT bearer authentication, default-deny on every endpoint. When it
lands, these decisions become required and none of them may be improvised:

- who issues tokens (identity provider vs. symmetric development key),
- whether `organizer` stops being request input and is derived from a token claim — an API contract change,
- authorisation rules: who may cancel whose booking, and whether an administrator role exists.

Until then, no code may assume an authenticated caller, and no half-built identity model is introduced.

## Dependencies
- Adding any NuGet dependency requires an ADR (`docs/decisions/`): what it buys, what it costs, what
  the alternative was. The banned list lives in `docs/architecture.md` (FD-6).
- `RoomBook.Domain` takes no packages at all (FD-1).
- Package versions are pinned; floating versions are forbidden.
- `dotnet list package --vulnerable` runs in CI and fails the build on a known advisory.
- New dependencies are checked for license, maintenance status and CVE history before the ADR is
  accepted — FluentAssertions is on the banned list precisely because of a license change.

## Review lens
Security is a mandatory dimension of every independent review (see `prompts/review.md`), not a
separate afterthought phase.
