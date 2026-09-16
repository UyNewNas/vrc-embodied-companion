# Runtime

Current world-side prototypes:

- `CompanionPlayerLifecycle` — issue #4 PlayerObject lifecycle anchor; restore-gated, fail-closed on owner mismatch, no ownership transfer, no presentation-privacy claim.
- `CompanionPlayerLookup` — issue #4 bounded lookup adapter using `Networking.GetPlayerObjects`; never falls back to another player's object.

Planned/parallel modules:

- `CompanionPerception`
- `CompanionRelationshipState`
- `CompanionBehaviorController`
- `CompanionMemoryAdapter`
- `CompanionDialogueAdapter`

The lifecycle prototype is intentionally not marked runtime-verified until the merged `World/` project is opened in the supported Unity/VRChat toolchain and issue #12's two-client matrix is executed.

Do not add provider-specific LLM code directly to Udon behaviors. Keep the runtime contract provider-neutral.
