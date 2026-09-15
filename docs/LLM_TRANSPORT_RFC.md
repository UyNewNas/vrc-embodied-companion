# RFC: LLM Transport v0.1

Status: **MVP architecture selected; VRChat end-to-end proof still pending on #2**  
Verified against current VRChat Creator documentation: **2026-09-16**

## Decision

For the first networked companion MVP, use a **bounded, stateless GET transport** built from editor-time `VRCUrl` values.

The world does **not** attempt to turn arbitrary runtime text into a URL. Instead, it maps a small local context tuple to one of a generated set of pre-baked routes:

```text
persona + intent + relation_band + comfort_style + language + turn_slot
    -> editor-time VRCUrl
    -> VRCStringDownloader.LoadUrl(...)
    -> BehaviorPlan v0.1 JSON
    -> world-side validation / arbitration
```

The live gateway is optional. The same route contract can be backed by a static trusted response pack so the companion still has network-supplied variety without requiring a custom dynamic host.

This is deliberately narrower than free-form chat. It is selected because it is compatible with the platform surface that VRChat currently documents, keeps sensitive relationship state local, and does not require a client mod, helper application, or bot account.

## Verified platform facts

### Runtime outbound data is URL-shaped, not a general HTTP client

VRChat documents external loading through `VRCUrl`. A `VRCUrl(string)` constructor can only be called at editor time. At runtime, a user can enter a URL through a `VRCUrlInputField`, but the official API does not document a way for Udon to construct a new `VRCUrl` from an arbitrary runtime string.

Implication: a normal text input field cannot be assumed to become `https://gateway/...?...<prompt>` at runtime.

Official source:
- https://creators.vrchat.com/worlds/udon/external-urls/

### String loading is pull-only in the documented surface

`VRCStringDownloader.LoadUrl` downloads text/bytes from a `VRCUrl` and reports success/error through callbacks. The documented API exposes the attempted URL and response body/error, but does not expose request bodies, custom headers, HTTP methods, or a general POST API.

Architectural rule: **do not rely on POST, cookies, custom request headers, or undocumented client-identifying headers.** If later testing reveals additional behavior, treat it as non-contractual unless VRChat documents it.

Official source:
- https://creators.vrchat.com/worlds/udon/string-loading/

### String downloads are rate limited

VRChat currently documents one string download every five seconds. Requests above that limit are queued, and queued downloads may be processed in random order.

Transport rule:
- at most one companion request may be in flight locally;
- do not intentionally enter the VRChat string-download queue;
- start requests no faster than one per 6 seconds in v0.1;
- dialogue UI must remain usable while the transport is cooling down.

Official source:
- https://creators.vrchat.com/worlds/udon/string-loading/

### Custom live gateways require URL trust consent

For string loading, custom hosts outside VRChat's trusted list require the player to enable **Allow Untrusted URLs**. The current trusted string-loading list includes `*.github.io`, GitHub Gist, Pastebin, Disbridge, and VRCDN, but a normal custom API / Workers domain cannot be assumed trusted.

Product rule: the companion must work without a live gateway. A custom live gateway is an enhancement, not a boot requirement.

Official sources:
- https://creators.vrchat.com/worlds/udon/external-urls/
- https://creators.vrchat.com/worlds/udon/string-loading/

## Why a stateless finite route matrix

A stateful server session would need a stable session/user key on every request. Under the documented Udon URL surface, a server-generated token cannot be inserted into a newly constructed runtime URL on the next turn.

Likewise, a player ID or instance ID should not be inferred from undocumented HTTP headers, source IP, cookies, or other transport side channels.

Therefore the MVP gateway is stateless. Every request contains all server-visible context in the **choice of a pre-baked route**, and the meaningful durable relationship state stays inside the world.

Example route shape:

```text
GET /v1/plan/default/quiet_company/warm/quiet/zh/2
```

The segments are bounded codes, not free-form user data.

A corresponding model prompt can be assembled server-side from those codes:

```text
persona=default
intent=quiet_company
relation_band=warm
comfort_style=quiet
language=zh
```

The gateway returns a `BehaviorPlan v0.1` document. It must not return arbitrary world commands outside the existing schema and world-side allowlist.

## v0.1 route dimensions

The initial route vocabulary should stay intentionally small.

### Intent

Recommended initial values:

- `greet`
- `quiet_company`
- `talk_light`
- `walk_with_me`
- `offer_hug`
- `goodbye`

These are semantic intents selected by the player/world, not model-generated URLs.

### Relationship band

- `new`
- `familiar`
- `warm`

This is a coarse local presentation bucket. Do not expose a hidden numerical dependency/affection score to the gateway.

### Comfort style

- `neutral`
- `quiet`

Additional styles can be added only when they remain low-sensitivity and have a clear player-visible meaning.

### Language

Start with the languages actually supported by the demo. The route key uses a bounded code such as `zh` or `en`.

### Turn slot

Use a small rotating `turn_slot` (`0..3`). It has two purposes:

1. distinguish sequential requests that otherwise use the same context tuple;
2. provide a bounded variation/cache lane without inventing a runtime URL.

The slot is not a user identifier and carries no relationship information.

With 6 intents × 3 relation bands × 2 comfort styles × 2 languages × 4 turn slots, one persona needs 288 generated URLs. These should be produced by editor tooling rather than authored by hand.

## Live gateway mode

