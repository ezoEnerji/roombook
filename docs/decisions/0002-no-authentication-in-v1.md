# ADR 0002 — No authentication in V1; JWT bearer is the target posture

- Status: Accepted
- Date: 2026-09-12

## Context

V1 was scoped as a single-office, in-memory booking API with no authentication. During bootstrap JWT
bearer authentication was initially picked as the security posture, which contradicted that scope, so
the conflict was resolved explicitly rather than silently.

Adding identity in V1 is not a package reference. It forces decisions that reach into the domain and
the public contract: who issues tokens (an identity provider or a symmetric development key), whether
`organizer` stops being request input and becomes a token claim — an API contract change — and what the
authorisation rules are (may anyone cancel any booking, is there an administrator role). That
introduces a new actor, `User`, into a model that currently has none.

## Decision

V1 ships with no authentication and no authorisation, recorded in `docs/security.md` as an accepted
risk: the API trusts every caller, is fit only for local development and a trusted internal network,
and exposing it to the internet without authentication is a release blocker.

JWT bearer with default-deny on every endpoint is the documented target posture for V2, to be
introduced alongside persistent storage. Until then no code assumes an authenticated caller and no
partial identity model is added.

## Consequences

What it buys: the first specs can prove the conflict and availability rules — the actual product —
without doubling the spec and test surface. The domain stays free of a `User` concept that would
otherwise be invented speculatively and probably wrongly. The risk is written down where reviewers and
future agents will read it, so it cannot be mistaken for an omission.

What it costs: the API is genuinely insecure until V2, so deployment is constrained to trusted
networks and any accidental exposure is a real incident. When identity lands, the booking contract
changes (`organizer` becomes derived), which is a breaking change for any client built against V1.
Cancellation has no ownership check, so V1 cannot be used by an untrusted user population at all.

## Alternatives considered

- **JWT in V1.** Correct end state, wrong order: it makes identity provider selection, claim mapping
  and authorisation rules blocking questions before a single business rule is proven.
- **Shared API key header.** Cheap and keeps `User` out of the domain, but it is not authentication —
  it identifies no one, so cancellation ownership still cannot be enforced. It buys a false sense of
  security while still requiring the V2 work.
- **JWT infrastructure present but disabled by configuration.** Rejected as the worst of both: untested
  security code in the repository, and a configuration flag whose "off" state is the one everybody runs.

## Revisit triggers

- Any plan to expose the API beyond a trusted network, or to a user population that is not fully trusted.
- Persistent storage is introduced (the natural moment to pair it with identity).
- A requirement appears that depends on knowing who the caller is — per-user booking lists, ownership
  checks on cancellation, audit trails, or quotas.
