# Real Unity smoke execution gate

Status checked: 2026-09-17.

Issue #2 already has a reproducible `World/Tools/Invoke-UnitySmoke.ps1` runner. The remaining gap is not another source-level surrogate: it is executing that runner with the exact VRChat-supported Unity editor and a valid Unity license.

## Platform facts

- VRChat currently requires **Unity 2022.3.22f1** for supported content creation.
- GitHub's current `windows-2022` hosted-runner inventory does **not** list Unity Editor as preinstalled software.
- Unity documents `-version` as a command-line argument that prints the editor version without opening the editor.
- Unity still requires an activated license to use the Editor. For the 2022.3 editor line, command-line serial activation is for Plus/Pro; Personal activation is handled through Unity Hub.
- GitHub warns that long-lived self-hosted runners are risky for public repositories. Keep this repository's Unity runner temporary/manual-only and do not expose its registration token or Unity credentials in repository files or logs.

Sources:

- https://creators.vrchat.com/sdk/upgrade/current-unity-version/
- https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md
- https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html
- https://docs.unity3d.com/2022.3/Documentation/Manual/ManagingYourUnityLicense.html
- https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners
- https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/use-in-a-workflow

## Decision

Do **not** pretend that a GitHub-hosted `windows-latest` source/VPM job is a Unity runtime result, and do not add Unity account credentials or serials to repository code in order to force a hosted-runner activation path.

The repository therefore provides a manual-only workflow:

`.github/workflows/unity-smoke-self-hosted.yml`

It requires a self-hosted Windows x64 runner carrying the custom label:

`unity-2022.3.22f1`

The runner must already have an activated Unity **2022.3.22f1** installation. The workflow defaults to the normal Unity Hub path and also accepts an explicit `unity_path` when dispatched.

The workflow itself should be dispatched from trusted `master`, while the required `target_sha` input names the immutable checkout under test. `target_sha` must be a **full 40-character commit SHA**. This avoids a branch moving between runner preparation and execution, and it lets a reviewed PR head be tested without taking the workflow definition from that PR branch. Do not use the temporary runner to execute unreviewed fork commits.

Before the real Unity smoke opens/imports the project, the workflow:

1. checks out the exact `target_sha` supplied by the operator;
2. verifies `git rev-parse HEAD` equals that full 40-character SHA and exports it as `UNITY_SMOKE_TARGET_SHA`;
3. initializes the evidence directory and reruns the checked-in host preflight from that exact target checkout;
4. saves the preflight result as `host-preflight.json` and fail-closes unless it certifies Windows/x64, the exact `2022.3.22f1` editor, and the World smoke source contract;
5. installs the pinned VRChat VPM CLI (`0.1.28`), installs official templates, and resolves the committed World project;
6. requires `com.vrchat.base == 3.10.5` and `com.vrchat.worlds == 3.10.5`, and requires VPM resolution to leave tracked World sources clean;
7. records immutable runner/ref/SDK/editor context, including separate `workflow_sha` and `tested_sha` fields;
8. executes the real `Invoke-UnitySmoke.ps1` import/compile/bootstrap/save-reopen check;
9. uploads `host-preflight.json`, the runner context, Unity log, and JSON smoke summary even when the smoke step fails.

A separate hosted `Unity smoke workflow contract` check verifies that this execution workflow stays manual-only, self-hosted, version-pinned, immutable-targeted, evidence-producing, and free of embedded Unity activation credentials. It also locks the requirement that the real workflow itself executes `Test-UnitySmokeHost.ps1`; the operator-only preflight is not accepted as a substitute.

## Prepare the Windows host before registering it

On the intended Windows x64 machine, clone/check out the exact commit you want to test and run:

```powershell
.\World\Tools\Test-UnitySmokeHost.ps1
```

If Unity is installed somewhere else:

```powershell
.\World\Tools\Test-UnitySmokeHost.ps1 -UnityPath 'D:\Unity\2022.3.22f1\Editor\Unity.exe' -OutputPath '.artifacts\unity-host-preflight.json'
```

The operator-side preflight is still recommended because it catches a bad host before a temporary runner is registered. The workflow then **reruns the checked-in host preflight after the immutable target SHA has been checked out** and preserves `host-preflight.json` with the run evidence. This prevents a skipped manual step or a stale/misapplied runner label from silently becoming runtime evidence.

The preflight validates Windows/x64, the exact Unity editor version, the committed World Unity version, and the repository smoke-runner contract. It intentionally records `unity_license_verified=false`: `Unity.exe -version` is not proof that the license can actually open the project. `host-preflight.json` is therefore host/toolchain evidence, **not** Unity activation, import, compile, SDK-validation, or Build & Test evidence. Only the real smoke invocation may establish the import/compile/save-reopen portion.

Record the exact target before registering the runner:

```powershell
$targetSha = (git rev-parse HEAD).Trim()
if ($targetSha -notmatch '^[0-9a-f]{40}$') { throw "Expected a full commit SHA, got $targetSha" }
$targetSha
```

Use that exact value as the workflow's `target_sha` input. Do not substitute a mutable branch name.

## Temporary self-hosted runner setup

Because this is a public repository, prefer a **temporary runner used only for the manual smoke** rather than leaving a general-purpose development machine attached indefinitely.

1. Open repository **Settings -> Actions -> Runners -> New self-hosted runner** and choose Windows/x64.
2. Use the exact download/configuration commands GitHub generates there. The registration token is time-limited; do not paste it into an issue, commit, workflow, or log.
3. During initial configuration, add the custom label `unity-2022.3.22f1`. GitHub automatically supplies the normal `self-hosted`, `windows`, and `x64` labels for a standard Windows x64 runner.
4. Keep the runner process active until GitHub shows it online/listening for jobs.
5. Open **Actions -> Unity 2022.3.22f1 smoke (self-hosted)**, choose **Run workflow from `master`**, and paste the reviewed full 40-character commit SHA into `target_sha`. For PR #22, use its exact current head SHA rather than the branch name.
6. Preserve the uploaded evidence artifact. Confirm `host-preflight.json` belongs to the intended target/toolchain and `runner-context.json` contains the intended `tested_sha`, then remove/unregister the temporary runner when this validation session is finished.

GitHub's current Windows guidance recommends `C:\actions-runner` when installing the runner application as a service. A service is not required for this one-shot smoke; an interactive temporary runner is easier to remove after the evidence is captured.

## Minimal experiment

Register or reuse one Windows x64 self-hosted GitHub Actions runner that has Unity 2022.3.22f1 already activated, add the `unity-2022.3.22f1` label, then run the workflow from `master` with the exact reviewed full 40-character commit SHA as `target_sha`.

A successful run is real evidence for:

- the exact `tested_sha` recorded in the artifact;
- the workflow-enforced host/toolchain preflight recorded in `host-preflight.json`;
- VPM package resolution on the execution machine;
- Unity project import;
- C#/UdonSharp compilation reached by opening the project;
- execution of `CreateSaveReopenAndVerifyMinimalWorld`;
- serialized scene save/reopen verification.

It is **not** evidence for VRChat SDK validation, Build & Test, client-visible spawn/placeholder behavior, Quest behavior, or the two-client lifecycle matrix in #12.

If Unity hits the known 2022.3.22f1 `UUM-57742` batchmode/OpenScene crash signature, keep the uploaded log as engine-limit evidence and execute the same create/save/reopen verifier interactively in the supported editor. Do not silently switch Unity versions.
