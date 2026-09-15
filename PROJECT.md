# Project Board

This file mirrors the intended GitHub Projects board until an actual board is created.

## NOW

| Item | Type | Exit condition |
|---|---|---|
| Platform capability matrix | Research | Current VRChat constraints verified |
| Minimal Unity/VRC SDK project | Engineering | World builds and uploads |
| Companion interaction contract | Design | Sensors/actions schema frozen for MVP |
| Per-player companion prototype | Engineering | Two players each receive independent companion state |

## NEXT

| Item | Type | Exit condition |
|---|---|---|
| Headpat / proximity / gaze | Engineering | Stable local sensing |
| Behavior state machine | Engineering | Companion behaves coherently without LLM |
| Persistence schema | Engineering | Preferences survive re-entry |
| LLM gateway RFC | Research | Transport path selected |
| Structured behavior plan | AI | Model output drives dialogue + animation |

## LATER

| Item | Type |
|---|---|
| Public demo world | Product |
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
