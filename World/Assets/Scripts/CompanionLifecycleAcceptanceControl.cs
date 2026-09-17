using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Development-only interaction surface for the #4/#12 PlayerObject acceptance run.
    ///
    /// The generated minimal world creates three collider-backed controls using this behaviour:
    /// local enable/disable toggle, local owner refresh, and a remote mutation rejection probe.
    /// This component deliberately owns no synchronized state and never transfers ownership.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CompanionLifecycleAcceptanceControl : UdonSharpBehaviour
    {
        public const string LocalToggleControlName = "LifecycleControl-LocalToggle";
        public const string LocalRefreshControlName = "LifecycleControl-LocalRefresh";
        public const string RemoteMutationProbeControlName = "LifecycleControl-RemoteMutationProbe";
        public const string LogPrefix = "[VRC Companion Lifecycle Control]";

        [Header("Development-only evidence")]
        public int interactionCount;
        public string lastControlResult = "idle";

        public override void Interact()
        {
            interactionCount++;

            if (gameObject.name == LocalToggleControlName)
            {
                RunLocalToggle();
                return;
            }

            if (gameObject.name == LocalRefreshControlName)
            {
                RunLocalRefresh();
                return;
            }

            if (gameObject.name == RemoteMutationProbeControlName)
            {
                RunRemoteMutationProbe();
                return;
            }

            lastControlResult = "unknown_control";
            Debug.Log(LogPrefix + " result=unknown_control control=" + gameObject.name);
        }

        private void RunLocalToggle()
        {
            CompanionPlayerLookup lookup = FindLookup();
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(lookup) || !Utilities.IsValid(localPlayer))
            {
                LogUnavailable("local_toggle", "lookup_or_local_player_unavailable");
                return;
            }

            CompanionPlayerLifecycle lifecycle = lookup.FindForPlayer(localPlayer);
            if (!Utilities.IsValid(lifecycle))
            {
                LogUnavailable("local_toggle", "local_lifecycle_not_found");
                return;
            }

            int beforeState = lifecycle.lifecycleState;
            string beforeAction = lifecycle.lastLifecycleAction;
            if (beforeState == CompanionPlayerLifecycle.StateReady)
            {
                lifecycle.DisableForLocalOwner();
                string result = "local_toggle_unexpected";
                if (lifecycle.lifecycleState == CompanionPlayerLifecycle.StateDisabled
                    && lifecycle.lastLifecycleAction == "disabled_by_local_owner")
                {
                    result = "local_disable_succeeded";
                }

                LogLifecycle("local_toggle", result, localPlayer, lifecycle, beforeState, beforeAction);
                return;
            }

            if (beforeState == CompanionPlayerLifecycle.StateDisabled)
            {
                lifecycle.EnableForLocalOwner();
                string result = "local_toggle_unexpected";
                if (lifecycle.lifecycleState == CompanionPlayerLifecycle.StateReady
                    && lifecycle.lastLifecycleAction == "enabled_by_local_owner")
                {
                    result = "local_enable_succeeded";
                }

                LogLifecycle("local_toggle", result, localPlayer, lifecycle, beforeState, beforeAction);
                return;
            }

            LogLifecycle("local_toggle", "local_toggle_not_ready", localPlayer, lifecycle, beforeState, beforeAction);
        }

        private void RunLocalRefresh()
        {
            CompanionPlayerLookup lookup = FindLookup();
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(lookup) || !Utilities.IsValid(localPlayer))
            {
                LogUnavailable("local_refresh", "lookup_or_local_player_unavailable");
                return;
            }

            CompanionPlayerLifecycle lifecycle = lookup.FindForPlayer(localPlayer);
            if (!Utilities.IsValid(lifecycle))
            {
                LogUnavailable("local_refresh", "local_lifecycle_not_found");
                return;
            }

            int beforeState = lifecycle.lifecycleState;
            string beforeAction = lifecycle.lastLifecycleAction;
            if (beforeState != CompanionPlayerLifecycle.StateReady
                && beforeState != CompanionPlayerLifecycle.StateDisabled)
            {
                LogLifecycle("local_refresh", "local_refresh_not_ready", localPlayer, lifecycle, beforeState, beforeAction);
                return;
            }

            lifecycle.RefreshLifecycleOwner();

            string expectedAction = beforeState == CompanionPlayerLifecycle.StateDisabled
                ? "manual_refresh_owner_valid_disabled"
                : "manual_refresh_owner_valid_ready";
            string result = "local_refresh_unexpected";
            if (lifecycle.lifecycleState == beforeState
                && lifecycle.lastLifecycleAction == expectedAction)
            {
                result = "local_refresh_preserved";
            }

            LogLifecycle("local_refresh", result, localPlayer, lifecycle, beforeState, beforeAction);
        }

        private void RunRemoteMutationProbe()
        {
            CompanionPlayerLookup lookup = FindLookup();
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(lookup) || !Utilities.IsValid(localPlayer))
            {
                LogUnavailable("remote_mutation_probe", "lookup_or_local_player_unavailable");
                return;
            }

            // Capture a read-only inventory before attempting the rejection probe. This independently
            // records every player's spawned PlayerObject lifecycle through the official
            // Networking.GetPlayerObjects path without calling RefreshLifecycleOwner or changing
            // lifecycle state, making the ownership/isolation row easier to audit from real logs.
            LogPlayerObjectInventory(localPlayer);

            VRCPlayerApi[] players = VRCPlayerApi.GetPlayers();
            for (int i = 0; i < players.Length; i++)
            {
                VRCPlayerApi player = players[i];
                if (!Utilities.IsValid(player) || player.playerId == localPlayer.playerId)
                {
                    continue;
                }

                CompanionPlayerLifecycle lifecycle = lookup.FindForPlayer(player);
                if (!Utilities.IsValid(lifecycle) || lifecycle.lifecycleState != CompanionPlayerLifecycle.StateReady)
                {
                    continue;
                }

                int beforeState = lifecycle.lifecycleState;
                string beforeAction = lifecycle.lastLifecycleAction;
                lifecycle.DisableForLocalOwner();

                string result = "remote_mutation_unexpected";
                if (lifecycle.lifecycleState == beforeState
                    && lifecycle.lastLifecycleAction == "disable_rejected_not_local_ready")
                {
                    result = "remote_mutation_rejected";
                }

                LogLifecycle("remote_mutation_probe", result, player, lifecycle, beforeState, beforeAction);
                return;
            }

            LogUnavailable("remote_mutation_probe", "remote_ready_lifecycle_not_found");
        }

        private void LogPlayerObjectInventory(VRCPlayerApi localPlayer)
        {
            VRCPlayerApi[] players = VRCPlayerApi.GetPlayers();
            int expectedPlayerCount = 0;
            int lifecycleCopyCount = 0;
            int inconsistentCount = 0;
            int localPlayerId = localPlayer.playerId;

            for (int i = 0; i < players.Length; i++)
            {
                VRCPlayerApi player = players[i];
                if (!Utilities.IsValid(player))
                {
                    continue;
                }

                expectedPlayerCount++;
                int targetPlayerId = player.playerId;
                bool foundLifecycle = false;
                GameObject[] playerObjects = Networking.GetPlayerObjects(player);
                for (int j = 0; j < playerObjects.Length; j++)
                {
                    GameObject playerObject = playerObjects[j];
                    if (!Utilities.IsValid(playerObject))
                    {
                        continue;
                    }

                    CompanionPlayerLifecycle lifecycle = playerObject.GetComponent<CompanionPlayerLifecycle>();
                    if (!Utilities.IsValid(lifecycle))
                    {
                        continue;
                    }

                    foundLifecycle = true;
                    lifecycleCopyCount++;
                    VRCPlayerApi actualOwner = Networking.GetOwner(playerObject);
                    int actualOwnerId = Utilities.IsValid(actualOwner) ? actualOwner.playerId : -1;
                    bool consistent = actualOwnerId == targetPlayerId
                        && lifecycle.associatedPlayerId == targetPlayerId
                        && lifecycle.lifecycleState != CompanionPlayerLifecycle.StateDetached;
                    string result = consistent ? "snapshot_copy_consistent" : "snapshot_copy_unexpected";
                    if (!consistent)
                    {
                        inconsistentCount++;
                    }

                    Debug.Log(
                        LogPrefix
                        + " action=snapshot_all"
                        + " result=" + result
                        + " control=" + gameObject.name
                        + " localPlayerId=" + localPlayerId
                        + " targetPlayerId=" + targetPlayerId
                        + " actualOwnerId=" + actualOwnerId
                        + " associatedPlayerId=" + lifecycle.associatedPlayerId
                        + " state=" + lifecycle.lifecycleState
                        + " restoreObserved=" + lifecycle.restoreObserved
                        + " localOwner=" + lifecycle.localOwner
                        + " restoreEvents=" + lifecycle.restoreEventCount
                        + " ignoredRestoreEvents=" + lifecycle.ignoredRestoreEventCount
                        + " ownerMismatches=" + lifecycle.ownerMismatchCount
                        + " detaches=" + lifecycle.detachCount
                        + " lastAction=" + lifecycle.lastLifecycleAction);
                }

                if (!foundLifecycle)
                {
                    inconsistentCount++;
                    Debug.Log(
                        LogPrefix
                        + " action=snapshot_all"
                        + " result=snapshot_copy_missing"
                        + " control=" + gameObject.name
                        + " localPlayerId=" + localPlayerId
                        + " targetPlayerId=" + targetPlayerId);
                }
            }

            string summaryResult = expectedPlayerCount > 0
                && lifecycleCopyCount == expectedPlayerCount
                && inconsistentCount == 0
                ? "snapshot_all_consistent"
                : "snapshot_all_incomplete";
            Debug.Log(
                LogPrefix
                + " action=snapshot_all_summary"
                + " result=" + summaryResult
                + " control=" + gameObject.name
                + " localPlayerId=" + localPlayerId
                + " expectedPlayerCount=" + expectedPlayerCount
                + " lifecycleCopyCount=" + lifecycleCopyCount
                + " inconsistentCount=" + inconsistentCount);
        }

        private CompanionPlayerLookup FindLookup()
        {
            GameObject runtime = GameObject.Find("CompanionRuntime");
            if (!Utilities.IsValid(runtime))
            {
                return null;
            }

            return runtime.GetComponent<CompanionPlayerLookup>();
        }

        private void LogLifecycle(
            string action,
            string result,
            VRCPlayerApi targetPlayer,
            CompanionPlayerLifecycle lifecycle,
            int beforeState,
            string beforeAction)
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            VRCPlayerApi actualOwner = Networking.GetOwner(lifecycle.gameObject);
            int localPlayerId = Utilities.IsValid(localPlayer) ? localPlayer.playerId : -1;
            int targetPlayerId = Utilities.IsValid(targetPlayer) ? targetPlayer.playerId : -1;
            int actualOwnerId = Utilities.IsValid(actualOwner) ? actualOwner.playerId : -1;

            lastControlResult = result;
            Debug.Log(
                LogPrefix
                + " action=" + action
                + " result=" + result
                + " control=" + gameObject.name
                + " localPlayerId=" + localPlayerId
                + " targetPlayerId=" + targetPlayerId
                + " actualOwnerId=" + actualOwnerId
                + " associatedPlayerId=" + lifecycle.associatedPlayerId
                + " beforeState=" + beforeState
                + " afterState=" + lifecycle.lifecycleState
                + " beforeAction=" + beforeAction
                + " afterAction=" + lifecycle.lastLifecycleAction
                + " restoreObserved=" + lifecycle.restoreObserved
                + " localOwner=" + lifecycle.localOwner);
        }

        private void LogUnavailable(string action, string reason)
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            int localPlayerId = Utilities.IsValid(localPlayer) ? localPlayer.playerId : -1;
            lastControlResult = reason;
            Debug.Log(
                LogPrefix
                + " action=" + action
                + " control=" + gameObject.name
                + " localPlayerId=" + localPlayerId
                + " result=" + reason);
        }
    }
}
