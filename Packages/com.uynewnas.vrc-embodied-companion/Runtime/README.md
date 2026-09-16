# Runtime

Current world-side scaffolding includes:

- `CompanionTransportRouteBinding` — minimal UdonSharp serialization boundary for the bounded transport route table. Editor tooling populates schema/index versions, persona ID, route count, and parallel live/static `VRCUrl[]` arrays. Runtime transport code must treat these arrays as immutable and use array index as `route_index`.
- `CompanionTransportProbe` — minimal `VRCStringDownloader` request-discipline probe for #18. It exposes separate live/static route starts, enforces one request in flight, clamps request starts to >=6 seconds apart, times out locally, compares the callback `result.Url` with the currently expected URL, and passes matched success payloads through `CompanionBehaviorPlanValidator` before retaining them as valid plans. A callback arriving after timeout or after a newer request has replaced the expected URL is counted and ignored as stale.
- `CompanionBehaviorPlanValidator` — fail-closed `VRCJson` parser for `BehaviorPlan v0.1`. It rejects unsupported schema versions, unknown root/nested fields, unknown actions/gaze/style values, out-of-range numeric fields, malformed memory proposals, and payloads larger than 16 KiB before parsing. It exposes only bounded parsed fields for the later behavior/dialogue adapter.

VRChat documents that `VRCJson.TryDeserializeFromJson` parses lazily: a valid top-level object can still contain malformed nested JSON that fails only when accessed. The validator therefore walks every supported nested object/list and its keys rather than treating the initial deserialize result as sufficient validation.

The probe still intentionally stops before automatic live -> static fallback selection, deterministic #6 fallback invocation, memory-write authorization, or behavior execution. `memory_proposals` are syntax-validated only; they are not authorized or persisted by this component.

Planned/parallel world-side modules:

- `CompanionLifecycle`
- `CompanionPerception`
- `CompanionRelationshipState`
- `CompanionBehaviorController`
- `CompanionMemoryAdapter`
- `CompanionDialogueAdapter`

Do not add provider-specific LLM code directly to Udon behaviors. Keep the runtime contract provider-neutral.

`CompanionTransportProbe` and `CompanionBehaviorPlanValidator` are source scaffolding only until #2 provides a runnable VCC World project. Their Unity/UdonSharp compilation, JSON behavior, downloader callback behavior, cadence timing, timeout/stale-callback behavior, and PC/Quest results are still unverified and must not be reported as runtime facts.
