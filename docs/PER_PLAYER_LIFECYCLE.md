# Per-Player Companion Lifecycle v0.1

Status: **design-ready / runtime validation pending**  
Owner: Issue #4  
Blocked runtime evidence: #2  
Presentation-scope experiment: #11  
Two-client acceptance matrix: #12

This document defines how one logical companion instance is associated with one VRChat player without assuming that ownership, persistence, visual privacy, and social presentation are the same thing.

The design is intentionally conservative: use VRChat's PlayerObject lifecycle for per-player state, wait for persistence restoration before durable reads/writes, keep private relationship state local by default, and treat private visual/audio presentation as a separate experiment rather than an inferred property.

## 1. Verified VRChat facts

Checked against the current VRChat Creator documentation on 2026-09-16.

### PlayerObject creation

VRChat automatically spawns a copy of a `VRCPlayerObject` template for each player who joins a world. The spawned copy includes the template's components and children.

Source: <https://creators.vrchat.com/worlds/udon/persistence/player-object/>

### PlayerObject ownership

A player's PlayerObject and synced UdonBehaviours below it are owned by that player, and PlayerObject ownership cannot be transferred to another player. This makes PlayerObject a suitable default container for companion state that should remain associated with one player without ownership-transfer races.

Source: <https://creators.vrchat.com/worlds/udon/persistence/player-object/>

### Persistence readiness

Persistent PlayerObject data must not be treated as ready in `Start`. VRChat documents `OnPlayerRestored(VRCPlayerApi player)` as the readiness signal after persistent data has loaded. Reading or writing persistent user data too early can use stale values or be overwritten by restored data.

Sources:

- <https://creators.vrchat.com/worlds/udon/persistence/player-object/>
- <https://creators.vrchat.com/worlds/udon/persistence/player-data/>

### Persistence scope

Persistence is per player and per world. It is available across instances and devices for the same uploaded world, but it is not shared between different worlds. VRChat currently documents a 100 KB PlayerData budget and a separate 100 KB PlayerObject budget per player per world.

Source: <https://creators.vrchat.com/worlds/udon/persistence/>

### Local Build & Test behavior

In local Build & Test, each test client gets separate User Data. `Build & Reload`/rejoin within the test process can preserve that client's local test data, while closing the test client deletes it. This is useful for the #12 two-client acceptance matrix without pretending it is identical to uploaded-world server persistence.

Source: <https://creators.vrchat.com/worlds/udon/persistence/>

### ClientSim limit

ClientSim is useful for inspecting PlayerObjects and persistence, but VRChat explicitly warns that it cannot simulate all client/network behavior. It simulates only one local VRChat player; spawned remote players do not reproduce full remote deserialization behavior. Therefore ClientSim may be used for early lifecycle debugging, but it cannot satisfy #4's final two-client acceptance criterion.

Sources:

- <https://creators.vrchat.com/worlds/clientsim/>
- <https://creators.vrchat.com/worlds/clientsim/playerObject-editor/>

## 2. Architecture decision

For MVP, **one PlayerObject is the authoritative per-player lifecycle anchor for one logical companion state**.

That PlayerObject is responsible for:

- associating state with its owning player;
- exposing whether persistence restoration is complete;
- holding or referencing the player's `RelationshipState`;
- maintaining lifecycle/debug status;
- providing the stable lookup anchor used by world-side systems;
- optionally holding persistent synced fields once #7 finalizes their schema.

It is **not** automatically responsible for:

- proving that the companion renderer is owner-only;
- making audio private;
- synchronizing raw perception;
- directly calling an LLM;
- storing arbitrary conversation transcripts;
- making model output authoritative.

Those concerns remain separate adapters.

## 3. Lifecycle states

The lifecycle state machine is deliberately smaller than the behavior state machine from `INTERACTION_CONTRACT.md`.

```text
template
   |
   | VRChat spawns PlayerObject for player
   v
spawned
   |
   | local references discovered / ownership valid
   v
restoring
   |
   | OnPlayerRestored(owner)
   v
ready
   | \
   |  \ companion disabled by explicit local/world policy
   |   v
   | disabled
   |
   | owner leaves / object becomes invalid
   v
detached
```

