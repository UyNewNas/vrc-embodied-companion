# Runtime

Current world-side scaffolding includes:

- `CompanionTransportRouteBinding` — minimal UdonSharp serialization boundary for the bounded transport route table. Editor tooling populates schema/index versions, persona ID, route count, and parallel live/static `VRCUrl[]` arrays. Runtime transport code must treat these arrays as immutable and use array index as `route_index`.

Planned/parallel world-side modules:

- `CompanionLifecycle`
- `CompanionPerception`
- `CompanionRelationshipState`
- `CompanionBehaviorController`
- `CompanionMemoryAdapter`
- `CompanionDialogueAdapter`

Do not add provider-specific LLM code directly to Udon behaviors. Keep the runtime contract provider-neutral.

The route binding is intentionally not a downloader. Request cadence, one-in-flight gating, callback URL validation, stale-response rejection, JSON validation, and deterministic fallback belong to the transport/runtime layer and still require #2/#18 Unity + VRChat evidence.
