---
name: implement-requirement
description: Implement one requirement or milestone slice end to end (backend, client, frontend, tests) from docs/SRS.md and docs/PLAN.md.
argument-hint: "[FR-ID or milestone, e.g. FR-SUB-007 or M5.2]"
disable-model-invocation: true
---

# Implement $ARGUMENTS

1. Read `$ARGUMENTS` in `docs/SRS.md` (or its line in `docs/PLAN.md`) plus every requirement it references. Read the `hrs-domain` skill.
2. Open the matching mockup listed in `docs/design/README.md`.
3. Write a plan in the chat before editing:
   - acceptance criteria (Given/When/Then), including permission and window edge cases
   - backend changes (domain, use cases, endpoints, migration)
   - frontend changes (routes, components, states)
   - tests you will add
   - open questions — if any business rule is unclear, ask and wait
4. Implement the backend with the `backend-feature` skill, then `api-sync`, then the UI with the `frontend-feature` skill.
5. Run the `verify` skill until it is green.
6. Tick the matching boxes in `docs/PLAN.md`.
7. Commit with a Conventional Commit message that cites the IDs, e.g. `feat(observer): pull figures from Muqamaat [FR-SUB-007, FR-OBS-005]`.
8. End with a short summary: what changed, how it was tested, anything deferred.