Allowed lifecycle states:

- `spawned` — object exists, but initialization is not yet complete;
- `restoring` — ownership/reference setup is valid, persistent user data is not yet safe for durable mutation;
- `ready` — normal companion interaction may proceed;
- `disabled` — companion intentionally disabled for the owning player; persistent state remains intact unless an explicit reset occurs;
- `detached` — owning player/reference is no longer valid; no further player-facing action is allowed.

`template` is an editor concept and is not a runtime lifecycle state. VRChat disables PlayerObject templates and creates runtime copies. Runtime code must not mutate or destroy the disabled template after spawning.

## 4. Ownership invariant

For a spawned companion PlayerObject `C` associated with player `P`:

```text
owner(C) == P
```

must remain true for the lifetime of `C`.

The implementation should **not** call `Networking.SetOwner` on the PlayerObject or its synced UdonBehaviour children. VRChat's PlayerObject ownership model is specifically intended to avoid ownership transfer for per-player objects.

If an ownership check ever disagrees with the expected player during runtime testing, treat that as an implementation/SDK error condition and stop state mutation instead of attempting a manual repair transfer.

## 5. Restore gate

The interaction contract already exposes `companion.mode = restoring`. #4 maps that mode directly to lifecycle readiness.

Before `OnPlayerRestored(P)` for the PlayerObject owner:

Allowed:

- discover components/references;
- initialize non-persistent local defaults used only for presentation/debugging;
- expose `restoring` status;
- show a neutral loading/idle presentation;
- collect no irreversible relationship changes.

Forbidden:

- write durable relationship preferences;
- increment durable familiarity counters;
- overwrite restored synced values with defaults;
- execute memory migrations;
- send a model request containing durable memory that has not finished restoring.

After the matching `OnPlayerRestored(P)`:

1. mark lifecycle `ready`;
2. validate/migrate schema if #7 requires it;
3. expose the restored `RelationshipState` to behavior/dialogue adapters;
4. emit the interaction-contract event `persistence_restored`;
5. permit durable writes.

`OnPlayerRestored` for another player must not accidentally unlock the local owner's restore gate.

## 6. Finding the correct per-player companion

World systems must not keep references to the disabled template and pretend it is a player's runtime instance.

Preferred lookup paths from the official API are:

- `Networking.GetPlayerObjects(targetPlayer)` and then find the expected companion component; or
- `Networking.FindComponentInPlayerObjects(targetPlayer, referenceComponent)` using a component reference under the template.

Source: <https://creators.vrchat.com/worlds/udon/persistence/player-object/>

The implementation should wrap lookup behind one small adapter so behavior/perception code does not duplicate PlayerObject discovery logic.

Suggested conceptual API:

```text
CompanionHandle FindCompanion(VRCPlayerApi player)
bool IsReady(CompanionHandle companion)
VRCPlayerApi GetAssociatedPlayer(CompanionHandle companion)
```

This is an architectural contract, not compiled UdonSharp code yet.

## 7. Player leave and invalid references

VRChat recommends checking `VRCPlayerApi.IsValid` before using a stored player reference because it becomes invalid after that player leaves.

Source: <https://creators.vrchat.com/worlds/udon/players/>

Therefore:

- any cached player reference is treated as ephemeral;
- behavior/perception ticks verify validity before player-facing work;
- `OnPlayerLeft` invalidates lookup caches for that player;
- lifecycle moves to `detached` before any further state-dependent action;
- no persistent writes are deferred until `OnPlayerLeft` for the local player, because VRChat documents that local persistent user data must be saved before leaving and cannot be saved from the local player's `OnPlayerLeft` event.

Source: <https://creators.vrchat.com/worlds/udon/persistence/>

## 8. State isolation

The MVP requires isolation at two different layers.

### 8.1 Logical state isolation

Player A's normal local interaction must never mutate Player B's `RelationshipState`.

