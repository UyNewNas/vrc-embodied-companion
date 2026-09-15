# Project Board

This file mirrors the intended GitHub Projects board until an actual board is created.

## DONE

| Item | Type | Result |
|---|---|---|
| Platform capability matrix (#1) | Research | Verified current VRChat constraints; unresolved product-specific behavior converted into explicit experiments |
| Companion interaction contract (#3) | Design | Frozen v0.1 perception/events, RelationshipState, bounded actions, arbitration, offline fallback, and provider-neutral BehaviorPlan envelope |

## NOW

| Item | Type | Exit condition |
|---|---|---|
| Minimal Unity/VRC SDK project (#2) | Engineering | World builds and passes one-client + two-client Build & Test acceptance |
| Per-player companion prototype (#4) | Engineering | Design is frozen in `docs/PER_PLAYER_LIFECYCLE.md`; runtime remains open until two-client evidence proves independent companion state |
| Private/social presentation experiment (#11) | Research | Two-client evidence identifies the MVP owner-only/private presentation strategy and social-mode boundary |
| Two-client lifecycle matrix (#12) | Test | Execute PlayerObject ownership, restore-gate, isolation, rejoin, leave, and no-cloud assertions in VRChat Build & Test |
| Headpat / proximity / gaze (#5) | Engineering | Algorithm/spec and schemas are frozen; draft PR #15 contains an uncompiled perception prototype; merge only after #2 + #13 runtime evidence |
| Perception calibration matrix (#13) | Test | VR/Desktop/avatar-scale Build & Test evidence validates or adjusts v0.1 thresholds; blocked on #2 |
| Behavior state machine (#6) | Engineering | Deterministic policy/spec and BehaviorCommand schema are frozen; draft PR #16 contains an uncompiled controller; execute the 14 acceptance vectors after #2 |
| Persistence schema (#7) | Engineering | Native v1 schema/privacy/reset/migration rules are frozen in `docs/PERSISTENCE_SCHEMA.md`; prove opt-in, restore, reset and future-schema safety in a runnable #2 world |
| LLM gateway RFC (#8) | Research | MVP transport selected in `docs/LLM_TRANSPORT_RFC.md`: bounded stateless editor-time URL matrix + live/static/local fallback; keep open until #18 proves it in a real world |
| LLM transport runtime matrix (#18) | Test | Prove serialized routes, >=6s gate, stale-response rejection, untrusted-host failure, trusted static fallback, and offline fallback; blocked on #2 |

## NEXT

| Item | Type | Exit condition |
|---|---|---|
| Structured behavior plan (#9) | AI | v0.1 schema is validated at the gateway/world boundary and drives only authorized actions |
| Remote/social perception regime (#14) | Research | Remote bone-derived tracking is calibrated separately or touch classification stays disabled for social mode |

## LATER

| Item | Type |
|---|---|
| Public demo world (#10) | Product |
| VPM package | Distribution |
| Free-form text | Research |
| Voice/STT/TTS | Research |
| Cross-world/private memory | Backend |
| Companion marketplace / persona packs | Ecosystem |

## Current architecture decisions

- Native VRChat persistence is for **bounded low-sensitivity preferences**, not intimate transcripts or psychological profiles.
- PlayerObject + `VRCEnablePersistence` is the default package-local durable carrier for v1; PlayerData remains available but is not required.
- Durable preference memory is opt-in for a new player. Reset means overwrite-to-neutral + disable durable writes; the world API must not claim physical backend deletion it cannot perform.
- PlayerObject ownership/state isolation does not prove private presentation; #11 remains a separate two-client experiment.
- Current documented Udon networking does not provide a general arbitrary-text HTTP POST path. MVP live inference therefore uses **bounded stateless GET routes represented by editor-time `VRCUrl` values** rather than pretending free-form prompts can be encoded at runtime.
- The LLM gateway receives coarse intent/context codes only; free-form transcript and intimate relationship history remain local/out of scope for transport v0.1.
- Transport fallback order is **live gateway -> trusted static response pack -> deterministic local behavior**. A custom live host may require the player to enable Allow Untrusted URLs; the companion must still function without it.

## Decision rules

A task enters **NOW** only if it unblocks the MVP.

A feature that requires unproven platform behavior belongs in **Research**, not in the critical path.

The MVP is considered successful if a player can spend 15–20 quiet minutes with the companion and perceive:
1. presence,
2. responsiveness,
3. continuity,
4. non-repetitive behavior,
without needing unrestricted free-form voice.
