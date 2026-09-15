# Deterministic Behavior State Machine v0.1

Status: **MVP implementation contract**  
Owner: Issue #6  
Depends on: `docs/INTERACTION_CONTRACT.md`  
Runtime verification: blocked on #2

This document turns the fallback policy in the interaction contract into an explicit, testable world-side state machine. The goal is not emotional sophistication. The goal is a companion that remains coherent, interruptible, and safe when no LLM is available.

## 1. Core rule

The BehaviorController owns the body. Perception, UI intents, relationship state, and model output may **propose** behavior, but they do not directly manipulate Animator parameters, transforms, NavMesh destinations, or persistent state.

Every proposal passes through:

```text
input/event
   |
   v
normalize -> permission/state gates -> priority arbitration -> transition guard
   |                                                    |
   +------------------- rejected -----------------------+
   |
   v
BehaviorCommand -> presentation/navigation adapter -> result
```

A failed or unavailable adapter must not leave the controller in a fake active state.

## 2. Controller modes

Lifecycle mode is orthogonal to the visible action:

- `restoring`: lifecycle exists, durable state is not ready;
- `available`: normal behavior arbitration;
- `busy`: a short non-interruptible presentation is completing;
- `disabled`: no companion interaction.

`disabled` rejects all ordinary commands. `restoring` permits only local non-durable presentation and rejects durable-write side effects.

## 3. Action registry

The only executable v0.1 action identifiers are:

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

Unknown strings never reach Animator/NavMesh code.

## 4. Priority registry

Numerically larger values win arbitration. The implementation may use integers internally, but external names remain stable.

| Priority | Rank | Typical source |
|---|---:|---|
| `safety` | 500 | disable, stop-touch, emergency navigation failure |
| `explicit_intent` | 400 | follow/stay/sit/quiet/hug UI intent |
| `interaction` | 300 | active headpat, other explicit physical interaction |
| `normal` | 200 | validated model plan, spatial acknowledgement |
| `idle` | 100 | inactivity/persona idle |

Lifecycle readiness is a gate before this ranking, not another competing command.

## 5. Controller state

Minimum runtime state:

```text
mode
current_action
current_priority
current_started_at
current_min_hold_until
current_max_end_at
current_target_distance_m
current_gaze
last_transition_reason
last_result

permissions:
  approach_allowed
  touch_response_allowed
  offer_hug_allowed
  follow_mode = ask | allowed | avoid

navigation:
  path_required
  path_available
  destination_valid
```

The controller must never infer consent from proximity or familiarity.

## 6. Transition policy

### 6.1 Global interrupts

These transitions are unconditional where applicable:

| Input | Current | Result |
|---|---|---|
| `companion_disabled` | any | cancel presentation/navigation -> `idle`, then mode `disabled` |
| `intent_stop_touch` | `react_headpat` / `offer_hug` / any | stop touch presentation immediately; if in contact and approach is disallowed/avoidance requested, request `keep_distance` |
| `intent_stay` | `follow` / `approach` / other lower priority action | cancel locomotion -> `stay` |
| navigation invalidated | locomotion action | stop NavMesh movement -> safe `idle` or `stay`; result `rejected_navigation`/`fallback_applied` |

### 6.2 Explicit intents

| Event | Guards | Command |
|---|---|---|
| `intent_follow` | follow != `avoid`, navigation valid | `follow`, `explicit_intent` |
| `intent_follow` | follow == `avoid` or navigation invalid | reject; remain safe current action or `idle` |
| `intent_stay` | mode != disabled | `stay`, `explicit_intent` |
| `intent_sit_with_me` | approach allowed, seat/path valid | `sit_near`, `explicit_intent` |
| `intent_quiet_company` | approach allowed | `sit_near` with `gaze=brief`; **no forced speech** |
| `intent_offer_hug_ok` | offer_hug allowed, touch allowed | `offer_hug`, `explicit_intent` |
| `intent_stop_touch` | always | safety interrupt as above |

### 6.3 Physical interaction

| Event | Guards | Command |
|---|---|---|
| `headpat_started` | touch allowed, available | `react_headpat`, `interaction` |
| `headpat_started` | touch avoided/disallowed | no touch reaction |
| `headpat_ended` | current=`react_headpat` | return to pre-interaction safe action if still valid, otherwise `idle` |

For v0.1 the controller stores one `resume_action` only for short physical interruptions. It must not resume an action whose guard became invalid while interrupted.

### 6.4 Spatial/attention acknowledgement

| Event | Guards | Command |
|---|---|---|
| `entered_near` | available, no >=interaction command active | `look_at_player`, normal priority, brief duration |
| `facing_toward_started` | available, idle/normal action | may refresh brief gaze only; do not initiate touch or approach |
| `departure_started` | current=`follow`, path valid | keep/refresh `follow` |
| `departure_started` | follow not active | no automatic pursuit |

### 6.5 Ambient behavior

| Event | Guards | Command |
|---|---|---|
| `inactivity_short` | near/contact, available, no explicit/interaction action | `idle`; preserve presence, do not force dialogue |
| `inactivity_long` | near/contact, available, no higher-priority action | `sleep_idle` or persona idle |
| no valid proposal | available | `idle` |

Ambient events never cancel explicit player intent.

## 7. Command validation

Before any transition, validate in this order:

