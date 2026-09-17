using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UyNewNas.VRCEmbodiedCompanion;

/// <summary>
/// Development-only observability for the minimal World acceptance run.
///
/// This probe does not make lifecycle decisions and does not sync state. It records the callback
/// player/actual owner immediately, then emits a one-frame-delayed snapshot so the sibling
/// CompanionPlayerLifecycle has had a chance to process the same VRChat event. The resulting
/// Debug.Log lines are intended as evidence for issues #4/#12, not as a production telemetry path.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CompanionPlayerLifecycleDebugProbe : UdonSharpBehaviour
{
    public bool loggingEnabled = true;
    public int snapshotCount;
    public int restoreCallbackCount;
    public int leaveCallbackCount;
    public string lastSnapshotReason = "idle";

    private CompanionPlayerLifecycle lifecycle;

    private void Start()
    {
        lifecycle = GetComponent<CompanionPlayerLifecycle>();
        SendCustomEventDelayedFrames(nameof(CaptureAfterStart), 1);
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        restoreCallbackCount++;
        LogCallback("restore", player);
        SendCustomEventDelayedFrames(nameof(CaptureAfterRestore), 1);
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        leaveCallbackCount++;
        LogCallback("left", player);
        SendCustomEventDelayedFrames(nameof(CaptureAfterLeave), 1);
    }

    public void CaptureAfterStart()
    {
        CaptureSnapshot("after_start");
    }

    public void CaptureAfterRestore()
    {
        CaptureSnapshot("after_restore");
    }

    public void CaptureAfterLeave()
    {
        CaptureSnapshot("after_leave");
    }

    public void CaptureNow()
    {
        CaptureSnapshot("manual");
    }

    private void LogCallback(string eventName, VRCPlayerApi player)
    {
        if (!loggingEnabled)
        {
            return;
        }

        int callbackPlayerId = Utilities.IsValid(player) ? player.playerId : -1;
        VRCPlayerApi owner = Networking.GetOwner(gameObject);
        int actualOwnerId = Utilities.IsValid(owner) ? owner.playerId : -1;

        Debug.Log($"[VRC Companion Lifecycle Callback] event={eventName} object={gameObject.name} callbackPlayerId={callbackPlayerId} actualOwnerId={actualOwnerId} restoreCallbacks={restoreCallbackCount} leaveCallbacks={leaveCallbackCount}");
    }

    private void CaptureSnapshot(string reason)
    {
        if (!loggingEnabled)
        {
            return;
        }

        if (!Utilities.IsValid(lifecycle))
        {
            lifecycle = GetComponent<CompanionPlayerLifecycle>();
        }

        snapshotCount++;
        lastSnapshotReason = reason;

        VRCPlayerApi owner = Networking.GetOwner(gameObject);
        int actualOwnerId = Utilities.IsValid(owner) ? owner.playerId : -1;
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        int localPlayerId = Utilities.IsValid(localPlayer) ? localPlayer.playerId : -1;

        if (!Utilities.IsValid(lifecycle))
        {
            Debug.Log($"[VRC Companion Lifecycle Snapshot] reason={reason} snapshot={snapshotCount} object={gameObject.name} actualOwnerId={actualOwnerId} localPlayerId={localPlayerId} lifecycle=missing");
            return;
        }

        Debug.Log($"[VRC Companion Lifecycle Snapshot] reason={reason} snapshot={snapshotCount} object={gameObject.name} actualOwnerId={actualOwnerId} associatedPlayerId={lifecycle.associatedPlayerId} localPlayerId={localPlayerId} state={lifecycle.lifecycleState} restoreObserved={lifecycle.restoreObserved} localOwner={lifecycle.localOwner} restoreEvents={lifecycle.restoreEventCount} ignoredRestoreEvents={lifecycle.ignoredRestoreEventCount} ownerMismatches={lifecycle.ownerMismatchCount} detaches={lifecycle.detachCount} lastAction={lifecycle.lastLifecycleAction}");
    }
}
