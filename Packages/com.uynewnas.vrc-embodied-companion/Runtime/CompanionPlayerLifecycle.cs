using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Minimal PlayerObject lifecycle anchor for issue #4.
    ///
    /// Intended placement: on the same root GameObject as a VRCPlayerObject template. VRChat creates
    /// a runtime copy for every player. This component never transfers ownership and does not claim
    /// presentation privacy; it only exposes logical lifecycle readiness and local-mutation gates.
    ///
    /// Runtime/two-client verification remains required by issues #2 and #12.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CompanionPlayerLifecycle : UdonSharpBehaviour
    {
        public const int StateSpawned = 0;
        public const int StateRestoring = 1;
        public const int StateReady = 2;
        public const int StateDisabled = 3;
        public const int StateDetached = 4;

        [Header("Lifecycle state")]
        public int lifecycleState = StateSpawned;
        public int associatedPlayerId = -1;
        public bool restoreObserved;
        public bool localOwner;

        [Header("Debug counters")]
        public int restoreEventCount;
        public int ignoredRestoreEventCount;
        public int ownerMismatchCount;
        public int detachCount;
        public string lastLifecycleAction = "bootstrap";

        private VRCPlayerApi associatedPlayer;

        private void Start()
        {
            RefreshOwner("start");

            // VRChat guarantees PlayerObject ownership is correct in Start. Reaching Start without
            // a valid owner is therefore not a normal startup-order condition for a runtime copy;
            // fail closed instead of leaving an ownerless lifecycle in StateSpawned indefinitely.
            if (lifecycleState == StateSpawned && associatedPlayerId < 0)
            {
                MarkDetached("start_owner_unavailable");
            }
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            restoreEventCount++;

            if (lifecycleState == StateDetached)
            {
                ignoredRestoreEventCount++;
                lastLifecycleAction = "restore_ignored_detached";
                return;
            }

            if (!Utilities.IsValid(player))
            {
                ignoredRestoreEventCount++;
                lastLifecycleAction = "restore_ignored_invalid_player";
                return;
            }

            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            if (!Utilities.IsValid(owner))
            {
                ignoredRestoreEventCount++;

                // Before the PlayerObject has ever been bound, an unavailable owner can still be a
                // startup-order condition. After binding/restoration, however, losing the actual
                // owner is an invariant violation for a non-transferable PlayerObject and must fail
                // closed instead of leaving a previously ready/disabled lifecycle discoverable.
                if (associatedPlayerId >= 0 || restoreObserved)
                {
                    MarkDetached("restore_owner_invalid_after_binding");
                    return;
                }

                lastLifecycleAction = "restore_ignored_invalid_owner";
                return;
            }

            // OnPlayerRestored is emitted once for every player in the instance, so unrelated restore
            // events are expected. However, a PlayerObject owner is not transferable: once this
            // lifecycle has bound to a player, observing a different actual owner is an invariant
            // violation and must fail closed instead of silently rebinding the lifecycle.
            if (associatedPlayerId >= 0 && associatedPlayerId != owner.playerId)
            {
                ignoredRestoreEventCount++;
                ownerMismatchCount++;
                MarkDetached("restore_owner_changed_unexpectedly");
                return;
            }

            if (owner.playerId != player.playerId)
            {
                ignoredRestoreEventCount++;
                ownerMismatchCount++;
                lastLifecycleAction = "restore_ignored_owner_mismatch";
                return;
            }

            CacheOwner(owner);
            restoreObserved = true;

            // A duplicate/replayed restore signal must not undo an explicit local disable. Disabled
            // remains a presentation/interaction choice layered on top of a successfully restored
            // PlayerObject, and only EnableForLocalOwner() may transition it back to ready.
            if (lifecycleState == StateDisabled)
            {
                lastLifecycleAction = "restore_observed_disabled";
                return;
            }

            lifecycleState = StateReady;
            lastLifecycleAction = "ready_after_restore";
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (lifecycleState == StateDetached)
            {
                return;
            }

            if (Utilities.IsValid(player)
                && associatedPlayerId >= 0
                && player.playerId == associatedPlayerId)
            {
                MarkDetached("owner_left");
                return;
            }

            // VRCPlayerApi references may already be invalid by the time leave cleanup runs. Do not
            // silently ignore that case: re-check the actual PlayerObject owner. This is a no-op for
            // an unrelated departure while our owner remains valid, and fail-closes to detached when
            // this PlayerObject has lost its owner or ownership changed unexpectedly.
            RefreshOwner("player_left");
        }

        public bool IsReady()
        {
            if (lifecycleState != StateReady)
            {
                return false;
            }

            return ValidateAssociatedPlayer();
        }

        public bool CanRunPlayerFacingBehavior()
        {
            return IsReady();
        }

        public bool CanMutateLocalState()
        {
            if (!IsReady())
            {
                return false;
            }

            return IsLocalOwnerNow();
        }

        public bool MatchesPlayer(VRCPlayerApi player)
        {
            if (lifecycleState == StateDetached)
            {
                return false;
            }

            if (!Utilities.IsValid(player) || associatedPlayerId < 0)
            {
                return false;
            }

            return player.playerId == associatedPlayerId;
        }

        public VRCPlayerApi GetAssociatedPlayer()
        {
            if (!ValidateAssociatedPlayer())
            {
                return null;
            }

            return associatedPlayer;
        }

        public void DisableForLocalOwner()
        {
            if (!CanMutateLocalState())
            {
                lastLifecycleAction = "disable_rejected_not_local_ready";
                return;
            }

            lifecycleState = StateDisabled;
            lastLifecycleAction = "disabled_by_local_owner";
        }

        public void EnableForLocalOwner()
        {
            if (lifecycleState != StateDisabled)
            {
                lastLifecycleAction = "enable_rejected_not_disabled";
                return;
            }

            if (!ValidateAssociatedPlayer() || !IsLocalOwnerNow() || !restoreObserved)
            {
                lastLifecycleAction = "enable_rejected_not_local_restored";
                return;
            }

            lifecycleState = StateReady;
            lastLifecycleAction = "enabled_by_local_owner";
        }

        public void RefreshLifecycleOwner()
        {
            RefreshOwner("manual_refresh");
        }

        private void RefreshOwner(string reason)
        {
            if (lifecycleState == StateDetached)
            {
                return;
            }

            bool preserveDisabled = lifecycleState == StateDisabled && restoreObserved;
            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            if (!Utilities.IsValid(owner))
            {
                if (associatedPlayerId >= 0 || restoreObserved)
                {
                    MarkDetached(reason + "_owner_invalid");
                    return;
                }

                associatedPlayer = null;
                associatedPlayerId = -1;
                localOwner = false;
                lifecycleState = StateSpawned;
                lastLifecycleAction = reason + "_owner_unavailable_initially";
                return;
            }

            if (associatedPlayerId >= 0 && associatedPlayerId != owner.playerId)
            {
                ownerMismatchCount++;
                MarkDetached("owner_changed_unexpectedly");
                return;
            }

            CacheOwner(owner);
            if (preserveDisabled)
            {
                lifecycleState = StateDisabled;
                lastLifecycleAction = reason + "_owner_valid_disabled";
                return;
            }

            lifecycleState = restoreObserved ? StateReady : StateRestoring;
            lastLifecycleAction = restoreObserved
                ? reason + "_owner_valid_ready"
                : reason + "_owner_valid_restoring";
        }

        private void CacheOwner(VRCPlayerApi owner)
        {
            associatedPlayer = owner;
            associatedPlayerId = owner.playerId;
            localOwner = IsLocalPlayer(owner);
        }

        private bool ValidateAssociatedPlayer()
        {
            if (lifecycleState == StateDetached)
            {
                return false;
            }

            if (!Utilities.IsValid(associatedPlayer))
            {
                MarkDetached("cached_owner_invalid");
                return false;
            }

            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            if (!Utilities.IsValid(owner))
            {
                MarkDetached("runtime_owner_invalid");
                return false;
            }

            if (owner.playerId != associatedPlayerId)
            {
                ownerMismatchCount++;
                MarkDetached("runtime_owner_mismatch");
                return false;
            }

            localOwner = IsLocalPlayer(owner);
            return true;
        }

        private bool IsLocalOwnerNow()
        {
            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            if (!Utilities.IsValid(owner))
            {
                return false;
            }

            localOwner = IsLocalPlayer(owner);
            return localOwner;
        }

        private bool IsLocalPlayer(VRCPlayerApi player)
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            return Utilities.IsValid(player)
                && Utilities.IsValid(localPlayer)
                && player.playerId == localPlayer.playerId;
        }

        private void MarkDetached(string reason)
        {
            lifecycleState = StateDetached;
            associatedPlayer = null;
            localOwner = false;
            detachCount++;
            lastLifecycleAction = reason;
        }
    }
}
