# Runtime

Current world-side prototypes:

- `CompanionPlayerLifecycle` — issue #4 PlayerObject lifecycle anchor; restore-gated, fail-closed on owner mismatch, no ownership transfer, no presentation-privacy claim.
- `CompanionPlayerLookup` — issue #4 bounded lookup adapter using `Networking.GetPlayerObjects`; never falls back to another player's object.

The #4 draft branch also wires these prototypes into the minimal World bootstrap: `CompanionPlayerObjectTemplate` carries both the SDK `VRCPlayerObject` and `CompanionPlayerLifecycle`, while the scene-level `CompanionRuntime` object carries `CompanionPlayerLookup`. Keeping the lifecycle behaviour on the PlayerObject root makes its `Networking.GetOwner(gameObject)` checks query the PlayerObject ownership anchor directly instead of depending on ownership semantics of an unsynced child object. This is intentional logical-state integration only. Visual/audio privacy remains #11, and persistence remains #7; the lifecycle component has no synced fields and does not require `VRCEnablePersistence`.

Current official PlayerObject setup requires adding `VRCPlayerObject` to the template GameObject, with optional UdonBehaviour components on that object or its children. `VRCEnablePersistence` is only needed on an UdonBehaviour GameObject whose synced variables should persist. The World editor bootstrap creates UdonSharp behaviours through `AddUdonSharpComponent<T>()`, not plain Unity `AddComponent<T>()`, so the backing Udon program setup is performed. The branch CI separately resolves the committed SDK 3.10.5 graph and checks the exact `VRC.SDK3.Components.VRCPlayerObject`, `VRC.SDK3.Components.VRCEnablePersistence`, and UdonSharp editor-helper surfaces before relying on them.

Official references:

- https://creators.vrchat.com/worlds/udon/persistence/player-object/
- https://creators.vrchat.com/worlds/components/vrc_enablepersistence/
- https://creators.vrchat.com/worlds/udon/udonsharp/editorscripting/

Planned/parallel modules:

- `CompanionPerception`
- `CompanionRelationshipState`
- `CompanionBehaviorController`
- `CompanionMemoryAdapter`
- `CompanionDialogueAdapter`

The lifecycle prototype is intentionally not marked runtime-verified until the merged `World/` project is opened in the supported Unity/VRChat toolchain and issue #12's two-client matrix is executed.

Do not add provider-specific LLM code directly to Udon behaviors. Keep the runtime contract provider-neutral.
