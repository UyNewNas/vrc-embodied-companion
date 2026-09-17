# Real Unity smoke execution gate

Status checked: 2026-09-17.

Issue #2 already has a reproducible `World/Tools/Invoke-UnitySmoke.ps1` runner. The remaining gap is not another source-level surrogate: it is executing that runner with the exact VRChat-supported Unity editor and a valid Unity license.

## Platform facts

- VRChat currently requires **Unity 2022.3.22f1** for supported content creation.
- GitHub's current `windows-2022` hosted-runner inventory does **not** list Unity Editor as preinstalled software.
- Unity documents `-version` as a command-line argument that prints the editor version without opening the editor.
- Unity still requires an activated license to use the Editor. For the 2022.3 editor line, command-line serial activation is for Plus/Pro; Personal activation is handled through Unity Hub.

Sources:

- https://creators.vrchat.com/sdk/upgrade/current-unity-version/
- https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md
- https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/ManagingYourUnityLicense.html

## Decision

Do **not** pretend that a GitHub-hosted `windows-latest` source/VPM job is a Unity runtime result, and do not add Unity account credentials or serials to repository code in order to force a hosted-runner activation path.

The repository therefore provides a manual-only workflow:

`.github/workflows/unity-smoke-self-hosted.yml`

It requires a self-hosted Windows x64 runner carrying the custom label:

`unity-2022.3.22f1`

The runner must already have an activated Unity **2022.3.22f1** installation. The workflow defaults to the normal Unity Hub path and also accepts an explicit `unity_path` when dispatched.

Before Unity opens the project, the workflow:

1. checks out the exact GitHub SHA under test;
2. installs the pinned VRChat VPM CLI (`0.1.28`);
3. installs official VPM templates and resolves the committed World project;
4. requires `com.vrchat.base == 3.10.5` and `com.vrchat.worlds == 3.10.5`;
5. requires the VPM resolve to leave tracked World sources clean;
6. runs `Unity.exe -version` and rejects anything other than `2022.3.22f1`;
7. records immutable runner/ref/SDK/editor context;
8. executes the real `Invoke-UnitySmoke.ps1` import/compile/bootstrap/save-reopen check;
9. uploads the Unity log, JSON smoke summary, and runner context even when the smoke step fails.

A separate hosted `Unity smoke workflow contract` check verifies that this execution workflow stays manual-only, self-hosted, version-pinned, evidence-producing, and free of embedded Unity activation credentials.

## Minimal experiment

Register or reuse one Windows x64 self-hosted GitHub Actions runner that has Unity 2022.3.22f1 already activated, add the `unity-2022.3.22f1` label, then manually dispatch **Unity 2022.3.22f1 smoke (self-hosted)** from the commit/branch being tested.

A successful run is real evidence for:

- VPM package resolution on the execution machine;
- Unity project import;
- C#/UdonSharp compilation reached by opening the project;
- execution of `CreateSaveReopenAndVerifyMinimalWorld`;
- serialized scene save/reopen verification.

It is **not** evidence for VRChat SDK validation, Build & Test, client-visible spawn/placeholder behavior, Quest behavior, or the two-client lifecycle matrix in #12.

If Unity hits the known 2022.3.22f1 `UUM-57742` batchmode/OpenScene crash signature, keep the uploaded log as engine-limit evidence and execute the same create/save/reopen verifier interactively in the supported editor. Do not silently switch Unity versions.
