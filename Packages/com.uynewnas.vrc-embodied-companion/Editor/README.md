# Editor

Creator tooling lives here. Current implemented scaffolding:

- `CompanionRouteTableImporter` reads the reviewed `unity-route-table.v0.1` JSON artifact;
- rejects unsupported schema/index versions, invalid persona IDs, non-288 tables, route/index/path drift, and unsafe base URLs;
- recomputes every v0.1 route from its array index instead of trusting table ordering;
- combines separately configured HTTPS live/static base URLs with reviewed relative paths;
- constructs the two parallel `VRCUrl[288]` arrays only in editor code.

The importer currently returns an in-memory `CompiledRouteTable`. It intentionally does **not** guess how a future World prefab/UdonSharp transport component should serialize those arrays. That binding step remains blocked on #2's runnable VCC World project.

Not yet verified:

- Unity compilation against the actual VRChat SDK package;
- inspector/prefab serialization of the generated `VRCUrl[]` arrays;
- `VRCStringDownloader` runtime behavior.

Planned creator tooling also includes persona configuration, animation/action mapping, prefab validation, debug panels, package diagnostics, and example-scene setup.
