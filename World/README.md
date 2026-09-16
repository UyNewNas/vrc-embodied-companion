# Minimal VRChat World bootstrap

This directory is the runnable-project bootstrap for issue #2. It is intentionally small and keeps generated Unity/VCC state out of the repository until it has been produced by the supported toolchain.

## Verified platform baseline

As checked on 2026-09-16:

- VRChat's supported Unity editor is **2022.3.22f1**.
- VRChat SDK **3.10.5** is the current release (2026-09-04).
- VRChat recommends creating/opening Worlds projects with the **VRChat Creator Companion (VCC)**.
- Every uploadable world scene requires a **VRC Scene Descriptor**.

Official references:

- https://creators.vrchat.com/sdk/upgrade/current-unity-version/
- https://creators.vrchat.com/sdk/
- https://creators.vrchat.com/worlds/creating-your-first-world/
- https://creators.vrchat.com/releases/release-3-10-5/

The VPM project started from VRChat's maintained world-template dependency shape (`com.vrchat.base` + `com.vrchat.worlds`). A clean Windows `vpm resolve` proved that leaving those declarations at `3.x.x` is not a stable post-resolve repository state: VPM rewrites them to the concrete selected SDK and adds a `locked` graph. This repository now commits that resolved state at **3.10.5** so a fresh preflight can be tested for idempotence instead of silently floating to a later 3.x SDK.

The Unity project manifest additionally links this repository's package through a relative local UPM dependency:

`file:../../Packages/com.uynewnas.vrc-embodied-companion`

That path is relative to `World/Packages/manifest.json`, so a normal repository clone keeps the package and test project coupled without copying framework source into `Assets/`.

## Headless VPM preflight

The repository CI performs a Windows VPM-level preflight before anyone opens Unity. VRChat's VPM CLI needs the official templates installed in a fresh profile before package resolution, so the equivalent manual commands are:

```powershell
dotnet tool install --global VRChat.VPM.CLI --version 0.1.28
vpm install templates
vpm check project World
vpm resolve project World
```

`vpm check project World` proves that the sparse `World/` directory is recognized as a compatible VRChat project. `vpm resolve project World` restores the committed 3.10.5 package graph. CI checks that `com.vrchat.base` and `com.vrchat.worlds` materialize as 3.10.5, the repository-local framework dependency remains intact, the committed `dependencies` + `locked` graph is unchanged, and the World source tree remains clean afterwards.

`World/Packages/.gitignore` mirrors VRChat's maintained world-template package policy so VPM-materialized package directories are not accidentally committed. If a future SDK release is intentionally adopted, update the reviewed VPM lock state in its own change rather than letting the #2 bootstrap silently float during resolution.

This preflight is deliberately narrower than a Unity/VCC runtime claim: it does not compile C#, import assets, execute the scene bootstrap, run SDK validation, or launch VRChat Build & Test.

## First real open

1. Add the `World/` directory as an existing Worlds project in VCC and open it with Unity **2022.3.22f1**.
2. Let VCC/Unity resolve `com.vrchat.base` and `com.vrchat.worlds` completely.
3. Run **VRC Companion > Create, Save, Reopen and Verify Minimal Test World**.
4. Confirm the Console contains the serialized-scene verification success message and no compile/import errors.
5. Open **VRChat SDK > Show Control Panel** and run **Build & Test**.

The bootstrap command creates, deterministically:

- one `VRCWorld` object with a compile-time-bound `VRCSceneDescriptor` from the resolved Worlds SDK;
- one spawn transform assigned directly to `descriptor.spawns`;
- a small floor/three-wall test room;
- one capsule named `CompanionPlaceholder`;
- one `CompanionPlayerObjectTemplate` carrying `VRCPlayerObject`, with a child `CompanionPlayerLifecycle` created through UdonSharp's editor API;
- one scene-level `CompanionRuntime` carrying `CompanionPlayerLookup`, also created through UdonSharp's editor API;
- one directional light;
- one enabled build scene at `Assets/Scenes/CompanionMinimal.unity`.

The PlayerObject wiring is deliberately **logical-state integration only**. VRChat's current PlayerObject contract automatically instantiates the template once per joining player and permits Udon behaviours on the template or its children. Presentation privacy remains issue #11, and persistent synced fields remain issue #7; this lifecycle prototype does not add `VRCEnablePersistence`.