Every state mutation must be routed through the companion associated with the intended player. Generic scene managers must not store one global `RelationshipState` singleton for all users.

### 8.2 Presentation isolation

PlayerObject ownership does **not** by itself prove owner-only rendering or owner-only audio. Presentation privacy is tracked separately in #11.

Until #11 is experimentally resolved:

- relationship memory remains private-by-default at the contract layer;
- presentation code must declare whether an effect is `local/private` or `social/synchronized`;
- no design document may claim that PlayerObject ownership automatically makes a visible companion private.

## 9. Social mode boundary

A future social companion mode may intentionally synchronize selected presentation state, but it should synchronize a **small presentation projection**, not raw private state.

Example social projection:

```text
presentation_action = sit_near
presentation_expression = gentle_idle
presentation_target = owner
```

Do not synchronize by default:

- raw interaction history;
- durable memory contents;
- private model prompts;
- inferred comfort preferences;
- transcript text that was not explicitly chosen for social display.

This keeps social visibility an explicit product feature rather than an accidental consequence of networking.

## 10. Failure behavior

### PlayerObject not found

- stay in safe idle/no-companion state;
- retry lookup on a bounded cadence or relevant lifecycle event;
- show debug diagnostics in development builds;
- do not fall back to another player's object.

### Persistence restore delayed

- remain `restoring`;
- use neutral local presentation only;
- do not mutate durable state;
- do not infer that missing data means a new user until restoration completes.

### Player leaves during interaction

- cancel active locomotion/touch/gaze target tied to that player;
- invalidate caches;
- enter `detached`;
- do not transfer the companion to another player.

### Cloud/LLM unavailable

No lifecycle transition is required. The deterministic behavior policy remains active once the PlayerObject is `ready`.

## 11. Acceptance matrix

#4 is **not complete** until #12 is executed in VRChat Build & Test with at least two clients.

| Test | Setup | Expected result |
|---|---|---|
| Spawn isolation | Start 2 clients | each player has a distinct PlayerObject instance |
| Ownership | inspect both PlayerObjects on both clients | each object remains associated with/owned by its player |
| Restore gate | log `Start` and `OnPlayerRestored` | no durable mutation before matching restore event |
| Cross-player mutation | interact only as A | B's relationship state does not change |
| Rejoin persistence | mutate one allowed persistent test field, Build & Reload/rejoin | correct client's field restores according to documented local-test persistence behavior |
| Leave invalidation | remove/close one test player where supported | stale player reference is rejected and companion becomes detached |
| No-cloud continuity | disable model/gateway | lifecycle and deterministic embodiment remain functional |
| Presentation scope | execute #11 matrix | actual owner-only/social visibility is recorded rather than assumed |

ClientSim may be used before this matrix to inspect PlayerObject spawning and stored fields, but it is not accepted as proof of the two-client networking behavior.

## 12. Implementation order

When #2 supplies a real `World/` project:

1. create one minimal PlayerObject template with a tiny UdonSharp lifecycle component;
2. expose owner/player ID and lifecycle state in a development-only debug display;
3. implement `spawned -> restoring -> ready` using `OnPlayerRestored`;
4. implement one non-sensitive test field for persistence;
5. implement PlayerObject lookup wrapper;
6. execute #12;
7. execute #11 for presentation scope;
8. only then connect #5 perception and #6 behavior to the lifecycle handle.

This order keeps networking/persistence uncertainty from contaminating the embodiment work.

## 13. Definition of design-ready

The design portion of #4 is considered ready when all implementation decisions required before Unity work are explicit:

- PlayerObject is the lifecycle anchor;
- ownership is per associated player and must not be manually transferred;
- `OnPlayerRestored` is the persistence readiness gate;
- PlayerObject lookup uses the official per-player APIs;
- stale `VRCPlayerApi` references are invalidated;
- logical privacy is separate from presentation privacy;
- two-client evidence, not ClientSim alone, is required for final acceptance.

The runtime portion remains open until #2, #11, and #12 provide real client evidence.
