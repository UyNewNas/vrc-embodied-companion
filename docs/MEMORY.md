# Memory Model

## Goal

Memory should create continuity without turning the companion into an opaque surveillance database.

## Memory layers

### Working memory
Short-lived interaction state such as current topic, recent choices, current action, and comfort mode.

### Session memory
Valid until the player leaves or the session expires.

### Durable preferences
Small, stable, user-beneficial facts such as preferred name, interpersonal distance, touch preferences, comfort style, and language preference.

### Episodic summaries
Optional and conservative. Avoid storing raw intimate transcripts by default.

## Memory write policy

A model may **propose** a memory write, but world/gateway policy decides whether it is stored.

Suggested classes:
- `explicit`: player directly asks to remember;
- `preference`: repeated stable preference;
- `episode`: summarized encounter;
- `sensitive`: never persist by default.

## User controls

Required before public release:
- show remembered profile;
- delete individual durable memories where practical;
- reset relationship;
- delete all local companion memory;
- clear explanation of what is stored and where.

## Cross-world memory

Not MVP. Any later cloud memory must support explicit opt-in, export, deletion, world-level consent boundaries, encryption at rest, and no hidden advertising/engagement profile.
