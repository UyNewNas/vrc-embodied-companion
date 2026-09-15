# Perception Pipeline v0.1

Status: **implementation specification; runtime calibration pending**  
Owner: Issue #5  
Calibration: Issue #13  
Remote/social sensing: Issue #14

Verified against current VRChat Creator documentation on 2026-09-16.

This document turns the `PerceptionSnapshot` / `InteractionEvent` contract from `docs/INTERACTION_CONTRACT.md` into a concrete world-side sensing algorithm. The numeric thresholds below are **design defaults**, not validated runtime facts; #13 owns Build & Test calibration.

## 1. Scope and trust boundary

The private MVP perception loop runs on the local owner's client and reads `Networking.LocalPlayer`.

VRChat's official `GetTrackingData` documentation says this is the suggested API for head/hand position and rotation. For the **local player** it comes from the TrackingManager; for **remote players**, Head/LeftHand/RightHand values are derived from avatar bones. Therefore v0.1 deliberately treats local-owner sensing and social/remote sensing as different quality regimes. Remote reuse is tracked separately in #14.

Official references:

- Player positions / `GetTrackingData`: https://creators.vrchat.com/worlds/udon/players/player-positions/
- Player API / `IsUserInVR`: https://creators.vrchat.com/worlds/udon/players/
- Player collisions: https://creators.vrchat.com/worlds/udon/players/player-collisions/
- Avatar scaling: https://creators.vrchat.com/worlds/udon/players/player-avatar-scaling/

## 2. Sampling loop

Default sample rate: **10 Hz** (`0.1 s`).

Do not emit policy events every frame. The perception adapter samples raw state, updates filtered state, then emits an event only on a meaningful transition.

Each sample reads:

- local player validity;
- local player root position;
- local player root rotation;
- local head `TrackingData` position + rotation;
- local left/right hand `TrackingData` positions;
- `IsUserInVR()`;
- companion root position;
- companion gaze/head interaction anchor position;
- companion locomotion/current action from the behavior adapter.

If `Networking.LocalPlayer` is invalid or lifecycle mode is `disabled`, emit no spatial/touch interaction events and return an `unknown` snapshot where appropriate.

## 3. Coordinate definitions

Use explicit anchors instead of avatar-bone guesses for the companion:

- `companionRoot`: locomotion/root reference;
- `companionGazeAnchor`: position the player visually faces;
- `companionHeadTouchAnchor`: center of the head-touch region.

Player tracking uses `GetTrackingData(Head|LeftHand|RightHand)` rather than `GetBonePosition` because VRChat explicitly recommends TrackingData for head/hands.

### 3.1 Body distance

`distance_m` is the horizontal root-to-root distance unless a behavior explicitly needs 3D reach distance:

```text
playerRootXZ = (player.x, 0, player.z)
companionRootXZ = (companion.x, 0, companion.z)
distance_m = length(playerRootXZ - companionRootXZ)
```

Using horizontal distance keeps crouching/sitting from spuriously changing social distance.

### 3.2 Head-facing vector

`facing` is **head orientation**, not eye tracking and not an inference of attention.

```text
toCompanion = normalize(companionGazeAnchor - headPosition)
headForward = headRotation * Vector3.forward
facingDot = dot(headForward, toCompanion)
```

## 4. Distance-band hysteresis

The interaction contract defines nominal boundaries:

- contact `< 0.35 m`
- near `0.35–1.2 m`
- social `1.2–3.0 m`
- far `> 3.0 m`

Do not switch exactly on those boundaries. v0.1 uses transition hysteresis:

| Transition | Enter | Exit |
|---|---:|---:|
| contact | `<= 0.30 m` | `>= 0.40 m` |
| near from social | `<= 1.10 m` | `>= 1.30 m` |
| social from far | `<= 2.85 m` | `>= 3.15 m` |

Transitions emit exactly one of `entered_contact`, `entered_near`, `entered_social`, `entered_far` after the new band remains stable for **2 consecutive samples**.

The asymmetry is intentional: the current band has inertia so body sway does not flap events.

## 5. Facing classification

Default head-facing thresholds:

- enter `toward_companion` when `facingDot >= 0.65` for 2 samples;
- leave `toward_companion` when `facingDot <= 0.50` for 2 samples;
- enter `away` when `facingDot <= -0.25` for 2 samples;
- leave `away` when `facingDot >= -0.10` for 2 samples;
- otherwise classify `sideways`;
- invalid/missing tracking -> `unknown`.

Emit only transition events:

- `facing_toward_started`
- `facing_away_started`

Do **not** use these states as proof that the player is listening, interested, sad, or affectionate.

## 6. Relative motion classification

Player world velocity alone is insufficient because the companion may also be moving. Use radial distance change as the primary signal:

```text
rawRadialSpeed = (distanceNow - distancePrevious) / dt
filteredRadialSpeed = EMA(rawRadialSpeed, alpha = 0.35)
```

Classification defaults:

- `approaching`: `filteredRadialSpeed <= -0.12 m/s` for 3 samples;
- `departing`: `filteredRadialSpeed >= +0.12 m/s` for 3 samples;
- return to neutral when `abs(filteredRadialSpeed) <= 0.06 m/s` for 3 samples;
- if distance changes while radial speed is neutral but root velocity is meaningful, use `moving_other`;
- otherwise `still`.

Emit `approach_started` / `departure_started` only when entering those stable states.

If either root teleports by more than `1.0 m` between samples, reset the motion filter instead of generating a motion transition.

