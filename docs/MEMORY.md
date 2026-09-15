# Memory Model

## Goal

Memory should create continuity without turning the companion into an opaque surveillance database.

The project distinguishes **psychological/product memory** from **what the VRChat-native persistence layer is actually suitable for storing**. VRChat Persistence is durable world state, but PlayerObject synced fields and PlayerData are networked world data, not a private encrypted vault. Native v1 persistence therefore stores only bounded, low-sensitivity preferences.

See [`PERSISTENCE_SCHEMA.md`](PERSISTENCE_SCHEMA.md) for the concrete v1 storage contract.

## Memory layers

### Working memory

Short-lived interaction state such as current topic, recent choices, current action, and comfort mode.

- session-local;
- not persisted;
- not treated as durable truth about the player.

### Session memory

Valid until the player leaves or the session expires.

Examples:

- current quiet-company request;
- recent interaction cooldowns;
- current activity mode;
- recent behavior-plan context.

Session memory is intentionally disposable in the VRChat-native MVP.

### Durable preferences

Small, stable, low-sensitivity configuration that improves continuity.

Native v1 allows only bounded preferences such as:

- comfort style (`quiet`, `gentle_prompt`, `talkative`);
- touch mode (`ask`, `avoid`, `headpat_ok`, `close_contact_ok`);
- preferred companion distance;
- response verbosity;
- language code.

Durable memory is disabled by default for a new player and can be explicitly enabled. Reset overwrites all native durable fields to neutral defaults and disables further durable preference writes.

### Episodic summaries

**Deferred beyond the native MVP.**

Do not persist raw or summarized intimate conversations in VRChat PlayerObject/PlayerData merely because the storage API allows strings. If future versions add episodic memory, it needs a separate opt-in architecture and threat model appropriate for private data.

## Memory write policy

A model may **propose** a memory write, but the model never owns persistence.

Suggested proposal classes:

- `explicit`: player directly asks to remember a supported preference;
- `preference`: bounded stable preference candidate;
- `episode`: not accepted by the native v1 store;
- `sensitive`: never accepted by the native v1 store.

Every accepted durable write must pass:

1. restore/lifecycle gate;
2. ownership gate;
3. `memoryEnabled` consent gate;
4. field allowlist;
5. type/range normalization;
6. persistence adapter serialization policy.

## Native v1 privacy boundary

Never store in VRChat-native persistence:

- raw dialogue transcripts;
- third-party names or relationship stories;
- sexual, medical, or mental-health details;
- secrets/tokens/account identifiers;
- inferred diagnoses, attachment labels, vulnerability scores, or hidden affection scores;
- unbounded free-form episodic summaries.

Presentation can be visually private to the owner while the underlying synced persistence is still ordinary network state. Those are separate properties and must not be conflated.

## User controls

Required before public release:

- clear indication whether durable preferences are enabled;
- inspect the small native preference set;
- change/disable durable preferences;
- reset relationship/native memory to neutral defaults;
- clear explanation of what is stored and where;
- no claim that reset physically deletes VRChat backend records when the world API only allows overwriting the meaningful fields.

## Cross-world/private memory

Not MVP.

Any later cloud/private memory must support explicit opt-in, export, deletion, world-level consent boundaries, encryption at rest, and no hidden advertising/engagement profile. It must be designed as a separate storage plane rather than silently expanding the VRChat-native schema.
