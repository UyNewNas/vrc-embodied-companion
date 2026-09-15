# Companion Interaction Contract v0.1

Status: **MVP contract**  
Owner: Issue #3  
Implementation owners: #4 (lifecycle), #5 (perception), #6 (behavior), #7 (memory), #9 (LLM plan validation)

This document freezes the boundary between **perception**, **relationship state**, **behavior execution**, and optional **model-generated plans** for the first embodied companion prototype.

The contract is intentionally small enough to implement in UdonSharp without making the LLM the center of the system.

## 1. Invariants

1. **The world owns the body.** A model may request an action; only world-side code may authorize and execute it.
2. **Consent and platform state outrank generation.** A plan cannot override touch/proximity preferences, lifecycle state, navigation validity, or local privacy policy.
3. **The companion works without a model.** The same perception events must be sufficient to drive a deterministic fallback policy.
4. **Raw sensing is not emotion diagnosis.** Head pose, silence, distance and movement are interaction signals only.
5. **Private state is local by default.** Synchronization is opt-in and belongs to presentation/networking adapters, not the core decision contract.
6. **Models propose memories; policy persists them.** Model output cannot directly mutate durable memory.
7. **Unknown or malformed model output fails closed.** Invalid actions are rejected or replaced by a safe fallback; they never become arbitrary Animator/NavMesh commands.

## 2. Data flow

```text
VRChat player/tracking state
          |
          v
   PerceptionAdapter
          |
          v
 PerceptionSnapshot + InteractionEvent
          |
          +--------------------+
          |                    |
          v                    v
 RelationshipState        DeterministicPolicy
          |                    |
          +---------+----------+
                    |
             optional LLM plan
                    |
                    v
              PlanValidator
                    |
                    v
             BehaviorCommand
                    |
                    v
             BehaviorController
        (Animator / gaze / NavMesh)
```

The optional LLM consumes normalized state. It never consumes control of the BehaviorController directly.

## 3. Identifiers and versions

Every cross-component message uses:

- `schema_version`: currently `0.1`;
- `player_id`: an opaque per-session/player identifier supplied by the world adapter; never assume it is a stable external account identifier in generic code;
- `sequence`: monotonically increasing integer within the local session;
- `timestamp_s`: local monotonic session time in seconds where available.

Wall-clock time is not required for embodiment decisions.

## 4. PerceptionSnapshot

A snapshot is a normalized, read-only view of current observable interaction state.

```json
{
  "schema_version": "0.1",
  "sequence": 128,
  "timestamp_s": 42.5,
  "player": {
    "distance_m": 0.82,
    "distance_band": "near",
    "facing": "toward_companion",
    "motion": "still",
    "head_height_delta_m": -0.08,
    "left_hand_near_head": false,
    "right_hand_near_head": true,
    "inactivity_s": 7.4
  },
  "companion": {
    "mode": "available",
    "locomotion": "idle",
    "current_action": "look_at_player"
  },
  "permissions": {
    "approach": true,
    "touch_response": true,
    "offer_hug": true
  }
}
```

### 4.1 Required normalized fields

`distance_band`:

- `contact`: < 0.35 m
- `near`: 0.35–1.2 m
- `social`: 1.2–3.0 m
- `far`: > 3.0 m

These thresholds are MVP defaults and may later become persona/player configuration. Implementations should use hysteresis around boundaries instead of switching on every frame.

`facing`:

- `toward_companion`
- `sideways`
- `away`
- `unknown`

`motion`:

- `still`
- `approaching`
- `departing`
- `moving_other`
- `unknown`

`companion.mode`:

- `restoring` — persistence/lifecycle not ready; do not make durable writes;
- `available` — normal interaction;
- `busy` — completing a non-interruptible short action;
- `disabled` — companion interaction intentionally off.

Raw values may be retained internally for debugging, but behavior policy should prefer stable normalized fields.

## 5. InteractionEvent

Events represent meaningful transitions rather than per-frame noise.

```json
{
  "schema_version": "0.1",
  "sequence": 129,
  "timestamp_s": 42.6,
  "type": "headpat_started",
  "source": "right_hand",
  "confidence": 0.93
}
```