## 7. Headpat detection

### 7.1 Why not player trigger/collision events

VRChat documents `OnPlayerTrigger*` / `OnPlayerCollision*` as collisions involving the **player capsule**, not tracked individual hands. They are useful for room/body zones but are not a reliable hand-to-companion-head detector. Headpat detection therefore uses hand TrackingData positions directly.

### 7.2 VR-only automatic headpat for v0.1

Automatic tracked-hand headpat classification is enabled only when `localPlayer.IsUserInVR()` is true.

Desktop users should receive an explicit interaction/UI fallback rather than interpreting animated avatar hand bones as deliberate touch.

### 7.3 Spatial gate

For each hand:

```text
handHeadDistance = length(handPosition - companionHeadTouchAnchor)
```

Defaults:

- candidate enter radius: `<= 0.16 m`;
- exit radius: `>= 0.22 m`;
- candidate dwell: at least `0.20 s`;
- end dwell outside exit radius: at least `0.30 s`.

A single fast pass through the sphere must not emit `headpat_started`.

### 7.4 Intentional-motion evidence

Maintain a rolling `0.6 s` hand-position window while inside the candidate radius.

A headpat start requires:

1. spatial candidate dwell satisfied; and
2. rolling path length `>= 0.08 m`; and
3. at least one direction reversal or curved/tangential movement around the head-touch anchor.

This distinguishes a pat/stroke from a controller simply resting near the head.

If a single sample jumps by more than `0.50 m`, reset that hand's touch window as a tracking discontinuity.

### 7.5 Debounce and ownership

- only one active headpat source hand at a time;
- if both qualify simultaneously, select the hand with lower current head distance;
- emit one `headpat_started` on activation;
- emit one `headpat_ended` after the selected hand stays outside the exit radius for `0.30 s`;
- `intent_stop_touch` / touch permission denial immediately clears the active detector and suppresses new starts.

### 7.6 Confidence

`confidence` is a sensor-quality score, not psychological certainty.

A simple v0.1 score may combine:

- proximity margin;
- dwell margin;
- normalized hand path length;
- motion reversal evidence.

Clamp to `[0,1]`. Behavior policy must still check touch permission regardless of confidence.

## 8. Inactivity

`inactivity_s` means **time since meaningful interaction activity**, not mood or disengagement.

Reset the timer on any of:

- explicit player intent;
- distance-band transition;
- approach/departure start;
- facing-toward/away transition;
- headpat start/end;
- local root displacement `>= 0.10 m` accumulated since the last reset;
- local head displacement `>= 0.12 m` accumulated since the last reset.

Do not reset on tiny tracking jitter.

Emit once per inactivity epoch:

- `inactivity_short` at `10 s`;
- `inactivity_long` at `60 s`.

Any meaningful activity rearms both thresholds.

## 9. Avatar scale and tracking changes

Thresholds are expressed in world meters because interaction space is physical, but avatar scale can still change perceived head size and comfort distance.

For v0.1:

- keep distance-band thresholds fixed in meters;
- expose the current avatar eye height to the debug view using `GetAvatarEyeHeightAsMeters()`;
- record `OnAvatarChanged` / `OnAvatarEyeHeightChanged` events for calibration logs;
- do not dynamically rescale touch radii until #13 has evidence that this improves accuracy.

## 10. Snapshot update order

Each 10 Hz tick executes in this order:

1. validate local player and lifecycle mode;
2. read raw tracking/root/companion anchors;
3. reject teleport/discontinuity samples;
4. update distance band;
5. update head-facing state;
6. update radial-motion filter;
7. update headpat detector if VR + touch sensing enabled;
8. update inactivity timer;
9. construct immutable `PerceptionSnapshot`;
10. enqueue zero or more transition `InteractionEvent`s;
11. let arbitration/behavior consume events after the snapshot is complete.

This prevents behavior side effects from altering the raw state halfway through a perception tick.

## 11. Debug view requirements

#5's runtime implementation should expose at least:

- sample timestamp / sequence;
- `IsUserInVR`;
- raw body distance;
- current distance band;
- raw `facingDot` + facing class;
- raw/filtered radial speed + motion class;
- left/right hand distance to head anchor;
- active headpat source;
- headpat candidate dwell/path length;
- inactivity timer;
- last emitted event;
- avatar eye height;
- perception enabled/disabled reason.

The debug view is required because the thresholds cannot be responsibly calibrated from code review alone.

## 12. Acceptance cases before closing #5

These are runtime cases, not claims that they have already passed:

1. standing near a boundary and swaying does not repeatedly flap distance events;
2. turning the head across a threshold emits one facing transition, not one per frame;
3. walking toward/away produces stable approach/departure events;
4. companion locomotion does not automatically misclassify a stationary player as approaching/departing;
5. a deliberate VR headpat starts and ends exactly once;
6. a rapid hand fly-through does not count as a headpat;
7. a hand resting near the head without pat motion does not count as a headpat;
8. Desktop mode does not synthesize automatic tracked-hand headpats;
9. tracking discontinuities/teleports reset filters rather than emitting false interaction;
10. `intent_stop_touch` suppresses touch reactions immediately.

#13 owns the concrete VR/Desktop/avatar-scale calibration matrix and final threshold changes.

## 13. Known limits

- No eye-gaze inference; head orientation only.
- No finger-level contact classification.
- No remote/social threshold guarantees; #14 owns that regime.
- No emotional-state classifier.
- No claim that current numeric defaults are production-quality before Build & Test calibration.
