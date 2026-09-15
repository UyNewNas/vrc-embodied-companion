# Persistence Schema v1

Status: **design frozen; runtime evidence pending**

This document defines the first durable-memory contract for the VRChat-native MVP. It deliberately stores only a small set of low-sensitivity preferences. Free-form dialogue, intimate summaries, secrets, and inferred psychological profiles are out of scope for VRChat Persistence.

## Verified VRChat facts

Checked against the current VRChat Creator documentation on 2026-09-16:

- Persistence is scoped to **one world + one player account** and follows the player across devices and instances of the same world.
- Each world can store up to **100 KB PlayerData and 100 KB PlayerObject data per player** on VRChat's servers.
- Persistent reads/writes must wait for `OnPlayerRestored`; writing earlier can be overwritten by the data arriving from VRChat.
- The local player's persistent data must be saved before leaving; `OnPlayerLeft` is too late to save the departing local player's data.
- VRChat recommends considering **PlayerObjects rather than PlayerData for persistent prefabs** because PlayerObjects stay encapsulated in the prefab hierarchy, while all PlayerData keys share one world-wide namespace and every PlayerData change sends the player's complete PlayerData collection.
- A `VRCEnablePersistence` component on a PlayerObject persists synced variables on that PlayerObject and its child UdonBehaviours.
- PlayerObject persistent fields are still **synced variables**. Synced variables are replicated network state, not a private secret store.
- PlayerData is also network-visible world state: Udon can read PlayerData for any player in the instance, and remote PlayerData updates are received by clients.

Official references:

- https://creators.vrchat.com/worlds/udon/persistence/
- https://creators.vrchat.com/worlds/udon/persistence/player-object/
- https://creators.vrchat.com/worlds/udon/persistence/player-data/
- https://creators.vrchat.com/worlds/components/vrc_enablepersistence/
- https://creators.vrchat.com/worlds/udon/networking/variables/

## Storage decision

For the reusable package, the default durable carrier is:

```text
VRCPlayerObject
  └─ CompanionPersistentState (manual sync)
       + VRCEnablePersistence
```

Why:

1. it matches the per-player lifecycle already selected for #4;
2. it keeps the package's persistence fields inside the package instead of competing in the global PlayerData key namespace;
3. preference changes are infrequent, so manual synchronization is a better fit than continuous synchronization;
4. the same object can gate restore, ownership, persistence, and debug state.

PlayerData is not forbidden, but v1 does not require it.

## Privacy boundary

VRChat Persistence is **durable**, but it is not a private encrypted memory vault.

Therefore v1 MUST NOT persist:

- raw dialogue transcripts;
- free-form diary entries;
- names of third parties mentioned in conversation;
- sexual/medical/mental-health details;
- passwords, tokens, account identifiers, or secrets;
- model-inferred diagnoses, attachment labels, vulnerability scores, or hidden "affection" scores;
- episodic summaries containing intimate content.

The only data allowed in the native v1 store is low-sensitivity configuration that is useful even if treated as ordinary synchronized world state.

If the project later needs private or cross-world memories, that requires a separate opt-in storage architecture with its own threat model; it must not be smuggled into v1 PlayerObject fields.

## Layering

### Ephemeral working state — never persisted

Examples:

- current dialogue turn;
- current topic;
- current BehaviorCommand;
- current perceived distance/gaze/touch event;
- temporary quiet-company mode;
- model proposal currently under arbitration.

These remain unsynced/session-local unless another feature explicitly requires networking.

### Session relationship state — not durable in v1

Examples:

- recent interaction timestamps;
- short-term cooldowns;
- current activity state;
- temporary interaction count.

These reset on session/world re-entry.

### Durable low-sensitivity preferences — PlayerObject v1

All fields are bounded and have deterministic defaults.

