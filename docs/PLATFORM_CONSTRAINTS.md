# VRChat Platform Constraints

This document is a **verification checklist**, not a frozen statement of platform law. Re-check against the current VRChat Creator documentation before implementation decisions.

## Assumptions to verify

### Udon outbound networking
World scripts have constrained outbound networking. Runtime free-form request construction cannot be assumed.

Implication: unrestricted `player text -> arbitrary POST -> LLM` may not be viable in pure Udon, so the MVP must not depend on it.

### External URL trust
Non-whitelisted external destinations may require player settings/consent.

### Request cadence
External string/data download paths may be rate limited. Dialogue should therefore be event-driven rather than continuous polling.

### Persistence
VRChat persistence can provide useful per-world continuity, but should be treated as a bounded store.

### Player tracking
Head/hand/body tracking data can support embodied behavior without camera-based emotion recognition.

## Architecture rule

Any feature requiring an unverified platform capability must be labeled **Research** until reproduced in a minimal world.