MVP event types:

### Lifecycle

- `player_ready`
- `player_left`
- `persistence_restored`
- `companion_enabled`
- `companion_disabled`

### Spatial

- `entered_contact`
- `entered_near`
- `entered_social`
- `entered_far`
- `approach_started`
- `departure_started`

### Attention / touch

- `facing_toward_started`
- `facing_away_started`
- `headpat_started`
- `headpat_ended`

### Explicit player intent

- `intent_follow`
- `intent_stay`
- `intent_sit_with_me`
- `intent_quiet_company`
- `intent_talk`
- `intent_offer_hug_ok`
- `intent_stop_touch`

### Ambient

- `inactivity_short` — default 10 s
- `inactivity_long` — default 60 s

Sensor implementations may emit additional debug events, but unknown event types must not change persistent relationship state until explicitly registered.

## 6. RelationshipState

Relationship state describes **interaction preferences and continuity**, not a hidden score of how much the player "deserves" affection.

```json
{
  "schema_version": "0.1",
  "familiarity": 0.25,
  "preferred_distance": "near",
  "touch_preference": "allowed",
  "comfort_style": "quiet",
  "follow_preference": "ask",
  "last_intent": "quiet_company",
  "session_interactions": 6
}
```

MVP fields:

- `familiarity`: float `[0,1]`, slow-changing; used only for gradual presentation/persona variation;
- `preferred_distance`: `contact | near | social | far`;
- `touch_preference`: `unknown | allowed | avoid`;
- `comfort_style`: `unknown | quiet | conversational | playful`;
- `follow_preference`: `ask | allowed | avoid`;
- `last_intent`: most recent explicit interaction intent;
- `session_interactions`: non-persistent counter useful for fallback behavior.

### 6.1 Mutation rules

- Explicit player intent may immediately update the matching session preference.
- Durable preference writes are delegated to the MemoryAdapter and #7 policy.
- A model may **propose** a memory/preference update but cannot write this object directly.
- Sensor events alone must not set intimate preferences. For example, proximity does not imply touch consent.
- `familiarity` may increase from completed interactions, but must not unlock actions forbidden by permission fields.

## 7. BehaviorCommand

The BehaviorController accepts only commands from a bounded registry.

```json
{
  "schema_version": "0.1",
  "action": "sit_near",
  "priority": "normal",
  "duration_s": 20,
  "target_distance_m": 0.9,
  "gaze": "soft_track"
}
```

Allowed MVP actions:

- `idle`
- `look_at_player`
- `look_away`
- `approach`
- `keep_distance`
- `sit_near`
- `follow`
- `stay`
- `react_headpat`
- `offer_hug`
- `wave`
- `sleep_idle`

Allowed gaze modes:

- `none`
- `brief`
- `soft_track`
- `track`
- `look_away`

Allowed priorities:

- `safety`
- `explicit_intent`
- `interaction`
- `normal`
- `idle`

### 7.1 Validation

Before execution, the controller validates:

1. companion mode permits the action;
2. permission/preference gates permit the action;
3. target/distance values are finite and within configured world limits;
4. required NavMesh/path/seat target exists;
5. cooldown/hysteresis permits transition;
6. the action exists in the local registry;
7. duration is clamped to the action's configured maximum.

The controller returns one of:

- `accepted`
- `clamped`
- `rejected_permission`
- `rejected_state`
- `rejected_navigation`
- `rejected_unknown_action`
- `fallback_applied`

## 8. Model BehaviorPlan envelope

