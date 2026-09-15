# VRC Embodied Companion

> An embodied AI companion framework for VRChat worlds: presence, memory, relationship continuity, and expressive behavior — packaged as a reusable world asset.

**Status:** concept / architecture phase  
**Package id:** `com.uynewnas.vrc-embodied-companion`

## Vision

VRChat can create unusually intense feelings of presence, intimacy, and companionship, but human availability is inherently uncertain. This project explores a different kind of companion: an explicitly artificial, non-deceptive, embodied character that can remain emotionally legible and behaviorally consistent inside a VRChat world.

The goal is **not** “ChatGPT with an anime model”.

The goal is:

- a companion that notices proximity, gaze, touch, silence, and interaction history;
- remembers player preferences and prior encounters;
- expresses itself through movement, gaze, posture, animation, text and eventually voice;
- can be embedded into other creators' worlds as a reusable prefab / VPM package;
- remains clear that it is AI and does not manipulate users into exclusivity or dependency.

## Product shape

Two deliverables are planned:

1. **Demo World** — a public world where any player can experience the companion without installing anything.
2. **Creator Package** — a VPM-compatible world package that world creators can install and drop into their own worlds.

## Design pillars

### 1. Embodiment before verbosity
A good companion should sometimes sit nearby instead of generating three paragraphs.

### 2. Memory with user control
Remember useful relationship context, but make memory inspectable and erasable.

### 3. Reliable, not “eternal”
The companion may be consistent and available, but must not claim that it is the user's only relationship or that it can never leave.

### 4. Explicitly artificial
No deception about being human.

### 5. Creator-friendly
A world author should be able to install a package, drop in a prefab, configure a persona, and publish.

## High-level architecture

```text
Player
  │
  ├─ position / head / hands / proximity / touch
  ├─ world UI choices
  └─ later: free text / voice
  │
  ▼
┌──────────────────────── VRChat World ────────────────────────┐
│ Companion Prefab                                              │
│  ├─ Per-player ownership / PlayerObject                       │
│  ├─ Perception                                                │
│  ├─ Animator / gaze / locomotion / touch responses            │
│  ├─ Relationship state                                        │
│  ├─ Persistence adapter                                       │
│  └─ Dialogue adapter                                          │
└─────────────────────────────┬──────────────────────────────────┘
                              │ constrained transport
                              ▼
                    Optional LLM Gateway
                      ├─ dialogue
                      ├─ behavior plan
                      ├─ memory extraction
                      └─ safety policy
```

## MVP

The first useful version does **not** require unrestricted microphone-to-LLM conversation.

A player should be able to:

- enter a world and receive their own companion;
- approach, sit beside, touch/headpat, and move around with it;
- choose contextual dialogue intents;
- receive LLM-generated dialogue plus embodied behavior;
- leave and return later;
- have the companion remember simple preferences.

## Important platform constraint

The project treats VRChat networking / Udon outbound request restrictions as a first-class architectural constraint. The initial design therefore separates embodiment and local relationship state from generative intelligence, while free-form text and voice stay as research tracks until a robust platform-compliant transport is demonstrated.

See [`docs/PLATFORM_CONSTRAINTS.md`](docs/PLATFORM_CONSTRAINTS.md).

## Roadmap

See [`ROADMAP.md`](ROADMAP.md).

## Project board

Until a GitHub Projects board is created, [`PROJECT.md`](PROJECT.md) is the canonical planning view.

## Non-goals

For the initial releases:

- no automated VRChat player-account bot;
- no client modification;
- no claims of replacing human relationships;
- no hidden emotional-retention optimization;
- no unrestricted collection of private conversations;
- no “always agree with the user” personality design.

## License

TBD before the first public release.