The combined verification command saves the scene, reopens it from disk, then fails closed unless the descriptor, its single `Spawn` child, the placeholder, the PlayerObject template, lifecycle component, lookup service, and enabled build-scene entry all survive serialization. This is an **Editor serialization smoke check**, not a substitute for VRChat SDK validation or Build & Test.

PlayerObject/UdonSharp references:

- https://creators.vrchat.com/worlds/udon/persistence/player-object/
- https://creators.vrchat.com/worlds/udon/udonsharp/editorscripting/

## Optional Unity batchmode smoke check

Once Unity **2022.3.22f1** is installed, use the checked-in Windows runner from the repository root:

```powershell
pwsh -File .\World\Tools\Invoke-UnitySmoke.ps1
```

The runner reads the required editor version from `ProjectVersion.txt`, locates the exact Unity Hub install (or honors `UNITY_EDITOR` / `-UnityPath`), invokes `CompanionMinimalWorldBootstrap.CreateSaveReopenAndVerifyMinimalWorld`, and fails unless all of these are true:

- Unity exits with code 0;
- the bootstrap's serialized-scene success marker appears in the Unity log;
- `Assets/Scenes/CompanionMinimal.unity` exists;
- `ProjectSettings/EditorBuildSettings.asset` exists.

It writes machine-local evidence to ignored paths:

- `World/Logs/unity-smoke.log`
- `World/Logs/world-unity-smoke-summary.json`

The JSON summary records the exact editor path/version, timestamps, exit code, success-marker state, generated-artifact checks, and whether the log matches the known `UUM-57742` OpenScene batchmode crash signature. CI executes the same script with `-ValidateOnly`, which validates the project version and execute-method contract without pretending that a Unity editor is available on the hosted runner.

To use a non-default Unity Hub location:

```powershell
pwsh -File .\World\Tools\Invoke-UnitySmoke.ps1 `
  -UnityPath 'D:\Unity\2022.3.22f1\Editor\Unity.exe'
```

Treat batchmode as **best-effort automation, not the authoritative #2 gate**. Unity's own 2022.3.22f1 release notes list **UUM-57742**, `Crash in CollectManagedImportDependencyGetters inside OpenScene in batch mode`, as a known issue. Because the smoke verifier intentionally calls `EditorSceneManager.OpenScene`, a batchmode crash with that signature must be recorded as an engine limitation rather than "fixed" by silently testing a different Unity version. In that case, run the same **Create, Save, Reopen and Verify** menu command in the supported interactive editor and keep the batchmode log as evidence of the known limitation.

Unity references for this caveat:

- https://unity.com/releases/editor/whats-new/2022.3.22f1
- https://issuetracker.unity3d.com/issues/crash-in-collectmanagedimportdependencygetters-inside-openscene-in-batch-mode

A successful batchmode run would still provide real Unity import/compile/bootstrap/serialization evidence. It would **not** prove VRChat SDK validation, client launch, spawn placement in the VRChat client, PlayerObject networking semantics, or PC/Quest behavior.

## Evidence still required before closing #2

A clean Windows CI runner now recognizes this directory with the official VPM CLI and resolves the committed VRChat SDK graph. The branch is still **source/bootstrap evidence only** until somebody opens it in the supported Unity/VCC environment. Do not claim any of the following until actually observed:

- VCC/Unity imports the resolved project without errors;
- UdonSharp compiles the lifecycle/lookup behaviours and the combined bootstrap/verification command succeeds;
- Unity resolves the repository-local package on Windows from a fresh clone;
- `VRCSceneDescriptor.spawns` and the PlayerObject lifecycle wiring survive serialization/reopen (the verifier is the minimum reproducible check for this);
- the scene passes VRChat SDK validation;
- Build & Test launches the world;
- the placeholder is visible and the spawn lands inside the room;
- PlayerObject ownership/restore/isolation behavior passes issue #12's two-client matrix.

After the first successful open, commit only stable project artifacts that improve reproducibility. Keep `Library/`, `Temp/`, `Logs/`, generated IDE files, and other machine-local state ignored.
