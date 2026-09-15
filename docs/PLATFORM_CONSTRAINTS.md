# VRChat Platform Capability Matrix

Verified against the current published VRChat Creator documentation on **2026-09-16**.

This is an architecture input, not a promise that undocumented behavior will remain stable. Any item marked **Research** must be reproduced in a minimal world before the product depends on it.

## Status legend

- **Supported** — explicitly documented by VRChat and usable for the MVP.
- **Constrained** — supported, but with a material platform limit that changes the architecture.
- **Unsupported for MVP** — no documented platform path that satisfies the required behavior; do not build the MVP around it.
- **Research** — documentation is insufficient for our exact product behavior; reproduce experimentally.

## Capability matrix

| Capability | Status | Verified platform fact | Architecture consequence |
|---|---|---|---|
| Load remote text/JSON from Udon | **Supported / constrained** | `VRCStringDownloader.LoadUrl` can download strings. VRChat documents one string download per five seconds per user; excess requests queue in random order. | A turn-based LLM bridge is plausible. Token streaming and rapid polling are not an MVP assumption. |
| Construct arbitrary request URLs in code at runtime | **Unsupported for MVP** | `new VRCUrl(string)` is documented as callable only at editor time. Runtime URLs can come from `VRCUrlInputField` user input or values baked into the world. | Do not assume `prompt -> dynamically construct URL -> request` can be implemented directly in pure Udon. |
| Runtime user-entered URL | **Supported / constrained** | Players may enter a URL through `VRCUrlInputField`. | Useful for experiments / BYO endpoints, but poor default UX for a zero-install public companion. |
| Arbitrary HTTP POST / streaming API from Udon | **Unsupported for MVP / Research** | Current official external-URL documentation exposes URL-based media/string loading, not a generic HTTP client or arbitrary POST body API. | The initial architecture must not require direct OpenAI-compatible POST calls from Udon. Issue #8 owns transport experiments. |
| Custom backend domain without player settings | **Constrained** | URLs outside VRChat's relevant allowlist require the player to enable **Allow Untrusted URLs**. | A custom hosted gateway is not automatically zero-friction. A public demo must either tolerate this onboarding step or use a compatible trusted-host strategy. |
| Per-player durable data | **Supported** | Persistence stores PlayerData and PlayerObject data on VRChat servers and restores it across devices and instances of the **same world**. | Suitable for world-local preferences and relationship continuity. |
| Persistence capacity | **Supported / bounded** | Per player, per world: 100 KB PlayerData + 100 KB PlayerObject data. | Keep relationship memory compact; store structured preferences and summaries, not raw transcripts. |
| Cross-world native persistence | **Unsupported** | VRChat explicitly states persistent data cannot be shared between different worlds. | Portable companion identity/memory requires an optional external account/backend in a later phase. |
| Safe persistence initialization | **Supported / constrained** | VRChat says to wait for `OnPlayerRestored` before reading/writing restored PlayerData/PlayerObject data. Local persistent data must be saved before leaving; it cannot be saved from the local player's `OnPlayerLeft`. | Companion initialization needs a `restoring -> ready` lifecycle and proactive save semantics. |
| Per-player object lifecycle | **Supported** | `VRCPlayerObject` automatically spawns a copy for each player. The associated player owns their PlayerObjects and ownership cannot be transferred away. | Strong fit for one logical companion state container per player. |
| Persistent prefab isolation | **Supported** | VRChat recommends PlayerObjects for persistent prefabs; they contain state within a prefab hierarchy and avoid shared PlayerData key collisions. | Prefer PlayerObject state for the reusable VPM package where practical. |
| Head/hand pose sensing | **Supported** | `VRCPlayerAPI.GetTrackingData` is the recommended API for head and hands. For local VR users it returns tracking-manager data; for remote players it uses avatar head/hand bones. | Embodied reactions can use head/hand pose without computer vision. Treat remote tracking as less semantically precise. |
| Player position / facing / velocity | **Supported** | Udon exposes player position, rotation, bone transforms and velocity. | Proximity, approach/departure and coarse facing signals are viable. |
| NPC pathfinding | **Supported / constrained** | VRChat supports Unity AI Navigation, NavMesh runtime generation/update, dynamic obstacles, links and multiple NavMesh surfaces. Runtime custom agent types are not currently supported. | Use the default agent type for MVP locomotion; do not depend on custom agent profiles. |
| Local-only companion logic | **Supported** | Only variables explicitly marked synced (`[UdonSynced]`) are network variables. `NetworkEventTarget.Self` executes only on the sending player and never traverses the network. | Per-client perception/behavior decisions can remain local unless intentionally synchronized. |
| Completely private visual/audio companion in a multi-user instance | **Research** | PlayerObjects provide per-player ownership, but the exact cheapest pattern for rendering/hearing only your own companion is not established by the reviewed docs. | Reproduce with two clients before promising private companion embodiment. Keep private-vs-shared embodiment configurable. |
| Multi-user testing in ClientSim | **Constrained** | ClientSim simulates the local player and can spawn remote players, but does not fully simulate remote networking/deserialization. | Use ClientSim for fast local logic tests, then use VRChat Build & Test with multiple clients for ownership/network/privacy acceptance tests. |

