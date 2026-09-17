# PlayerObject lifecycle runtime matrix v0.1

Status: **executable protocol / runtime evidence pending**  
Owner: Issue #12  
Implementation under test: Issue #4 / PR #22  
World prerequisite: Issue #2

This document turns the current `CompanionPlayerLifecycle` source contract into a falsifiable VRChat runtime test. It is intentionally evidence-first: a green source/SDK workflow is not accepted as Unity/UdonSharp or VRChat runtime proof.

## 1. Platform facts used by the test

Rechecked against VRChat Creator documentation on 2026-09-17:

- VRChat creates a PlayerObject copy for each joining player.
- The associated player owns that PlayerObject and ownership is not transferable.
- PlayerObject ownership is guaranteed correct in `Start` and `OnDeserialization`.
- `OnPlayerRestored` runs once for every player in the instance; only the matching owner event may unlock that PlayerObject's restored state.
- Player references become invalid after a player leaves.
- local Build & Test with multiple clients gives each test client separate local User Data.
- ClientSim does not reproduce full multi-client networking and cannot close #12.

Sources:

- <https://creators.vrchat.com/worlds/udon/persistence/player-object/>
- <https://creators.vrchat.com/worlds/udon/persistence/>
- <https://creators.vrchat.com/worlds/udon/players/>
- <https://creators.vrchat.com/worlds/clientsim/>

## 2. Required build under test

Use the exact branch/commit being reviewed. Before any networking claim, record:

- git commit SHA;
- Unity version (`2022.3.22f1` for the current project baseline);
- resolved VRChat SDK version;
- client mode and number of clients;
- whether this is local Build & Test or an uploaded world;
- VRChat client version/date.

First run the repository smoke command on the same checkout:

```powershell
pwsh -File .\World\Tools\Invoke-UnitySmoke.ps1
```

The smoke result must show real Unity execution and a successful `CreateSaveReopenAndVerifyMinimalWorld` result. Do not substitute `-ValidateOnly`, VPM resolution, or GitHub source-contract workflows for this prerequisite.

## 3. Runtime observables

For every PlayerObject copy capture, at minimum:

- client identity / test-client number;
- actual `Networking.GetOwner(gameObject).playerId`;
- `associatedPlayerId`;
- `lifecycleState`;
- `restoreObserved`;
- `localOwner`;
- `restoreEventCount`;
- `ignoredRestoreEventCount`;
- `ownerMismatchCount`;
- `detachCount`;
- `lastLifecycleAction`.

If a temporary debug UI is added for the run, keep it development-only and record the exact commit containing it. Console logs are acceptable if they contain the same fields and can be attributed to a specific PlayerObject.

## 4. Core two-client matrix

Start local Build & Test with at least two clients, A and B.

| Case | Action | Required observation |
|---|---|---|
| Spawn isolation | Let A and B join | A and B receive distinct runtime PlayerObject copies; no scene system uses the disabled template as a live instance. |
| Ownership | Inspect both copies from both clients | each copy's actual owner matches its associated player; no ownership-transfer repair occurs. |
| Restore fan-out | Record every `OnPlayerRestored` received by each copy | unrelated player restore callbacks are ignored; only the callback matching the actual owner reaches `ready`. |
| Local mutation gate | Invoke a normal local-owner mutation on A, then attempt the equivalent path against B's copy | A's local copy may mutate when ready; B's copy is rejected on A. Reverse the test from B. |
| Explicit disable | Disable A's companion, then provoke/reobserve restore-related lifecycle activity | A remains `disabled`; a repeated restore signal must not silently return it to `ready`. |
| Leave cleanup | Close/remove A while B remains | A's old lifecycle becomes unusable/detached or is destroyed; B remains valid and unchanged. A stale `VRCPlayerApi` must not keep A discoverable. |
| Rejoin | Rejoin A as a fresh test client lifecycle | a new PlayerObject lifecycle is created for the rejoined client; an old detached instance is never reused as the new companion. |
| Cross-player isolation | Change only A's relationship/test state | B's state and lifecycle counters do not change except for expected global callback observations. |

Any failure is a blocker for closing #4, even when source-contract CI is green.

## 5. Regression probes for the eight pre-runtime defects caught in PR #22

These probes exist so the fixes found by source review are tested as behavior rather than merely preserved as strings in CI.

1. **Disabled preservation during owner refresh**  
   Put A in `disabled`, trigger any path that calls `RefreshLifecycleOwner`, and verify state stays `disabled` while the owner remains valid.

2. **Correct UdonSharp editor wiring**  
   Open the generated scene after save/reopen and verify the PlayerObject root has a functioning Udon behaviour/program for `CompanionPlayerLifecycle`, and `CompanionRuntime` has a functioning `CompanionPlayerLookup`. Missing/broken backing Udon setup is a failure.

3. **Invalid leave callback cleanup**  
   During A leave, verify the old A lifecycle cannot remain discoverable as ready/disabled merely because the callback player reference became invalid.

4. **Detached is terminal for lookup**  
   After detach, call the per-player lookup path for the departed player/reference where possible. A retained diagnostic `associatedPlayerId` must not make the dead lifecycle a hit.

5. **Owner drift fails closed**  
   No test should deliberately transfer PlayerObject ownership. If instrumentation or an unexpected SDK condition ever reports actual owner != bound `associatedPlayerId`, the lifecycle must detach rather than rebind. Record the full event if observed.

6. **Repeated restore does not re-enable**  
   A disabled companion must remain disabled across any duplicate/replayed matching restore observation seen in the test environment.

7. **Missing owner after binding fails closed**  
   If the actual owner becomes invalid after a lifecycle has bound/restored, the old copy must not remain ready/disabled and discoverable.

8. **`Start` owner guarantee is enforced**  
   For every normal runtime PlayerObject copy, capture the owner identity at/after `Start`. An ownerless runtime copy is a hard failure, not an accepted transient state.

Some invariant-violation branches may be difficult or impossible to force through normal VRChat UI. They do not need fabricated reproduction. Mark such rows **not naturally inducible**, keep the source guard, and rely on the normal-path observations plus any real anomaly if one occurs.

## 6. Persistence/reload boundary

#12 should not conflate lifecycle isolation with the full #7 persistence feature, but one reload observation is required:

1. after the matching restore event, set one explicitly approved non-sensitive test value;
2. use Build & Reload / rejoin according to VRChat's documented local-test behavior;
3. verify the expected client restores only its own value;
4. verify the other test client does not inherit it.

Do not use intimate memory, transcript content, or hidden affinity data as the test value.

## 7. Evidence package

Attach or link the following to #12:

- commit SHA and environment versions;
- Unity smoke summary/log from the same checkout;
- screenshots or logs showing A/B PlayerObject identity and owner IDs;
- ordered lifecycle event log for join -> restore -> disable/enable -> leave -> rejoin;
- before/after local mutation values for both clients;
- Build & Reload/rejoin observation;
- all SDK/Udon warnings or errors;
- explicit list of matrix rows: pass / fail / not naturally inducible.

A short textual conclusion should distinguish:

- **Unity compile/serialization passed**;
- **one-client lifecycle passed**;
- **two-client isolation passed**;
- **persistence reload observation passed**;
- **presentation privacy not tested here** (belongs to #11).

## 8. Completion rule

Issue #12 may close only after a real two-client VRChat run has evidence for the core matrix. Passing GitHub Actions, VPM resolution, source-contract checks, SDK surface checks, or ClientSim alone is insufficient.

Issue #4 may then use #12 as its runtime isolation evidence, while owner-only visual/audio presentation remains separately gated by #11.
