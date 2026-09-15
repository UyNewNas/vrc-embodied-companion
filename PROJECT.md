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
| Headpat / proximity / gaze (#5) | Engineering | Algorithm/spec and machine-readable event schemas are frozen; implement runtime and calibrate thresholds with #13 before closing |
| Perception calibration matrix (#13) | Test | VR/Desktop/avatar-scale Build & Test evidence validates or adjusts v0.1 thresholds; blocked on #2 |
| Behavior state machine (#6) | Engineering | Deterministic v0.1 fallback policy behaves coherently without LLM |

## NEXT

| Item | Type | Exit condition |
|---|---|---|
| Persistence schema (#7) | Engineering | Preferences survive re-entry and reset semantics are implemented |
| LLM gateway RFC (#8) | Research | Transport path selected and minimally reproduced |
| Structured behavior plan (#9) | AI | v0.1 schema is validated at the gateway/world boundary and drives only authorized actions |
| Remote/social perception regime (#14) | Research | Remote bone-derived tracking is calibrated separately or touch classification stays disabled for social mode |

## LATER

| Item | Type |
|---|---|
| Public demo world (#10) | Product |
| VPM package | Distribution |
| Free-form text | Research |
| Voice/STT/TTS | Research |
| Cross-world memory | Backend |
| Companion marketplace / persona packs | Ecosystem |

## Decision rules

A task enters **NOW** only if it unblocks the MVP.

A feature that requires unproven platform behavior belongs in **Research**, not in the critical path.

The MVP is considered successful if a player can spend 15–20 quiet minutes with the companion and perceive:
1. presence,
2. responsiveness,
3. continuity,
4. non-repetitive behavior,
without needing unrestricted free-form voice.
