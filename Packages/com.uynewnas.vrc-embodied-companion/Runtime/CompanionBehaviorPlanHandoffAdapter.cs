using UdonSharp;
using UnityEngine;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Narrow bridge between a transport-validated BehaviorPlan and the future #6 behavior
    /// authorization integration.
    ///
    /// The transport probe may target this component with the fixed local events
    /// OnCompanionBehaviorPlanAccepted / OnCompanionBehaviorPlanUnavailable. This adapter copies
    /// only the already validated body proposal into a bounded pull surface and emits a second
    /// fixed event to an authorization target. It never executes a world action itself.
    ///
    /// A plan without a behavior object is valid but produces no body proposal. Any unavailable
    /// or inconsistent signal clears the prior snapshot before notifying the authorization layer,
    /// so stale model fields cannot be reused as fallback behavior.
    ///
    /// Unity/UdonSharp compilation and event-delivery behavior remain unverified until issue #2
    /// provides a runnable VCC World project.
    /// </summary>
    public class CompanionBehaviorPlanHandoffAdapter : UdonSharpBehaviour
    {
        private const string ProposalReadyEventName = "OnCompanionModelProposalReady";
        private const string ProposalClearedEventName = "OnCompanionModelProposalCleared";

        [Header("Validated source")]
        public CompanionBehaviorPlanValidator behaviorPlanValidator;

        [Header("Authorization sink")]
        [Tooltip("Optional UdonSharp target. On ready it must pull the bounded proposal fields below and route them through #6 authorization before any world action.")]
        public UdonSharpBehaviour behaviorAuthorizationTarget;

        [Header("Ephemeral model proposal")]
        public bool proposalAvailable;
        public int proposalEpoch;
        public string proposalRequestId = "";
        public string proposalAction = "";
        public string proposalGaze = "none";
        public float proposalDurationSeconds;
        public float proposalTargetDistanceMeters;

        [Header("Debug counters - runtime evidence only")]
        public int acceptedSignalCount;
        public int unavailableSignalCount;
        public int bodyProposalCount;
        public int acceptedWithoutBehaviorCount;
        public int inconsistentAcceptedSignalCount;
        public int clearedProposalCount;

        /// <summary>
        /// Fixed event entry point used by CompanionTransportProbe after URL matching and full
        /// BehaviorPlan validation have both succeeded.
        /// </summary>
        public void OnCompanionBehaviorPlanAccepted()
        {
            acceptedSignalCount++;

            // Always erase the previous proposal before observing the new validator snapshot.
            ClearProposal();

            if (behaviorPlanValidator == null || !behaviorPlanValidator.lastValid)
            {
                inconsistentAcceptedSignalCount++;
                NotifyProposalCleared();
                return;
            }

            // Speech-only plans are valid. They deliberately do not mutate body behavior.
            if (!behaviorPlanValidator.hasBehavior)
            {
                acceptedWithoutBehaviorCount++;
                NotifyProposalCleared();
                return;
            }

            if (string.IsNullOrEmpty(behaviorPlanValidator.lastAction))
            {
                inconsistentAcceptedSignalCount++;
                NotifyProposalCleared();
                return;
            }

            proposalRequestId = behaviorPlanValidator.lastRequestId;
            proposalAction = behaviorPlanValidator.lastAction;
            proposalGaze = string.IsNullOrEmpty(behaviorPlanValidator.lastGaze)
                ? "none"
                : behaviorPlanValidator.lastGaze;
            proposalDurationSeconds = behaviorPlanValidator.hasDurationSeconds
                ? behaviorPlanValidator.lastDurationSeconds
                : 0f;
            proposalTargetDistanceMeters = behaviorPlanValidator.hasTargetDistanceMeters
                ? behaviorPlanValidator.lastTargetDistanceMeters
                : 0f;
            proposalAvailable = true;
            proposalEpoch++;
            bodyProposalCount++;

            if (behaviorAuthorizationTarget != null)
            {
                behaviorAuthorizationTarget.SendCustomEvent(ProposalReadyEventName);
            }
        }

        /// <summary>
        /// Fixed event entry point for timeout/download/parser failures. Deterministic #6 state
        /// remains authoritative; no prior model proposal survives this transition.
        /// </summary>
        public void OnCompanionBehaviorPlanUnavailable()
        {
            unavailableSignalCount++;
            ClearProposal();
            NotifyProposalCleared();
        }

        private void ClearProposal()
        {
            bool hadProposalData = proposalAvailable
                || !string.IsNullOrEmpty(proposalRequestId)
                || !string.IsNullOrEmpty(proposalAction)
                || proposalDurationSeconds != 0f
                || proposalTargetDistanceMeters != 0f
                || proposalGaze != "none";

            proposalAvailable = false;
            proposalRequestId = "";
            proposalAction = "";
            proposalGaze = "none";
            proposalDurationSeconds = 0f;
            proposalTargetDistanceMeters = 0f;

            if (hadProposalData)
            {
                clearedProposalCount++;
            }
        }

        private void NotifyProposalCleared()
        {
            if (behaviorAuthorizationTarget != null)
            {
                behaviorAuthorizationTarget.SendCustomEvent(ProposalClearedEventName);
            }
        }
    }
}
