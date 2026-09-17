# PlayerObject acceptance controls

Status: **development-only runtime harness; real VRChat evidence pending**  
Related: #4, #12, PR #22

The lifecycle runtime matrix already requires local mutation, owner refresh, cross-player rejection, and per-player ownership observations. This harness makes those rows directly executable inside the generated minimal world instead of relying on inspector calls or one-off Udon edits.

## Controls in the generated world

The bootstrap creates three collider-backed cubes near the spawn point. VRChat's `Interact` event requires an interactable object with a collider, so each control is a normal primitive with a development-only UdonSharp behaviour.

| Control | Action | Expected evidence |
|---|---|---|
| `LifecycleControl-LocalToggle` | Toggle the current client's own ready lifecycle between enabled and disabled. | Ready -> disabled must emit `result=local_disable_succeeded`; disabled -> ready must emit `result=local_enable_succeeded`. Any other result is setup/not-ready or a failure, not a pass. |
| `LifecycleControl-LocalRefresh` | Call `RefreshLifecycleOwner()` on the current client's own ready/disabled lifecycle. | A valid ready or disabled lifecycle must keep the same state and emit `result=local_refresh_preserved`; `local_refresh_unexpected` is a failure. |
| `LifecycleControl-RemoteMutationProbe` | First capture a read-only inventory of every player's spawned lifecycle, then find the first **ready** non-local lifecycle and call the same disable path on it. | Inventory must end with `result=snapshot_all_consistent`; the mutation attempt must then report `result=remote_mutation_rejected`, unchanged lifecycle state, and `afterAction=disable_rejected_not_local_ready`. |

Run the local and remote controls from both client A and client B. Wait until both PlayerObjects have observed their matching restore callback before judging the controls. `local_toggle_not_ready`, `local_refresh_not_ready`, and `remote_ready_lifecycle_not_found` are setup/not-ready evidence, not passes.

## Stable evidence log

Every interaction emits a line beginning with:

`[VRC Companion Lifecycle Control]`

The line records the action, result, control object, local player id, target player id, actual owner id, associated player id, lifecycle state/action data, restore flag, and local-owner flag. Unavailable/setup cases use the same prefix plus a `result=` reason.

The local controls deliberately self-check the transition they are meant to prove. `LocalToggle` reports success only for `ready -> disabled_by_local_owner` or `disabled -> enabled_by_local_owner`; `LocalRefresh` reports success only when the lifecycle state is unchanged and the lifecycle reports the expected manual-refresh action. This avoids treating a generic "button was clicked" log as acceptance evidence.

Before the remote mutation attempt, `RemoteMutationProbe` performs a **read-only PlayerObject inventory** through `Networking.GetPlayerObjects(player)`. It does not call `RefreshLifecycleOwner()` or mutate lifecycle state while sampling. For each valid player it emits one lifecycle-copy line when found:

- `result=snapshot_copy_consistent` means actual owner id == target player id, `associatedPlayerId` == target player id, and the lifecycle is not detached;
- `result=snapshot_copy_unexpected` records a concrete owner/association/state mismatch;
- `result=snapshot_copy_missing` means no `CompanionPlayerLifecycle` was found in that player's PlayerObjects.

The inventory then emits `action=snapshot_all_summary`. `result=snapshot_all_consistent` is allowed only when there is exactly one discovered lifecycle copy per valid player and no inconsistent/missing copy. `snapshot_all_incomplete` is not a pass. This gives the two-client run a single machine-readable ownership/isolation checkpoint before the cross-player mutation probe.

The remote rejection path intentionally updates local diagnostic text (`lastLifecycleAction`) on the attempted remote copy to `disable_rejected_not_local_ready`. The protected lifecycle state must remain unchanged; diagnostics are not relationship state and must not be interpreted as a cross-player mutation.

## Acceptance sequence

1. Complete the same-checkout real Unity smoke prerequisite from `docs/PLAYER_OBJECT_RUNTIME_MATRIX.md`.
2. Start VRChat Build & Test with at least two clients.
3. Capture the existing `[VRC Companion Lifecycle Callback]` and `[VRC Companion Lifecycle Snapshot]` logs until both players are restored.
4. On A, interact with `LocalToggle` and require `result=local_disable_succeeded`; interact with `LocalRefresh` and require `result=local_refresh_preserved` with state still disabled; then interact with `LocalToggle` again and require `result=local_enable_succeeded`.
5. On A, interact with `RemoteMutationProbe`. First require `result=snapshot_all_consistent`, then require `result=remote_mutation_rejected` and unchanged lifecycle state for B's copy.
6. Repeat steps 4-5 on B against A.
7. Continue the leave/rejoin and persistence boundary rows from the main runtime matrix.

## Current platform basis

Rechecked against VRChat Creator documentation on 2026-09-18:

- UdonSharp `public override void Interact()` is the supported interaction event, and the object needs a collider to receive interaction.
- `VRCPlayerApi.GetPlayers()` returns the players currently in the instance; the allocating overload is appropriate here because the development-only probe runs only on explicit interaction rather than every frame.
- `Networking.GetPlayerObjects(VRCPlayerApi)` returns all PlayerObjects associated with the supplied player and is the documented lookup path for scene/runtime code that needs spawned PlayerObject references.

Sources:

- <https://creators.vrchat.com/worlds/examples/udon/>
- <https://creators.vrchat.com/worlds/udon/players/getting-players/>
- <https://creators.vrchat.com/worlds/udon/persistence/player-object/>

## Scope and safety boundary

`CompanionLifecycleAcceptanceControl` is test instrumentation only. It contains no `[UdonSynced]` state, never calls `RequestSerialization`, never calls `Networking.SetOwner`, and never sends a network event. Player and PlayerObject enumeration happens only when a tester interacts with the remote probe; this is not a per-frame production code path.

No result in this document is claimed until captured from a real Unity/VRChat run.
