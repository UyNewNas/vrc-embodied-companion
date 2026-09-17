using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Small lookup adapter around VRChat's PlayerObject APIs for issue #4.
    ///
    /// The adapter intentionally performs a fresh bounded lookup instead of caching a global
    /// PlayerObject reference. Player references become invalid after leave, and using another
    /// player's PlayerObject as fallback would violate the per-player isolation contract.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CompanionPlayerLookup : UdonSharpBehaviour
    {
        [Header("Debug counters")]
        public int lookupAttempts;
        public int lookupHits;
        public int lookupMisses;
        public string lastLookupStatus = "idle";

        public CompanionPlayerLifecycle FindForPlayer(VRCPlayerApi player)
        {
            lookupAttempts++;

            if (!Utilities.IsValid(player))
            {
                lookupMisses++;
                lastLookupStatus = "invalid_player";
                return null;
            }

            GameObject[] playerObjects = Networking.GetPlayerObjects(player);
            if (playerObjects == null || playerObjects.Length == 0)
            {
                lookupMisses++;
                lastLookupStatus = "no_player_objects";
                return null;
            }

            for (int i = 0; i < playerObjects.Length; i++)
            {
                GameObject playerObject = playerObjects[i];
                if (!Utilities.IsValid(playerObject))
                {
                    continue;
                }

                CompanionPlayerLifecycle lifecycle = playerObject.GetComponentInChildren<CompanionPlayerLifecycle>();
                if (!Utilities.IsValid(lifecycle))
                {
                    continue;
                }

                lifecycle.RefreshLifecycleOwner();
                if (!lifecycle.MatchesPlayer(player))
                {
                    continue;
                }

                lookupHits++;
                lastLookupStatus = lifecycle.IsReady() ? "found_ready" : "found_not_ready";
                return lifecycle;
            }

            lookupMisses++;
            lastLookupStatus = "lifecycle_component_not_found";
            return null;
        }

        public CompanionPlayerLifecycle FindForLocalPlayer()
        {
            return FindForPlayer(Networking.LocalPlayer);
        }
    }
}
