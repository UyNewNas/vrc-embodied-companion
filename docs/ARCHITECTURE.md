# Architecture

## Core principle

The world owns the **body**.  
The model owns suggestions about **language and behavior**.  
Neither should be able to silently override the other.

The normative MVP boundary between perception, relationship state, model proposals and executable behavior is [`INTERACTION_CONTRACT.md`](INTERACTION_CONTRACT.md). The machine-readable model envelope begins at [`../schemas/behavior-plan.v0.1.schema.json`](../schemas/behavior-plan.v0.1.schema.json).

## World-side components

### CompanionLifecycle
Creates or assigns a companion instance to a player.

Responsibilities:
- ownership;
- respawn/recovery;
- player join/leave;
- persistence readiness;
- local visibility rules.

The platform audit selected `VRCPlayerObject` as the default per-player state/lifecycle primitive. The private-vs-social presentation pattern still requires the two-client experiment in #2/#4.

### Perception
Normalizes world-observable signals into a small schema.

Initial signals:
- player distance and distance band;
- relative facing direction;
- head height delta;
- hand proximity;
- touch/headpat events;
- locomotion state;
- silence / inactivity duration;
- current interaction mode.

Avoid pretending these signals reveal hidden emotion with certainty. Runtime work in #5 should emit the normalized snapshot/events defined by the interaction contract rather than exposing raw tracking data directly to higher-level policy.

### RelationshipState
Stores slow-changing interaction preferences and continuity state.

MVP fields include:
- familiarity;
- preferred distance;
- touch preference;
- comfort style;
- follow preference;
- recent explicit intent;
- session interaction count.

RelationshipState is not a hidden affection/reward score. Permission fields always outrank familiarity or model suggestions.

### BehaviorController
Executes safe world-side behavior from a bounded action registry.

MVP actions:
- idle;
- look_at_player;
- look_away;
- approach;
- keep_distance;
- sit_near;
- follow;
- stay;
- offer_hug;
- react_headpat;
- wave;
- sleep_idle.

Every requested action passes world-side validation for lifecycle state, permission gates, navigation validity and cooldown/hysteresis before Animator/NavMesh execution.

### DeterministicPolicy
Provides coherent companion behavior even when no model/gateway is available.

Examples:
- headpat + touch allowed -> `react_headpat`;
- quiet-company intent -> `sit_near` without forced dialogue;
- stay intent -> cancel follow locomotion;
- malformed/unknown model action -> safe fallback rather than arbitrary execution.

This component is the core implementation target for #6.

### MemoryAdapter
Separates ephemeral turn context, session state, durable preferences, and optional episodic summaries. Models may propose memory writes, but policy decides whether anything is persisted. #7 owns the concrete persistence schema and reset semantics.

### DialogueAdapter
Accepts the provider-neutral `BehaviorPlan` proposal envelope.

```json
{
  "schema_version": "0.1",
  "request_id": "local-42",
  "speech": "嗯，那今晚就安静坐一会儿。",
  "style": "gentle",
  "behavior": {
    "action": "sit_near",
    "duration_s": 20,
    "target_distance_m": 0.9,
    "gaze": "brief"
  },
  "memory_proposals": []
}
```

The model does not receive direct Animator parameters, transforms, network synchronization controls, or persistence authority.

## Arbitration

Core precedence is:

1. safety / disable / stop-touch;
2. lifecycle and persistence readiness;
3. explicit player intent;
4. active physical interaction;
5. short non-interruptible action;
6. validated model proposal;
7. deterministic ambient policy;
8. idle behavior.

This is defined normatively in the interaction contract so #5, #6 and #9 do not invent competing priority systems.

## Optional gateway

Responsibilities:
- LLM provider abstraction;
- prompt construction;
- structured output validation;
- rate limiting;
- moderation / safety policy;
- cost control;
- optional server-side memory in later phases.

The gateway must not become mandatory for basic embodied behavior.

## Failure model

```text
LLM unavailable / invalid output
  ↓
PlanValidator rejects or times out
  ↓
DeterministicPolicy
  ↓
BehaviorController
  ↓
player receives continuity instead of dead silence
```

A companion that freezes whenever an API fails is not a companion.

## Multi-user design

MVP recommendation: **one logical companion per player**, with raw perception, relationship state, model context and arbitration local/private by default.

Presentation can later be configured as:
- `private`: owner-only presentation where the verified platform pattern permits it;
- `social`: selected presentation state intentionally synchronized.

Still-unresolved implementation questions belong to #2/#4:
- cheapest reliable owner-only renderer/audio gating;
- whether other players may interact with a social companion body;
- which presentation fields, if any, are synchronized;
- rejoin/ownership behavior under two real VRChat clients.