1. action exists in registry;
2. mode allows action;
3. permission/relationship guards allow action;
4. all numeric parameters are finite;
5. target distance is clamped to `[0.25, 4.0]` m for the MVP unless a world profile explicitly narrows it;
6. duration is clamped by action maximum;
7. navigation prerequisites are satisfied for locomotion actions;
8. cooldown/coalescing permits the transition.

Possible results:

- `accepted`
- `clamped`
- `rejected_permission`
- `rejected_state`
- `rejected_navigation`
- `rejected_unknown_action`
- `coalesced`
- `fallback_applied`

## 8. Default timing policy

These are engineering defaults pending Build & Test calibration; they are not user-psychology claims.

| Action | Minimum hold | Maximum/default lifetime | Same-action cooldown |
|---|---:|---:|---:|
| `look_at_player` | 0.4 s | 2.0 s | 1.0 s |
| `look_away` | 0.4 s | 2.0 s | 1.0 s |
| `react_headpat` | until end/stop event | 8.0 s failsafe | 0.25 s |
| `offer_hug` | 0.5 s | 12.0 s failsafe | 2.0 s |
| `wave` | 0.5 s | 3.0 s | 2.0 s |
| `sleep_idle` | 2.0 s | unbounded until higher priority | 5.0 s |
| locomotion actions | 0.25 s | until target/intent/guard changes | 0.5 s |
| `idle` / `stay` | 0 s | unbounded | 0 s |

Safety and explicit stop events ignore minimum-hold windows.

## 9. Coalescing and anti-flap rules

1. Repeating the same action at equal/lower priority during its cooldown returns `coalesced` rather than restarting animation/navigation.
2. A lower-priority command never resets the hold timer of a higher-priority action.
3. `look_at_player` events may extend gaze only up to the action maximum; repeated facing samples do not create an infinite forced stare.
4. `follow` does not continuously restart the NavMeshAgent. Destination updates belong to the navigation adapter at a slower configured cadence.
5. Touch start/end is edge-triggered; raw hand-near-head samples are not behavior commands.

## 10. Navigation contract

`approach`, `keep_distance`, `sit_near`, and `follow` require a navigation adapter. The BehaviorController only requests a semantic destination; it does not assume a path exists.

Adapter result contract:

- `ready`: path/destination accepted;
- `arrived`: target condition reached;
- `invalid_target`: target/seat missing;
- `no_path`: navigation cannot reach target;
- `cancelled`: higher-priority transition stopped navigation.

On `invalid_target` or `no_path`, the controller must stop locomotion and resolve to `stay` or `idle` rather than leaving a walking animation active.

VRChat officially supports Unity AI Navigation in worlds, but the exact NavMeshAgent wiring remains runtime work under #2/#6.

## 11. Presentation contract

The controller exposes semantic state; presentation adapters map it to actual Animator parameters and gaze targets.

Recommended debug outputs:

```text
currentAction
currentPriority
lastTransitionReason
lastResult
commandSequence
transitionSequence
navigationStatus
```

A world can replace the character model/Animator without changing arbitration semantics.

## 12. Model-plan arbitration

A validated model plan enters at `normal` priority unless a future protocol explicitly allows a narrower override.

Examples:

- player says `stay` while model proposes `follow` -> `stay` wins;
- touch is disallowed while model proposes `offer_hug` -> proposal rejected;
- active headpat reaction while model proposes `wave` -> headpat remains active;
- no model response -> deterministic policy continues normally.

A model cannot request `safety` or `explicit_intent` priority.

## 13. v0.1 deterministic acceptance vectors

These can be implemented as a debug harness before full avatar animation exists.

| # | Initial state | Input | Expected |
|---:|---|---|---|
| 1 | available + idle + touch allowed | `headpat_started` | `react_headpat`, interaction, accepted |
| 2 | available + idle + touch avoided | `headpat_started` | remain idle, rejected_permission |
| 3 | active `react_headpat` | `intent_stop_touch` | touch stops immediately; no resume of touch action |
| 4 | active model `follow` | `intent_stay` | `stay`, explicit_intent |
| 5 | active explicit `stay` | model proposes `follow` | stay unchanged, lower proposal rejected/coalesced |
| 6 | available + idle | `intent_quiet_company`, path valid | `sit_near`, gaze brief, no required speech |
| 7 | available + idle | `intent_sit_with_me`, no path | idle/stay fallback, rejected_navigation |
| 8 | available + near + idle | `entered_near` repeated rapidly | at most one brief look transition inside cooldown |
| 9 | available + near + idle | `inactivity_long` | `sleep_idle` |
| 10 | active sleep idle | `intent_follow`, valid | follow interrupts sleep |
| 11 | disabled | any normal/model proposal | rejected_state, no body movement |
| 12 | available | unknown action | rejected_unknown_action -> deterministic fallback |
| 13 | available + follow | path becomes invalid | locomotion stops -> stay/idle fallback |
| 14 | available + idle | no cloud/model available | all explicit/perception fallback cases still work |

## 14. Runtime completion criteria for #6

The design portion of #6 is complete when this document and the machine-readable BehaviorCommand schema agree. The issue itself remains open until a runnable world demonstrates:

1. controller compiles under UdonSharp;
2. debug outputs show the expected 14 acceptance vectors where applicable;
3. Animator does not flap/restart on duplicate events;
4. locomotion failure cannot leave walking presentation active;
5. stop-touch/disable interrupts execute immediately;
6. the same test set works with all model/gateway components absent.

Until #2 provides a real VCC world, do not claim these runtime criteria have passed.
