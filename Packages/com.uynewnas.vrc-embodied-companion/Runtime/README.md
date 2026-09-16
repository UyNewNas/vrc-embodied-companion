# Runtime

Current world-side scaffolding includes:

- `CompanionTransportRouteBinding` — minimal UdonSharp serialization boundary for the bounded transport route table. Editor tooling populates schema/index versions, persona ID, route count, and parallel live/static `VRCUrl[]` arrays. Runtime transport code must treat these arrays as immutable and use array index as `route_index`.
- `CompanionTransportProbe` — minimal `VRCStringDownloader` request-discipline probe for #18. It exposes separate live/static route starts, enforces one request in flight, clamps request starts to >=6 seconds apart, times out locally, compares the callback `result.Url` with the currently expected URL, and passes matched success payloads through `CompanionBehaviorPlanValidator` before retaining them as valid plans. A callback arriving after timeout or after a newer request has replaced the expected URL is counted and ignored as stale.
- `CompanionBehaviorPlanValidator` — fail-closed `VRCJson` parser for `BehaviorPlan v0.1`. It rejects unsupported schema versions, unknown root/nested fields, unknown actions/gaze/style values, out-of-range numeric fields, malformed memory proposals, and payloads larger than 16 KiB before parsing. It exposes only bounded parsed fields for the later behavior/dialogue adapter.

VRChat documents that `VRCJson.TryDeserializeFromJson` parses lazily: a valid top-level object can still contain malformed nested JSON that fails only when accessed. The validator therefore walks every supported nested object/list and its keys rather than treating the initial deserialize result as sufficient validation.

## Validated-plan handoff boundary

`CompanionTransportProbe.planHandoffTarget` is an optional local UdonSharp behavior that receives one of two fixed custom events:

- `OnCompanionBehaviorPlanAccepted` — emitted only after callback-URL matching and `BehaviorPlan v0.1` validation succeed. The target may read the bounded parsed fields from `behaviorPlanValidator`, but **must route any body action through the #6 `CompanionBehaviorController` authorization path**. Validation is not authorization.
- `OnCompanionBehaviorPlanUnavailable` — emitted after a matched download error, local timeout, missing validator, or schema-invalid plan. The model proposal is not applied. The intended deterministic fallback is to leave the #6 controller's existing event/state machine authoritative; a fallback adapter may react to `lastUnavailableReason` but must not reuse rejected model fields.

The probe deliberately uses `SendCustomEvent` rather than dynamic `SetProgramVariable` writes. That keeps the transport layer from guessing or mutating the authorization controller's field layout. VRChat documents that custom events sent to UdonSharp targets must be public, so any future handoff adapter must expose those exact public event names.

Stale callbacks do **not** emit `OnCompanionBehaviorPlanUnavailable`, because they do not belong to the current request and must not perturb the current deterministic interaction.

`memory_proposals` remain syntax-validated only; neither handoff event authorizes or persists them.

The probe still intentionally stops before automatic live -> static fallback selection, dialogue playback, memory-write authorization, or direct behavior execution. Its public/debug fields exist to capture the first real-world evidence requested by #18; they are not a production telemetry contract.

Planned/parallel world-side modules:

- `CompanionLifecycle`
- `CompanionPerception`
- `CompanionRelationshipState`
- `CompanionBehaviorController`
- `CompanionMemoryAdapter`
- `CompanionDialogueAdapter`

Do not add provider-specific LLM code directly to Udon behaviors. Keep the runtime contract provider-neutral.

`CompanionTransportProbe` and `CompanionBehaviorPlanValidator` are source scaffolding only until #2 provides a runnable VCC World project. Their Unity/UdonSharp compilation, JSON behavior, custom-event handoff behavior, downloader callback behavior, cadence timing, timeout/stale-callback behavior, and PC/Quest results are still unverified and must not be reported as runtime facts.