| Field | Type | Default | Constraint / meaning |
|---|---|---:|---|
| `schemaVersion` | int | `0` | `0` means no durable-memory record has been activated; current version is `1` |
| `resetEpoch` | int | `0` | Increment on a full user reset so stale state can be identified during tests |
| `memoryEnabled` | bool | `false` | Explicit durable-preference opt-in; when false, preference setters must not serialize durable values |
| `comfortStyle` | int enum | `0` | `0=unset`, `1=quiet`, `2=gentle_prompt`, `3=talkative` |
| `touchMode` | int enum | `0` | `0=ask`, `1=avoid`, `2=headpat_ok`, `3=close_contact_ok` |
| `preferredDistanceM` | float | `1.10` | Clamp to `[0.40, 3.00]` metres |
| `responseVerbosity` | int enum | `1` | `0=brief`, `1=normal`, `2=verbose` |
| `languageCode` | string | `"auto"` | `auto` or a creator-supported language/BCP-47-like tag; max 16 chars |

There is intentionally **no hidden relationship/affection score** in the durable schema.

## Consent/write rules

1. Do not perform durable preference writes before the matching player's `OnPlayerRestored`.
2. Only the owner of the PlayerObject may mutate/serialize its durable state.
3. `memoryEnabled=false` is the initial state for a player with no existing v1 record.
4. Enabling memory is an explicit player action. Enabling sets `schemaVersion=1`, `memoryEnabled=true`, clamps the current preferences, and serializes once.
5. When memory is disabled, setters may update session-local presentation but MUST NOT serialize those choices as durable preferences.
6. Batch multiple preference updates before calling `RequestSerialization()` where practical; preferences are low-frequency state.
7. Values crossing the contract boundary are clamped/normalized before they enter the persistent fields.

## Reset semantics

VRChat does not provide a per-prefab "delete this PlayerObject record" primitive in the world API. v1 therefore defines **forget/reset as overwrite-to-neutral**, not physical deletion from VRChat's backend.

`ResetPersistentMemory()` MUST:

1. require local ownership and restored state;
2. increment `resetEpoch`;
3. set `schemaVersion=1`;
4. set `memoryEnabled=false`;
5. overwrite every preference field with its documented neutral default;
6. call `RequestSerialization()` once;
7. clear any unsynced working/session memory immediately;
8. update UI/debug state to state plainly that only neutral defaults remain.

This satisfies the product meaning of "forget me" for the native store: no prior user preference remains semantically usable after the reset, even though a small neutral record may still exist on VRChat's persistence backend.

## Migration rules

Let `CURRENT_SCHEMA_VERSION = 1`.

### `schemaVersion == 0`

Treat as no activated durable memory. Use defaults for the session. Do **not** create a persistent record merely because the player joined.

### `schemaVersion < CURRENT_SCHEMA_VERSION`

Run an explicit, idempotent migration after `OnPlayerRestored`, clamp all migrated values, then serialize once.

### `schemaVersion == CURRENT_SCHEMA_VERSION`

Load normally after restore and validate/clamp values before use.

### `schemaVersion > CURRENT_SCHEMA_VERSION`

Fail safe. Do not overwrite a record written by a newer schema. Use neutral session defaults, mark the state as `future_schema_read_only`, and expose a debug warning.

This protects a player from losing newer persistent data after a creator accidentally publishes an older package/world revision.

## Bandwidth/storage policy

The v1 record is intentionally tiny compared with VRChat's persistence limits. Nevertheless:

- use manual synchronization;
- do not serialize per-frame or per-interaction counters;
- do not store unbounded strings/arrays;
- do not use Persistence as an LLM transcript database;
- storage usage APIs (`GetPlayerObjectStorageUsage`, `GetPlayerObjectStorageLimit`, `RequestStorageUsageUpdate`) are diagnostics, not a high-frequency polling mechanism.

## Runtime acceptance matrix for #7

The spec is complete when documented; Issue #7 remains open until these are observed in a runnable #2 world:

1. New player restores with `memoryEnabled=false` and neutral session defaults without an automatic durable write.
2. Player explicitly enables durable preferences; one serialized v1 record survives Build & Reload / re-entry.
3. `comfortStyle`, `touchMode`, distance, verbosity, and language restore correctly after `OnPlayerRestored`.
4. Preference setters invoked before restore cannot overwrite incoming saved values.
5. Reset overwrites all durable fields to neutral values and disables memory.
6. After reset + re-entry, prior preferences do not reappear.
7. A simulated future schema (`schemaVersion > 1`) is not overwritten by v1 code.
8. Two-client Build & Test proves each client's persistent state is isolated by PlayerObject owner.
9. No model/gateway is required for any of the above.

Blocked runtime evidence: #2 minimal World project.
