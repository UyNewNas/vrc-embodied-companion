# PlayerObject acceptance controls

Status: **development-only runtime harness; real VRChat evidence pending**  
Related: #4, #12, PR #22

The lifecycle runtime matrix already requires local mutation, owner refresh, and cross-player rejection observations. This harness makes those rows directly executable inside the generated minimal world instead of relying on inspector calls or one-off Udon edits.

## Controls in the generated world

The bootstrap creates three collider-backed cubes near the spawn point. VRChat's `Interact` event requires an interactable object with a collider, so each control is a normal primitive with a development-only UdonSharp behaviour.

| Control | Action | Expected evidence |
|---|---|---|
| `LifecycleControl-LocalToggle` | Toggle the current client's own ready lifecycle between enabled and disabled. | A ready local copy becomes `disabled_by_local_owner`; a disabled restored local copy becomes `enabled_by_local_owner`. |
| `LifecycleControl-LocalRefresh` | Call `RefreshLifecycleOwner()` on the current client's own lifecycle. | A valid ready lifecycle stays ready; a disabled restored lifecycle stays disabled. |
| `LifecycleControl-RemoteMutationProbe` | Find the first **ready** non-local player lifecycle and call the same disable path on it. | The emitted control line must report `result=remote_mutation_rejected`, `beforeState == afterState`, and `afterAction=disable_rejected_not_local_ready`. |

Run the local and remote controls from both client A and client B. Wait until both PlayerObjects have observed their matching restore callback before judging the remote-mutation row. `remote_ready_lifecycle_not_found` is setup/not-ready evidence, not a pass.

## Stable evidence log

Every interaction emits a line beginning with:

`[VRC Companion Lifecycle Control]`

The line records the action, result, control object, local player id, target player id, actual owner id, associated player id, before/after lifecycle state, before/after lifecycle action, restore flag, and local-owner flag. Unavailable/setup cases use the same prefix plus a `result=` reason.

The remote rejection path intentionally updates local diagnostic text (`lastLifecycleAction`) on the attempted remote copy to `disable_rejected_not_local_ready`. The protected lifecycle state must remain unchanged; diagnostics are not relationship state and must not be interpreted as a cross-player mutation.

## Acceptance sequence

1. Complete the same-checkout real Unity smoke prerequisite from `docs/PLAYER_OBJECT_RUNTIME_MATRIX.md`.
2. Start VRChat Build & Test with at least two clients.
3. Capture the existing `[VRC Companion Lifecycle Callback]` and `[VRC Companion Lifecycle Snapshot]` logs until both players are restored.
4. On A, interact with `LocalToggle`, then `LocalRefresh`, then `LocalToggle` again. Confirm disabled survives refresh and only the local owner can re-enable it.
5. On A, interact with `RemoteMutationProbe`. Require `result=remote_mutation_rejected` and unchanged lifecycle state for B's copy.
6. Repeat steps 4-5 on B against A.
7. Continue the leave/rejoin and persistence boundary rows from the main runtime matrix.

## Scope and safety boundary

`CompanionLifecycleAcceptanceControl` is test instrumentation only. It contains no `[UdonSynced]` state, never calls `RequestSerialization`, never calls `Networking.SetOwner`, and never sends a network event. It uses the current VRChat player enumeration API only when a tester interacts with the remote probe; this is not a per-frame production code path.

No result in this document is claimed until captured from a real Unity/VRChat run.