## Detailed findings

### 1. External networking is URL-loading, not a general-purpose web client

Official references:

- [External URLs](https://creators.vrchat.com/worlds/udon/external-urls/)
- [String Loading](https://creators.vrchat.com/worlds/udon/string-loading/)

VRChat documents that URLs used by Udon are wrapped in `VRCUrl`. The `VRCUrl(string)` constructor can only be called at editor time. At runtime, a player can provide a URL through a `VRCUrlInputField`.

`VRCStringDownloader` provides remote text/JSON loading, with these documented limits:

- one string download every **5 seconds**;
- excess string requests are queued and downloaded in a random order;
- maximum string size is 100 MB;
- maximum queue size is 1000.

This is enough to justify a **low-frequency request/response transport experiment**, but not real-time streaming conversation.

The official documentation reviewed here does not expose a generic arbitrary HTTP POST request API. Therefore direct OpenAI-compatible POST calls are **not an MVP assumption**. Issue #8 must prove any transport stronger than URL loading before architecture depends on it.

### 2. URL trust materially affects zero-install UX

Official reference: [External URLs](https://creators.vrchat.com/worlds/udon/external-urls/)

VRChat restricts external URL access. Non-allowlisted URLs require the user to enable **Allow Untrusted URLs**. This applies both to user-entered VRCUrls and VRCUrls uploaded with the world when the domain is not trusted for that content type.

Implication: a custom Cloudflare Worker / API domain may work technically but still impose onboarding friction. The public demo must degrade gracefully when external loading is disabled.

### 3. Persistence is a good fit for world-local relationship continuity

Official references:

- [Persistence](https://creators.vrchat.com/worlds/udon/persistence/)
- [PlayerData](https://creators.vrchat.com/worlds/udon/persistence/player-data/)
- [PlayerObject](https://creators.vrchat.com/worlds/udon/persistence/player-object/)
- [VRC Enable Persistence](https://creators.vrchat.com/worlds/components/vrc_enablepersistence/)

Key facts:

- data is associated with the VRChat account;
- data is available across platforms and instances of the same world;
- each world may store **100 KB PlayerData + 100 KB PlayerObject data per player**;
- data cannot be shared natively between different worlds;
- wait for `OnPlayerRestored` before reading/writing restored data;
- the local player's persistent data must be saved before leaving and cannot be saved in that player's `OnPlayerLeft` callback;
- only the local player's PlayerData can be mutated from that client.

For a reusable prefab, VRChat specifically notes that PlayerObjects are often a better fit than PlayerData because state is contained in the prefab hierarchy and frequent PlayerData changes resend the player's PlayerData collection.

### 4. PlayerObjects map closely to the per-player companion model

Official reference: [PlayerObject](https://creators.vrchat.com/worlds/udon/persistence/player-object/)

A `VRCPlayerObject` template automatically creates a copy for every player. The player owns their PlayerObject and cannot transfer its ownership to another player. `Networking.GetPlayerObjects` and `Networking.FindComponentInPlayerObjects` allow scripts to find a player's spawned objects/components.

This resolves the core **logical lifecycle and ownership** question for Issue #4 at the architecture level. What still needs a two-client experiment is the presentation policy: whether the default companion body should be visible/audible only to its owner or socially visible to everyone, and the cheapest reliable implementation of each mode.

### 5. Embodied sensing does not require screen capture or camera inference

Official reference: [Player Positions](https://creators.vrchat.com/worlds/udon/players/player-positions/)

Available signals include:

- world position and rotation;
- velocity;
- avatar bone positions/rotations;
- tracking data for Head, LeftHand and RightHand.

VRChat recommends `GetTrackingData` for head and hand transforms. For a local VR player this comes from tracking hardware; for remote players it resolves from corresponding avatar bones.

Product rule: these are **interaction signals**, not ground truth about emotional state. “Head lowered” may drive a quieter behavior policy, but must not be labeled as depression/sadness detection.

### 6. Native NPC locomotion is viable

Official reference: [AI Navigation](https://creators.vrchat.com/worlds/udon/ai-navigation/)

VRChat supports Unity's AI Navigation package, including runtime NavMesh generation/updating, dynamic obstacles, links and multiple surfaces. Current VRChat limitations mean runtime baking does not support custom agent types, so the MVP should use the default agent type.

### 7. Local decisions can stay local; explicit synchronization is opt-in

Official references:

- [Network Variables](https://creators.vrchat.com/worlds/udon/networking/variables/)
- [Network Events](https://creators.vrchat.com/worlds/udon/networking/events/)

Only variables marked synced are sent as network variables. `NetworkEventTarget.Self` is a loopback target received only by the sender and bypasses network rate limiting because it is never sent over the network.

This supports a design where raw perception and most companion decision state remain local. Only social/visible companion state should be synchronized deliberately.

## Minimal experiments required before implementation claims

These experiments are intentionally deferred to the minimal world work in #2 / transport work in #8. They are the smallest tests needed for facts not settled by documentation alone.

### EXP-01 — two-client private companion presentation

**Question:** Can each player share one world while seeing/hearing only their own PlayerObject-backed companion with simple local visibility/audio gating?

**Setup:**

1. Create one `VRCPlayerObject` template with a visible cube, an UdonSharp component and owner label.
2. Launch VRChat Build & Test with two clients.
3. On each client, enumerate PlayerObjects and locally enable renderer/audio only for the PlayerObject owned by `Networking.LocalPlayer`.
4. Record whether each client sees exactly one local companion and whether ownership remains stable after one client leaves/rejoins.

**Pass:** each client independently sees/hears only its own companion, with no ownership transfer hacks and no cross-client state corruption.

### EXP-02 — persistence restore lifecycle

**Question:** Does the proposed companion initialization order avoid stale/default data overwriting restored values?

**Setup:**

1. Persist one synced preference on a PlayerObject with `VRCEnablePersistence`.
2. Gate reads/writes until `OnPlayerRestored`.
3. Change the value, leave, rejoin, and confirm it restores.
4. Repeat with two Build & Test clients.

**Pass:** restored values survive rejoin independently for both clients and no pre-restore initialization overwrites them.

### EXP-03 — constrained remote JSON request

**Question:** What practical latency/UX do we get from a documented `VRCStringDownloader` request path?

**Setup:**

1. Bake a trusted/allowed test URL into the world (or explicitly enable untrusted URLs for a controlled endpoint).
2. Trigger one JSON load on interaction.
3. Measure request-to-callback latency and failure behavior.
4. Trigger two requests inside five seconds and confirm queue/rate-limit behavior.

**Pass:** event-driven requests behave consistently enough for selectable dialogue turns, with a clear timeout/fallback path.

### EXP-04 — runtime free-form transport boundary

**Question:** Is there any platform-compliant zero-install way to send arbitrary player text to our backend without constructing arbitrary `VRCUrl` values in Udon code?

**Setup:** evaluate only documented mechanisms first: `VRCUrlInputField`, pre-baked URL pools, and trusted-host indirection. Do not assume undocumented reflection/client modification.

**Pass:** a reproducible path accepts arbitrary text and returns a response without client mods. Otherwise mark free-form transport as requiring an optional bridge or a constrained interaction design.

## Testing caveat

Official reference: [ClientSim](https://creators.vrchat.com/worlds/clientsim/)

ClientSim is excellent for fast editor iteration, including PlayerData/PlayerObject debugging, but it does **not** fully simulate remote networking/deserialization. Any acceptance criterion involving ownership, remote state, per-player privacy, or multi-user embodiment must be re-tested in VRChat with multiple clients before being called complete.

## Architecture decisions from this matrix

1. **PlayerObject is the default per-player state/lifecycle primitive.**
2. **World-local memory uses VRChat Persistence first.** Cross-world memory is explicitly later/backend work.
3. **The MVP companion must function without cloud inference.** External URL trust and request cadence make graceful offline behavior mandatory.
4. **LLM transport is an adapter, not the center of the world architecture.**
5. **Free-form voice/text is a research track.** Do not block embodied MVP progress on it.
6. **Use local head/hand/proximity signals, not screen capture, for initial embodiment.**
7. **Use default-agent AI Navigation for MVP locomotion.**
8. **Keep private perception and decision state local; synchronize only intentional social state.**

## Remaining research owners

- Private-vs-social companion body acceptance test: **#2 / #4**.
- Remote JSON transport and free-form input boundary: **#8**.
- Persistence schema and user reset semantics: **#7**.
- Concrete sensor normalization and headpat detection: **#5**.

With those items explicitly assigned, the platform-capability inventory itself is complete.