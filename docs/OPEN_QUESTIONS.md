# Open Questions

## Platform
- What is the cleanest currently supported route for constrained LLM calls from a world?
- Can runtime-generated user text be transported without client mods or per-user helper software?
- What request latency is acceptable before presence breaks?
- What parts of companion state can remain local/private per player?

## Embodiment
- How should the companion detect a headpat robustly across avatar heights?
- Should the companion have NavMesh locomotion or mostly teleport/reposition?
- How do we prevent pathfinding behavior from feeling robotic?
- How should gaze behave so that constant eye contact is not uncanny?

## Dialogue
- Does the MVP need generative text, or can generative behavior + curated language already feel alive?
- How many response choices preserve agency without feeling like a visual novel?
- How do we preserve personality under provider/model changes?

## Memory
- Which memories belong to VRChat Persistence?
- What should never be stored?
- How do schema migrations work after world updates?

## Distribution
- What is the minimum Creator Companion / VPM installation flow?
- Should persona packs be separate packages?
- How do world creators plug in their own animation controllers?

## Economics
- Who pays inference cost in public worlds?
- Per-world API key?
- Shared hosted gateway?
- Free quota / BYOK / paid hosted tier?

## Voice
- Can a zero-install player experience natural voice interaction?
- If not, what is the least invasive optional bridge?
- How can TTS be spatialized and lip-synced?
