# Runtime

Current world-side scaffolding includes:

- `CompanionTransportRouteBinding` — minimal UdonSharp serialization boundary for the bounded transport route table. Editor tooling populates schema/index versions, persona ID, route count, and parallel live/static `VRCUrl[]` arrays. Runtime transport code must treat these arrays as immutable and use array index as `route_index`.
- `CompanionTransportProbe` — minimal `VRCStringDownloader` request-discipline probe for #18. It exposes separate live/static route starts, enforces one request in flight, clamps request starts to >=6 seconds apart, times out locally, and compares the callback `result.Url` with the currently expected URL before accepting a success or error. A callback arriving after timeout or after a newer request has replaced the expected URL is counted and ignored as stale.

The probe intentionally stops before JSON parsing, BehaviorPlan validation, fallback selection, or behavior execution. Its public/debug fields exist to capture the first real-world evidence requested by #18; they are not a production telemetry contract.

Planned/parallel world-side modules:

- `CompanionLifecycle`
- `CompanionPerception`
- `CompanionRelationshipState`
- `CompanionBehaviorController`
- `CompanionMemoryAdapter`
- `CompanionDialogueAdapter`

Do not add provider-specific LLM code directly to Udon behaviors. Keep the runtime contract provider-neutral.

`CompanionTransportProbe` is source scaffolding only until #2 provides a runnable VCC World project. Its Unity/UdonSharp compilation, downloader callback behavior, cadence timing, timeout/stale-callback behavior, and PC/Quest results are still unverified and must not be reported as runtime facts.
