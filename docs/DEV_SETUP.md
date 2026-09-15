# Development Baseline

Verified from the current VRChat Creator documentation on **2026-09-16**.

This document defines the environment for Issue #2. It deliberately separates what is verified from what still requires a real Unity/VRChat build.

## Current supported stack

- **OS for initial world development:** Windows (VRChat's Getting Started guide recommends Windows for the first project).
- **Unity:** **2022.3.22f1** — this remains VRChat's current supported Unity editor version.
- **VRChat SDK:** current live release is **3.10.5** (released 2026-09-04).
- **Project type:** VRChat Creator Companion **World project**.
- **Scripting:** UdonSharp, included with the Worlds SDK.
- **Render pipeline:** Built-in pipeline. Do not create the project with URP/HDRP.

Official references:

- [VRChat Getting Started](https://creators.vrchat.com/sdk/)
- [Current Unity Version](https://creators.vrchat.com/sdk/upgrade/current-unity-version/)
- [VRChat SDK Releases](https://creators.vrchat.com/releases/)
- [Release 3.10.5](https://creators.vrchat.com/releases/release-3-10-5/)
- [Creating Your First World](https://creators.vrchat.com/worlds/creating-your-first-world/)
- [UdonSharp](https://creators.vrchat.com/worlds/udon/udonsharp/)

## Bootstrap procedure

This must be executed on a machine with the VRChat Creator Companion and Unity installed. The repository automation environment does not currently provide a Unity editor, so these steps are documented but **not yet claimed as executed**.

1. Install/update the VRChat Creator Companion.
2. Ensure Unity `2022.3.22f1` is installed through VCC/Unity Hub.
3. In VCC, choose **Create New Project -> World project**.
4. Create the project in a temporary directory using the latest live Worlds SDK managed by VCC.
5. Confirm the project opens in Unity `2022.3.22f1`.
6. Confirm the Unity target is `PC, Mac & Linux Standalone <DX11>` for the first PC test.
7. Open `VRChat SDK -> Show Control Panel` and verify the Builder panel is healthy.
8. Create/open the default VRChat world scene and ensure a `VRCSceneDescriptor` exists.
9. Enter Play Mode to verify ClientSim starts without compile errors.
10. Use **Build & Test** before any public upload.

## Repository import strategy

Do **not** commit Unity-generated caches (`Library/`, `Temp/`, `Obj/`, logs, IDE files). The existing `.gitignore` already excludes these.

The intended repository shape is:

```text
vrc-embodied-companion/
├── Packages/
│   └── com.uynewnas.vrc-embodied-companion/   # reusable framework package
├── World/                                      # minimal demo/test world project (to be added by #2)
│   ├── Assets/
│   ├── Packages/
│   └── ProjectSettings/
└── docs/
```

The VPM framework package and the demo/test world should remain separable: creators eventually consume the package without copying the demo project.

## Issue #2 minimum scene

The first committed Unity scene should contain only enough to validate the architecture:

- one floor and simple room boundary;
- one spawn point / `VRCSceneDescriptor`;
- one placeholder companion body (primitive mesh is fine);
- one `VRCPlayerObject` template reserved for per-player companion state;
- one UdonSharp debug component;
- no production model, shaders, LLM integration, or elaborate UI.

The scene exists to prove lifecycle and testing flow, not aesthetics.

## Acceptance sequence

### A. Editor / ClientSim

Pass if:

- project imports with no compile errors;
- default scene enters Play Mode;
- UdonSharp compiles;
- local placeholder behavior is observable;
- PlayerObject and Persistence debug windows can be opened.

ClientSim is **not** sufficient for multi-user networking acceptance.

### B. VRChat Build & Test — one client

Pass if:

- world builds and launches in VRChat;
- local player can see the placeholder companion;
- Udon logs show expected initialization;
- no SDK validation blockers appear.

### C. VRChat Build & Test — two clients

Pass if:

- each client gets its own logical PlayerObject-backed companion state;
- ownership is correct for each PlayerObject;
- one client leaving does not corrupt the other's state;
- EXP-01 from `PLATFORM_CONSTRAINTS.md` can be executed for private presentation.

Only after A+B+C should Issue #2 be considered fully validated.

## Cross-platform note

Quest/Android support is intentionally **not** a blocker for the first lifecycle prototype. VRChat's setup docs say to install Android Build Support when targeting Quest/Android; we should add that before the public demo milestone, then profile shaders, world size, navigation, and companion model separately on mobile hardware.

## Current blocker

The repository is ready for a real Unity bootstrap, but this automation environment cannot launch the VRChat Creator Companion, Unity Editor, or VRChat Build & Test. Therefore this run does not claim a successful build. The next executable step for #2 is importing a VCC-created minimal World project into `World/` and validating the acceptance sequence above.