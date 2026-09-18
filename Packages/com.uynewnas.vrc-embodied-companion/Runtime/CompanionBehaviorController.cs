using UdonSharp;
using UnityEngine;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Uncompiled v0.1 prototype for issue #6.
    ///
    /// This class implements semantic arbitration only. Animator and navigation adapters are
    /// deliberately not embedded here so a failed presentation/path adapter cannot bypass the
    /// permission and priority rules.
    ///
    /// Runtime verification remains blocked on issue #2.
    /// </summary>
    public class CompanionBehaviorController : UdonSharpBehaviour
    {
        [Header("Lifecycle")]
        public string mode = "restoring";

        [Header("Permission / relationship gates")]
        public bool approachAllowed = true;
        public bool touchResponseAllowed = true;
        public bool offerHugAllowed = true;
        public string followPreference = "ask"; // ask | allowed | avoid

        [Header("World adapter state")]
        public string distanceBand = "far";
        public bool navigationAvailable;
        public bool seatTargetAvailable;

        [Header("Current semantic behavior")]
        public string currentAction = "idle";
        public string currentPriority = "idle";
        public string currentGaze = "none";
        public float currentTargetDistanceM;
        public float currentStartedAt;
        public float currentMinHoldUntil;
        public float currentMaxEndAt;

        [Header("Debug")]
        public string lastEventType = "";
        public string lastTransitionReason = "bootstrap";
        public string lastResult = "accepted";
        public int commandSequence;
        public int transitionSequence;
        public int rejectedSequence;

        private void Update()
        {
            if (currentMaxEndAt <= 0f || Time.time < currentMaxEndAt)
            {
                return;
            }

            // A maximum lifetime is a failsafe, not a lower-priority proposal.
            ForceTransition("idle", "idle", "none", 0f, 0f, "max_lifetime_elapsed", "fallback_applied");
        }

        public void SetModeRestoring()
        {
            mode = "restoring";
            ForceTransition("idle", "idle", "none", 0f, 0f, "lifecycle_restoring", "accepted");
        }

        public void SetModeAvailable()
        {
            mode = "available";
        }

        public void DisableCompanion()
        {
            mode = "disabled";
            // Safety priority can remain latched while disabled because mode rejects every normal
            // proposal. EnableCompanion explicitly releases the controller back to idle priority.
            ForceTransition("idle", "safety", "none", 0f, 0f, "companion_disabled", "accepted");
        }

        public void EnableCompanion()
        {
            mode = "available";
            ForceTransition("idle", "idle", "none", 0f, 0f, "companion_enabled", "accepted");
        }

        public void SetDistanceBand(string value)
        {
            distanceBand = value;
        }

        public void SetNavigationState(bool available, bool seatAvailable)
        {
            navigationAvailable = available;
            seatTargetAvailable = seatAvailable;

            if (!navigationAvailable && IsLocomotionAction(currentAction))
            {
                // Navigation failure is an interrupt, but the fallback state must not remain at
                // safety priority forever or all later player intents would be starved.
                ForceTransition("stay", "idle", "none", 0f, 0f, "navigation_invalidated", "fallback_applied");
            }
        }

        public void NotifyNavigationArrived()
        {
            if (currentAction == "approach" || currentAction == "keep_distance")
            {
                string settledPriority = currentPriority == "explicit_intent" ? "explicit_intent" : "idle";
                ForceTransition("stay", settledPriority, "none", 0f, 0f, "navigation_arrived", "accepted");
            }
        }

        public void NotifyNavigationFailed()
        {
            if (IsLocomotionAction(currentAction))
            {
                ForceTransition("stay", "idle", "none", 0f, 0f, "navigation_failed", "fallback_applied");
            }
        }

        public void SetTouchAllowed(bool allowed)
        {
            touchResponseAllowed = allowed;
            if (!allowed && (currentAction == "react_headpat" || currentAction == "offer_hug"))
            {
                ForceTransition("idle", "idle", "none", 0f, 0f, "touch_permission_revoked", "fallback_applied");
            }
        }

        public void SetApproachAllowed(bool allowed)
        {
            approachAllowed = allowed;
            if (!allowed && (currentAction == "approach" || currentAction == "sit_near"))
            {
                ForceTransition("stay", "idle", "none", 0f, 0f, "approach_permission_revoked", "fallback_applied");
            }
        }

        public void SetOfferHugAllowed(bool allowed)
        {
            offerHugAllowed = allowed;
            if (!allowed && currentAction == "offer_hug")
            {
                ForceTransition("idle", "idle", "none", 0f, 0f, "hug_permission_revoked", "fallback_applied");
            }
        }

        public void SetFollowPreference(string preference)
        {
            followPreference = preference;
            if (preference == "avoid" && currentAction == "follow")
            {
                ForceTransition("stay", "idle", "none", 0f, 0f, "follow_preference_avoid", "fallback_applied");
            }
        }

        /// <summary>
        /// Deterministic fallback event entry point. Integration code should pass only normalized
        /// InteractionEvent.type values from the v0.1 contract.
        /// </summary>
        public void HandleEvent(string eventType)
        {
            lastEventType = eventType;

            if (eventType == "companion_disabled")
            {
                DisableCompanion();
                return;
            }

            if (eventType == "companion_enabled")
            {
                EnableCompanion();
                return;
            }

            if (eventType == "persistence_restored")
            {
                SetModeAvailable();
                return;
            }

            if (eventType == "intent_stop_touch")
            {
                HandleStopTouch();
                return;
            }

            if (eventType == "intent_stay")
            {
                TryTransition("stay", "explicit_intent", "none", 0f, 0f, eventType);
                return;
            }

            if (eventType == "intent_follow")
            {
                TryTransition("follow", "explicit_intent", "soft_track", 0f, 0f, eventType);
                return;
            }

            if (eventType == "intent_sit_with_me")
            {
                TryTransition("sit_near", "explicit_intent", "soft_track", 0f, 0.9f, eventType);
                return;
            }

            if (eventType == "intent_quiet_company")
            {
                // Quiet company deliberately changes body behavior without requiring speech.
                TryTransition("sit_near", "explicit_intent", "brief", 0f, 0.9f, eventType);
                return;
            }

            if (eventType == "intent_talk")
            {
                TryTransition("look_at_player", "explicit_intent", "soft_track", 2f, 0f, eventType);
                return;
            }

            if (eventType == "intent_offer_hug_ok")
            {
                TryTransition("offer_hug", "explicit_intent", "soft_track", 12f, 0f, eventType);
                return;
            }

            if (eventType == "headpat_started")
            {
                TryTransition("react_headpat", "interaction", "soft_track", 8f, 0f, eventType);
                return;
            }

            if (eventType == "headpat_ended")
            {
                if (currentAction == "react_headpat")
                {
                    ForceTransition("idle", "idle", "none", 0f, 0f, eventType, "accepted");
                }
                return;
            }

            if (eventType == "entered_near")
            {
                TryTransition("look_at_player", "normal", "brief", 2f, 0f, eventType);
                return;
            }

            if (eventType == "facing_toward_started")
            {
                TryTransition("look_at_player", "normal", "brief", 2f, 0f, eventType);
                return;
            }

            if (eventType == "departure_started")
            {
                if (currentAction == "follow")
                {
                    TryTransition("follow", "explicit_intent", "soft_track", 0f, 0f, eventType);
                }
                return;
            }

            if (eventType == "inactivity_short")
            {
                if (distanceBand == "near" || distanceBand == "contact")
                {
                    TryTransition("idle", "idle", "none", 0f, 0f, eventType);
                }
                return;
            }

            if (eventType == "inactivity_long")
            {
                if (distanceBand == "near" || distanceBand == "contact")
                {
                    TryTransition("sleep_idle", "idle", "none", 0f, 0f, eventType);
                }
                return;
            }

            Reject("rejected_state", "unregistered_event:" + eventType);
        }

        /// <summary>
        /// Model proposals are intentionally fixed to normal priority. The model cannot claim
        /// safety/explicit-intent authority by supplying a priority string.
        /// </summary>
        public void SubmitModelProposal(string action, string gaze, float durationS, float targetDistanceM)
        {
            TryTransition(action, "normal", gaze, durationS, targetDistanceM, "model_proposal");
        }

        private void HandleStopTouch()
        {
            touchResponseAllowed = false;

            if (distanceBand == "contact" && navigationAvailable)
            {
                // The interrupt itself is safety-critical, but the resulting movement is an
                // explicit player intent. Keeping it at explicit_intent allows later explicit
                // commands after the short movement hold instead of permanently latching safety.
                ForceTransition("keep_distance", "explicit_intent", "none", 0f, 1.0f, "intent_stop_touch", "accepted");
                return;
            }

            ForceTransition("idle", "idle", "none", 0f, 0f, "intent_stop_touch", "accepted");
        }

        private bool TryTransition(string action, string priority, string gaze, float durationS, float targetDistanceM, string reason)
        {
            commandSequence++;

            if (!IsKnownAction(action))
            {
                Reject("rejected_unknown_action", reason + ":" + action);
                return false;
            }

            if (mode == "disabled")
            {
                Reject("rejected_state", reason + ":disabled");
                return false;
            }

            if (!PermissionAllows(action))
            {
                Reject("rejected_permission", reason + ":" + action);
                return false;
            }

            if (RequiresNavigation(action) && !navigationAvailable)
            {
                Reject("rejected_navigation", reason + ":" + action);
                return false;
            }

            if (action == "sit_near" && !seatTargetAvailable)
            {
                Reject("rejected_navigation", reason + ":seat_unavailable");
                return false;
            }

            int requestedRank = PriorityRank(priority);
            int currentRank = PriorityRank(currentPriority);

            if (requestedRank < currentRank)
            {
                Reject("rejected_state", reason + ":lower_priority");
                return false;
            }

            if (requestedRank == currentRank && action != currentAction && Time.time < currentMinHoldUntil)
            {
                Reject("rejected_state", reason + ":minimum_hold");
                return false;
            }

            if (action == currentAction && requestedRank <= currentRank && Time.time < currentMinHoldUntil)
            {
                lastResult = "coalesced";
                lastTransitionReason = reason + ":duplicate";
                return true;
            }

            float clampedTarget = targetDistanceM;
            bool clamped = false;
            if (clampedTarget > 0f)
            {
                if (clampedTarget < 0.25f)
                {
                    clampedTarget = 0.25f;
                    clamped = true;
                }
                else if (clampedTarget > 4.0f)
                {
                    clampedTarget = 4.0f;
                    clamped = true;
                }
            }

            float maxDuration = ActionMaxDuration(action);
            float effectiveDuration = durationS;
            if (maxDuration > 0f && (effectiveDuration <= 0f || effectiveDuration > maxDuration))
            {
                effectiveDuration = maxDuration;
                clamped = true;
            }

            ForceTransition(action, priority, NormalizeGaze(gaze), effectiveDuration, clampedTarget, reason, clamped ? "clamped" : "accepted");
            return true;
        }

        private void ForceTransition(string action, string priority, string gaze, float durationS, float targetDistanceM, string reason, string result)
        {
            currentAction = action;
            currentPriority = priority;
            currentGaze = gaze;
            currentTargetDistanceM = targetDistanceM;
            currentStartedAt = Time.time;
            currentMinHoldUntil = Time.time + ActionMinHold(action);
            currentMaxEndAt = durationS > 0f ? Time.time + durationS : 0f;
            lastTransitionReason = reason;
            lastResult = result;
            transitionSequence++;
        }

        private void Reject(string result, string reason)
        {
            lastResult = result;
            lastTransitionReason = reason;
            rejectedSequence++;
        }

        private bool PermissionAllows(string action)
        {
            if ((action == "approach" || action == "sit_near") && !approachAllowed)
            {
                return false;
            }

            if ((action == "react_headpat" || action == "offer_hug") && !touchResponseAllowed)
            {
                return false;
            }

            if (action == "offer_hug" && !offerHugAllowed)
            {
                return false;
            }

            if (action == "follow" && followPreference == "avoid")
            {
                return false;
            }

            return true;
        }

        private bool RequiresNavigation(string action)
        {
            return action == "approach"
                || action == "keep_distance"
                || action == "sit_near"
                || action == "follow";
        }

        private bool IsLocomotionAction(string action)
        {
            return RequiresNavigation(action);
        }

        private bool IsKnownAction(string action)
        {
            return action == "idle"
                || action == "look_at_player"
                || action == "look_away"
                || action == "approach"
                || action == "keep_distance"
                || action == "sit_near"
                || action == "follow"
                || action == "stay"
                || action == "react_headpat"
                || action == "offer_hug"
                || action == "wave"
                || action == "sleep_idle";
        }

        private int PriorityRank(string priority)
        {
            if (priority == "safety") return 500;
            if (priority == "explicit_intent") return 400;
            if (priority == "interaction") return 300;
            if (priority == "normal") return 200;
            return 100;
        }

        private float ActionMinHold(string action)
        {
            if (action == "look_at_player" || action == "look_away") return 0.4f;
            if (action == "react_headpat") return 0f;
            if (action == "offer_hug") return 0.5f;
            if (action == "wave") return 0.5f;
            if (action == "sleep_idle") return 2.0f;
            if (IsLocomotionAction(action)) return 0.25f;
            return 0f;
        }

        private float ActionMaxDuration(string action)
        {
            if (action == "look_at_player" || action == "look_away") return 2.0f;
            if (action == "react_headpat") return 8.0f;
            if (action == "offer_hug") return 12.0f;
            if (action == "wave") return 3.0f;
            return 0f;
        }

        private string NormalizeGaze(string gaze)
        {
            if (gaze == "brief"
                || gaze == "soft_track"
                || gaze == "track"
                || gaze == "look_away")
            {
                return gaze;
            }

            return "none";
        }
    }
}
