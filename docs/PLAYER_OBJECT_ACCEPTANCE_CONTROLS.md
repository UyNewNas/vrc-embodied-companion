# PlayerObject acceptance controls

Status: **development-only runtime harness; real VRChat evidence pending**  
Related: #4, #12, PR #22

The lifecycle runtime matrix already requires local mutation, owner refresh, and cross-player rejection observations. This harness makes those rows directly executable inside the generated minimal world instead of relying on inspector calls or one-off Udon edits.

## Controls in the generated world

The bootstrap creates three collider-backed cubes near the spawn point. VRChat's `Interact` event requires an interactable object with a collider, so each control is a normal primitive with a development-only UdonSharp behaviour.

| Control | Action | Expected evidence |
|---|---|---|
| `LifecycleControl-LocalToggle` | Toggle the current client's own ready lifecycle between enabled and disabled. | Ready -> disabled must emit `result=local_disable_succeeded`; disabled -> ready must emit `result=local_enable_succeeded`. Any other result is setup/not-ready or a failure, not a pass. |
| `LifecycleControl-LocalRefresh` | Call `RefreshLifecycleOwner()` on the current client's own ready/disabled lifecycle. | A valid ready or disabled lifecycle must keep the same state and emit `result=local_refresh_preserved`; `local_refresh_unexpected` is a failure. |
| `LifecycleControl-RemoteMutationProbe` | Find the first **ready** non-local player lifecycle and call the same disable path on it. | The emitted control line must report `result=remote_mutation_rejected`, `beforeState == afterState`, and `afterAction=disable_rejected_not_local_ready`. |

Run the local and remote controls from both client A and client B. Wait until both PlayerObjects have observed their matching restore callback before judging the controls. `local_toggle_not_ready`, `local_refresh_not_ready`, and `remote_ready_lifecycle_not_found` are setup/not-ready evidence, not passes.

## Stable evidence log

Every interaction emits a line beginning with:

`[VRC Companion Lifecycle Control]`

The line records the action, result, control object, local player id, target player id, actual owner id, associated player id, before/after lifecycle state, before/after lifecycle action, restore flag, and local-owner flag. Unavailable/setup cases use the same prefix plus a `result=` reason.

The local controls deliberately self-check the transition they are meant to prove. `LocalToggle` reports success only for `ready -> disabled_by_local_owner` or `disabled -> enabled_by_local_owner`; `LocalRefresh` reports success only when the lifecycle state is unchanged and the lifecycle reports the expected manual-refresh action. This avoids treating a generic "button was clicked" log as acceptance evidence.

The remote rejection path intentionally updates local diagnostic text (`lastLifecycleAction`) on the attempted remote copy to `disable_rejected_not_local_ready`. The protected lifecycle state must remain unchanged; diagnostics are not relationship state and must not be interpreted as a cross-player mutation.

## Acceptance sequence

1. Complete the same-checkout real Unity smoke prerequisite from `docs/PLAYER_OBJECT_RUNTIME_MATRIX.md`.
2. Start VRChat Build & Test with at least two clients.
3. Capture the existing `[VRC Companion Lifecycle Callback]` and `[VRC Companion Lifecycle Snapshot]` logs until both players are restored.
4. On A, interact with `LocalToggle` and require `result=local_disable_succeeded`; interact with `LocalRefresh` and require `result=local_refresh_preserved` with state still disabled; then interact with `LocalToggle` again and require `result=local_enable_succeeded`.
5. On A, interact with `RemoteMutationProbe`. Require `result=remote_mutation_rejected` and unchanged lifecycle state for B's copy.
6. Repeat steps 4-5 on B against A.
7. Continue the leave/rejoin and persistence boundary rows from the main runtime matrix.

## Current platform basis

Rechecked against VRChat Creator documentation on 2026-09-17:

- UdonSharp `public override void Interact()` is the supported interaction event, and the object needs a collider to receive interaction.
- `VRCPlayerApi.GetPlayers()` returns the players currently in the instance; the allocating overload is appropriate here because the development-only remote probe runs only on explicit interaction rather than every frame.

Sources:

- <https://creators.vrchat.com/worlds/examples/udon/>
- <https://creators.vrchat.com/worlds/udon/players/getting-players/>

## Scope and safety boundary

`CompanionLifecycleAcceptanceControl` is test instrumentation only. It contains no `[UdonSynced]` state, never calls `RequestSerialization`, never calls `Networking.SetOwner`, and never sends a network event. It uses the current VRChat player enumeration API only when a tester interacts with the remote probe; this is not a per-frame production code path.

No result in this document is claimed until captured from a real Unity/VRChat run.
