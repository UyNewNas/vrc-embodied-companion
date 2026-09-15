# Roadmap

## Phase 0 — Feasibility and constraints

**Exit criterion:** architecture decisions are grounded in current VRChat SDK capabilities.

- [ ] Build a VRChat capability matrix: Udon, PlayerObject, Persistence, AI Navigation, networking, external URLs.
- [ ] Reproduce the smallest possible outbound LLM request path.
- [ ] Benchmark practical request cadence and latency.
- [ ] Document what is impossible or fragile in pure Udon.
- [ ] Decide the boundary between world-side logic and optional gateway-side logic.

## Phase 1 — Embodiment MVP

**Exit criterion:** a companion can exist meaningfully without an LLM.

- [ ] Per-player companion lifecycle.
- [ ] Follow / stay / approach / sit state machine.
- [ ] Gaze and head tracking.
- [ ] Personal-space model.
- [ ] Headpat and touch detection.
- [ ] Idle behavior and silence behavior.
- [ ] Basic animation contract.
- [ ] Debug overlay for perceived player state.

## Phase 2 — Relationship state and persistence

**Exit criterion:** leaving and returning feels continuous.

- [ ] Define local player profile schema.
- [ ] Define relationship state schema.
- [ ] Persist stable preferences.
- [ ] Distinguish episodic memory from durable preference memory.
- [ ] Add user-facing “forget me / reset relationship” control.
- [ ] Add memory budget and migration/versioning strategy.

## Phase 3 — LLM dialogue adapter

**Exit criterion:** dialogue changes the companion's words *and* body.

- [ ] Define provider-neutral request/response schema.
- [ ] Gateway proof of concept.
- [ ] Structured behavior-plan output.
- [ ] Contextual response-intent UI.
- [ ] Dialogue-to-animation mapping.
- [ ] Graceful fallback when the model or network is unavailable.
- [ ] Latency instrumentation.

## Phase 4 — Demo world

**Exit criterion:** a normal VRChat player can experience the system without installing anything.

- [ ] Small private-room scene.
- [ ] Companion onboarding.
- [ ] Sitting / walking / quiet-company interactions.
- [ ] Relationship reset UI.
- [ ] Clear AI disclosure.
- [ ] Public-world performance testing.
- [ ] Abuse / griefing behavior tests.

## Phase 5 — Creator package / VPM

**Exit criterion:** another world author can integrate the companion without editing core code.

- [ ] VPM package metadata.
- [ ] Drop-in prefab.
- [ ] Persona configuration asset.
- [ ] Animation mapping configuration.
- [ ] World integration guide.
- [ ] Example scene.
- [ ] Package validation and release automation.

## Phase 6 — Free-form interaction research

These are deliberately separate from the MVP.

- [ ] Runtime free-text transport that remains platform-compliant.
- [ ] Voice input feasibility.
- [ ] STT architecture.
- [ ] TTS playback architecture.
- [ ] Lip sync.
- [ ] Speaker / addressee disambiguation in multi-user rooms.
- [ ] Multimodal affect inference without invasive surveillance.

## Phase 7 — Cross-world identity and cloud memory

Only after the single-world product is good.

- [ ] Portable companion identity.
- [ ] User-controlled cloud account.
- [ ] Encrypted memory store.
- [ ] Consent model for worlds importing memory.
- [ ] Export / delete user data.
- [ ] Cost controls and quotas.
