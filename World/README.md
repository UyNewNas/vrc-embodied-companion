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

The VPM dependency shape mirrors VRChat's maintained `vrchat-community/template-world` (`com.vrchat.base` + `com.vrchat.worlds`, `3.x.x`). The project manifest additionally links this repository's package through a relative local UPM dependency:

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

`vpm check project World` proves that the sparse `World/` directory is recognized as a compatible VRChat project. `vpm resolve project World` then restores the official VPM packages from `Packages/vpm-manifest.json`. CI additionally checks that `com.vrchat.base` and `com.vrchat.worlds` are materialized and that the repository-local framework dependency remains intact.

This preflight is deliberately narrower than a Unity/VCC runtime claim: it does not compile C#, import assets, execute the scene bootstrap, run SDK validation, or launch VRChat Build & Test.

## First real open

1. Add the `World/` directory as an existing Worlds project in VCC and open it with Unity **2022.3.22f1**.
2. Let VCC/Unity resolve `com.vrchat.base` and `com.vrchat.worlds` completely.
3. Run **VRC Companion > Create or Reset Minimal Test World**.
4. Open `Assets/Scenes/CompanionMinimal.unity` if Unity did not leave it active.
5. Open **VRChat SDK > Show Control Panel** and run **Build & Test**.

The bootstrap command creates, deterministically:

- one `VRCWorld` object with a current SDK `VRCSceneDescriptor` discovered from the loaded SDK assembly;
- one spawn transform assigned to the descriptor;
- a small floor/three-wall test room;
- one capsule named `CompanionPlaceholder`;
- one directional light;
- one enabled build scene at `Assets/Scenes/CompanionMinimal.unity`.

The descriptor type is looked up at editor time instead of hard-coding an SDK assembly reference into the bootstrap script. If the Worlds SDK has not resolved, the command fails closed with an explicit error rather than silently producing a non-VRChat scene.

## Evidence still required before closing #2

A clean Windows CI runner now recognizes this directory with the official VPM CLI and resolves the declared VRChat packages. The branch is still **source/bootstrap evidence only** until somebody opens it in the supported Unity/VCC environment. Do not claim any of the following until actually observed:

- VCC/Unity imports the resolved project without errors;
- the editor script compiles and creates the scene;
- Unity resolves the repository-local package on Windows from a fresh clone;
- `VRCSceneDescriptor.spawns` survives serialization/reopen;
- the scene passes VRChat SDK validation;
- Build & Test launches the world;
- the placeholder is visible and the spawn lands inside the room.

After the first successful open, commit only stable project artifacts that improve reproducibility. Keep `Library/`, `Temp/`, `Logs/`, generated IDE files, and other machine-local state ignored.
