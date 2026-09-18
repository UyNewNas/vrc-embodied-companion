# Editor

Creator tooling lives here. Current implemented scaffolding:

- `CompanionRouteTableImporter` reads the reviewed `unity-route-table.v0.1` JSON artifact;
- rejects unsupported schema/index versions, invalid persona IDs, non-288 tables, route/index/path drift, and unsafe base URLs;
- recomputes every v0.1 route from its array index instead of trusting table ordering;
- combines separately configured HTTPS live/static base URLs with reviewed relative paths;
- constructs the two parallel `VRCUrl[288]` arrays only in editor code;
- `CompanionRouteTableBinder` copies a validated compiled table onto the minimal `CompanionTransportRouteBinding` UdonSharp component;
- binding writes schema/index versions, persona ID, route count, and cloned live/static URL arrays, with Unity Undo/dirty/prefab-instance recording.

This freezes the first concrete Unity serialization boundary without claiming transport runtime success. The binding component contains only immutable route-table fields; downloader cadence, callback handling, JSON parsing, and behavior application remain separate runtime work for #18 once #2 provides a runnable VCC World project.

Not yet verified:

- Unity compilation against the actual VRChat SDK/UdonSharp packages;
- inspector/prefab serialization of the generated `VRCUrl[]` arrays;
- `VRCStringDownloader` runtime behavior;
- Build & Test behavior on PC or Quest.

Planned creator tooling also includes persona configuration, animation/action mapping, prefab validation, debug panels, package diagnostics, and example-scene setup.
