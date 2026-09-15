# Architecture

## Core principle

The world owns the **body**.  
The model owns suggestions about **language and behavior**.  
Neither should be able to silently override the other.

## World-side components

### CompanionLifecycle
Creates or assigns a companion instance to a player.

Responsibilities:
- ownership;
- respawn/recovery;
- player join/leave;
- local visibility rules.

### Perception
Normalizes world-observable signals into a small schema.

Initial signals:
- player distance;
- relative facing direction;
- head height;
- hand proximity;
- touch/headpat events;
- locomotion state;
- silence / inactivity duration;
- current interaction mode.

Avoid pretending these signals reveal hidden emotion with certainty.

### RelationshipState
Stores slow-changing interaction state.

Example fields:
- familiarity;
- preferred distance;
- touch permissions/preferences;
- comfort style;
- recent interaction timestamps;
- relationship phase.

### BehaviorController
Executes safe world-side behavior.

Possible actions:
- idle;
- look_at_player;
- look_away;
- approach;
- keep_distance;
- sit_near;
- follow;
- offer_hug;
- react_headpat;
- wave;
- sleep_idle.

The LLM requests actions; the controller validates them.

### MemoryAdapter
Separates ephemeral turn context, session state, durable preferences, and optional episodic summaries.

### DialogueAdapter
Accepts a provider-neutral structured response.

```json
{
  "speech": "...",
  "emotion": "gentle",
  "action": "sit_near",
  "gaze": "player",
  "memory_writes": []
}
```

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
LLM unavailable
  ↓
world-side fallback persona
  ↓
simple embodied response
  ↓
player receives continuity instead of dead silence
```

A companion that freezes whenever an API fails is not a companion.

## Multi-user design

Questions to solve:
- Is the companion private to a player or socially visible?
- Can other players touch it?
- Who hears its dialogue?
- How are conflicting interactions prioritized?
- Can one companion follow a group?

MVP recommendation: **one logical companion per player**, with local/private interaction where platform behavior allows it.