The provider-neutral model boundary is a **proposal envelope**, not a command channel.

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
  "memory_proposals": [
    {
      "class": "preference",
      "key": "comfort_style",
      "value": "quiet",
      "reason": "explicit player intent"
    }
  ]
}
```

Allowed `style` values for v0.1:

- `neutral`
- `gentle`
- `quiet`
- `playful`

`style` is presentation metadata, not a claim about the player's emotion.

### 8.1 Model authority limits

A BehaviorPlan may not:

- create arbitrary action names and expect them to execute;
- directly set Animator parameters, transforms, URLs, or NavMesh destinations;
- override `touch_preference`, `approach`, `offer_hug`, or other permission gates;
- directly mutate persisted memory;
- request network synchronization;
- request an action when `companion.mode = disabled`.

Malformed or unsupported plans are discarded and deterministic policy continues.

The machine-readable v0.1 envelope is in `schemas/behavior-plan.v0.1.schema.json`. Issue #9 owns future schema tightening and provider/gateway validation implementation.

## 9. Arbitration order

When multiple events/plans compete, use the following precedence:

1. **safety / disable / stop-touch**;
2. **lifecycle and persistence readiness**;
3. **explicit player intent**;
4. **active physical interaction** such as headpat;
5. **existing short non-interruptible action**;
6. **validated model proposal**;
7. **deterministic ambient policy**;
8. **idle behavior**.

A lower layer never cancels a higher-priority gate.

## 10. Deterministic fallback policy v0.1

This policy is deliberately simple. It proves the contract is usable with no LLM connection.

| Condition/event | Guard | Command |
|---|---|---|
| `intent_stop_touch` | always | stop touch response; `keep_distance` if in contact |
| `headpat_started` | touch allowed | `react_headpat` |
| `intent_sit_with_me` | approach allowed + seat/path valid | `sit_near` |
| `intent_follow` | follow not avoided + path valid | `follow` |
| `intent_stay` | always | `stay` |
| `intent_quiet_company` | approach allowed | `sit_near` + `gaze=brief` |
| player enters `near` | available | `look_at_player` briefly |
| `inactivity_short` while near | available | remain/return `idle`; do not force dialogue |
| `inactivity_long` while near | available | `sleep_idle` or persona idle |
| player departs while follow enabled | path valid | `follow` |
| invalid/no action | always | `idle` |

This fallback is not intended to be emotionally clever. It is intended to remain coherent, safe, testable, and available when inference fails.

## 11. State-machine transition rules

The BehaviorController should implement explicit transition rules instead of accepting arbitrary action replacement every frame.

Minimum rules:

- `disabled -> *` is forbidden until `companion_enabled`;
- `restoring -> durable-write` is forbidden until `persistence_restored`;
- `react_headpat` may interrupt `idle`, gaze, and locomotion presentation, but not `disabled`;
- `intent_stop_touch` interrupts `react_headpat` and `offer_hug` immediately;
- `stay` cancels follow locomotion;
- locomotion commands require a valid path before Animator transition;
- identical repeated commands inside their cooldown window are coalesced.

Exact cooldowns live in implementation/persona configuration, not in this protocol.

## 12. Local vs social scope

Core perception, RelationshipState, model context, and arbitration are **local/private by default**.

The presentation layer may later expose two modes:

- `private`: only the owning player receives the companion presentation where the platform implementation permits it;
- `social`: selected presentation state is synchronized intentionally.

The contract never implicitly synchronizes raw perception or private memory.

The cheapest reliable private-visual/audio implementation remains an experiment in #2/#4.

## 13. Acceptance tests for this contract

Implementation work can treat the contract as satisfied when these deterministic tests pass:

1. **No-model headpat:** `headpat_started` + touch allowed -> `react_headpat`.
2. **Consent wins:** `headpat_started` + touch avoided -> no touch reaction.
3. **Stop is immediate:** `intent_stop_touch` interrupts an active touch/hug behavior.
4. **Quiet means quiet:** `intent_quiet_company` may choose `sit_near` but does not force speech.
5. **Malformed plan fails closed:** unknown model action -> fallback policy, no Animator/NavMesh arbitrary command.
6. **Explicit intent beats model:** `intent_stay` beats a concurrent model `follow` proposal.
7. **Restore gate:** no durable state mutation before `persistence_restored`.
8. **Offline continuity:** removing all model responses still leaves approach/gaze/headpat/quiet-company behavior functional.

These are specification tests now; #5/#6 own their runtime implementation.

## 14. Versioning

`0.1` is frozen for the MVP prototype.

Compatible additions may add optional fields or registered enum values only when older consumers safely ignore/reject them. Any semantic change to required fields, action meaning, arbitration, or permission behavior requires a new contract version.