A live endpoint may invoke an LLM and return a fresh `BehaviorPlan` for the route tuple.

Requirements:

- GET is read-only/stateless from the application's perspective;
- no account/session cookie is required;
- no transcript is stored server-side by default;
- route codes are validated against the same bounded enums used by the world;
- response is JSON matching `schemas/behavior-plan.v0.1.schema.json`;
- model output is validated on the server **and again** by the world-side adapter before it affects behavior;
- rate limiting and cost limits exist independently of VRChat's client rate limit;
- failure returns a small bounded error response rather than an HTML error page.

A custom live host is expected to require **Allow Untrusted URLs** unless VRChat explicitly trusts that domain in the future.

## Trusted static-pack fallback

The same finite route matrix can be published as static JSON files on a trusted string-loading host such as `*.github.io`.

Example:

```text
https://example.github.io/vrc-companion-pack/v1/default/quiet_company/warm/quiet/zh/2.json
```

The static pack can be generated offline from an LLM, reviewed, and published. It is not conversational inference, but it gives the network adapter a zero-account, zero-custom-backend compatibility mode and exercises the same world-side parser.

Fallback order for v0.1:

```text
live gateway allowed + healthy
    -> live BehaviorPlan
else trusted static pack available
    -> static BehaviorPlan
else
    -> deterministic world-side behavior (#6)
```

No network failure should freeze the companion.

## Request ordering and stale-response policy

`VRCStringDownloader` may queue requests if the rate limit is exceeded, and queued order is not guaranteed. v0.1 therefore avoids queueing entirely.

World-side adapter rules:

1. Keep exactly one request in flight.
2. Save the selected `VRCUrl` / route key and `turn_slot` locally.
3. On callback, verify `IVRCStringDownload.GetUrl()` matches the expected route before accepting the response.
4. Rotate `turn_slot` only after success/error settles the previous request.
5. Do not start another request merely because UI timeout text is shown; the underlying downloader has no documented cancellation primitive.
6. If a callback is stale or malformed, discard it and use deterministic fallback.

The runtime proof must specifically test delayed responses and repeated identical intents.

## Privacy boundary

The gateway receives **bounded context classes**, not raw relationship history.

Do not put these in route paths:

- player display name / user ID;
- free-form conversation text;
- transcript or episodic memory;
- medical, sexual, mental-health, or vulnerability labels;
- names/stories about third parties;
- hidden affinity/dependency scores.

This keeps the selected transport compatible with the persistence decision in `docs/PERSISTENCE_SCHEMA.md`: intimate memory does not belong in the native/shared MVP data plane.

## Rejected alternatives for the MVP

### Arbitrary text -> runtime URL -> LLM

**Rejected / unsupported by current documented API.** `VRCUrl(string)` is editor-time only. A normal text box does not give Udon a documented arbitrary-string-to-`VRCUrl` conversion path.

### Ask players to type an encoded API URL into `VRCUrlInputField`

**Rejected product UX.** Although users may enter runtime URLs, requiring players to manually construct an API URL is not a companion chat interface and creates unnecessary security/privacy risk.

### POST JSON directly from Udon

**Rejected / unsupported by current documented string loader.** Do not design against an HTTP surface VRChat does not document.

### Server sessions identified by IP/cookies/undocumented headers

**Rejected.** Fragile, privacy-hostile, and not part of the official contract.

### Always-online bridge/bot account

**Rejected for initial releases.** It violates the project's no-player-bot architecture and makes a reusable world prefab operationally dependent on an in-world account.

### Free-form voice streaming

**Not part of this transport.** Voice/STT/TTS remains a later research track.

## What this transport can and cannot do

### It can

- give every player a zero-helper-app companion experience;
- request fresh generated wording for bounded player intents;
- incorporate coarse local relationship/comfort buckets without revealing the full relationship state;
- return speech + embodied behavior in one validated plan;
- degrade cleanly to static or local behavior.

### It cannot

- send arbitrary typed sentences from Udon to the model;
- provide unrestricted conversational memory to the server;
- guarantee live custom-gateway access when Allow Untrusted URLs is off;
- replace the later free-form text/voice research track.

That limitation is intentional for MVP v0.1.

## Minimal end-to-end proof required to close #8

Blocked on #2 for the VRChat half of the proof.

Once a runnable world exists:

1. Bake at least four test routes as serialized `VRCUrl` values.
2. Serve valid `BehaviorPlan` JSON from a test endpoint.
3. Trigger a route from Udon and parse the response.
4. Demonstrate the 5-second platform limit is respected with a >=6-second local gate.
5. Demonstrate a delayed/stale response is rejected rather than applied to a newer turn.
6. Demonstrate an untrusted custom gateway fails gracefully when the player has not enabled the setting.
7. Demonstrate a trusted static-pack route still works without the live gateway.
8. Demonstrate total network failure falls back to #6 deterministic behavior.
9. Record PC and Quest behavior separately before claiming cross-platform support.

Until these are executed in a real world/client, #8 remains open.

## Follow-up implementation slices

- editor generator: bounded context matrix -> serialized `VRCUrl[]` route table;
- world adapter: route lookup, one-in-flight gate, callback validation, BehaviorPlan parser;
- static pack generator for GitHub Pages;
- optional live gateway reference implementation;
- telemetry limited to aggregate route/error counters, with no intimate player content.
