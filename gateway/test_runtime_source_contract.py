from pathlib import Path
import re
import unittest


REPO_ROOT = Path(__file__).resolve().parents[1]
RUNTIME_ROOT = (
    REPO_ROOT
    / "Packages"
    / "com.uynewnas.vrc-embodied-companion"
    / "Runtime"
)
PROBE_PATH = RUNTIME_ROOT / "CompanionTransportProbe.cs"
HANDOFF_ADAPTER_PATH = RUNTIME_ROOT / "CompanionBehaviorPlanHandoffAdapter.cs"


class RuntimeSourceContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = PROBE_PATH.read_text(encoding="utf-8")
        cls.handoff_source = HANDOFF_ADAPTER_PATH.read_text(encoding="utf-8")

    def test_handoff_event_names_are_fixed(self):
        self.assertIn(
            'private const string AcceptedPlanEventName = "OnCompanionBehaviorPlanAccepted";',
            self.source,
        )
        self.assertIn(
            'private const string UnavailablePlanEventName = "OnCompanionBehaviorPlanUnavailable";',
            self.source,
        )

    def test_transport_does_not_mutate_authorization_sink_fields(self):
        self.assertNotIn("SetProgramVariable", self.source)
        self.assertNotIn("SubmitModelProposal", self.source)
        self.assertNotIn("CompanionBehaviorController", self.source)

    def test_validation_precedes_accepted_handoff(self):
        validation = self.source.index("if (!behaviorPlanValidator.TryValidate(payload))")
        accepted = self.source.index("SignalPlanAccepted();", validation)
        self.assertGreater(accepted, validation)
        self.assertRegex(
            self.source,
            r"ClearInFlightState\(\);\s+SignalPlanAccepted\(\);",
        )

    def test_failure_modes_signal_unavailable_after_clearing_request(self):
        for reason in (
            "timeout",
            "validator_missing",
            "behavior_plan_invalid",
            "download_error",
        ):
            with self.subTest(reason=reason):
                self.assertRegex(
                    self.source,
                    r'ClearInFlightState\(\);\s+SignalPlanUnavailable\("'
                    + re.escape(reason)
                    + r'"\);',
                )

    def test_stale_callback_path_does_not_signal_fallback(self):
        stale_method_start = self.source.index("private void RecordStaleCallback")
        stale_method_end = self.source.index("private bool RejectStart", stale_method_start)
        stale_method = self.source[stale_method_start:stale_method_end]
        self.assertNotIn("SignalPlanUnavailable", stale_method)
        self.assertNotIn("SignalPlanAccepted", stale_method)

    def test_authorization_adapter_event_names_are_fixed(self):
        self.assertIn(
            'private const string ProposalReadyEventName = "OnCompanionModelProposalReady";',
            self.handoff_source,
        )
        self.assertIn(
            'private const string ProposalClearedEventName = "OnCompanionModelProposalCleared";',
            self.handoff_source,
        )
        self.assertIn("public void OnCompanionBehaviorPlanAccepted()", self.handoff_source)
        self.assertIn("public void OnCompanionBehaviorPlanUnavailable()", self.handoff_source)

    def test_authorization_adapter_is_pull_only_and_controller_agnostic(self):
        self.assertNotIn("SetProgramVariable", self.handoff_source)
        self.assertNotIn("SubmitModelProposal", self.handoff_source)
        self.assertNotIn("CompanionBehaviorController", self.handoff_source)

    def test_accepted_signal_erases_old_proposal_before_copying_new_fields(self):
        method_start = self.handoff_source.index(
            "public void OnCompanionBehaviorPlanAccepted()"
        )
        method_end = self.handoff_source.index(
            "public void OnCompanionBehaviorPlanUnavailable()", method_start
        )
        method = self.handoff_source[method_start:method_end]
        clear_pos = method.index("ClearProposal();")
        validator_guard_pos = method.index(
            "behaviorPlanValidator == null || !behaviorPlanValidator.lastValid"
        )
        copy_pos = method.index("proposalAction = behaviorPlanValidator.lastAction;")
        ready_pos = method.index("SendCustomEvent(ProposalReadyEventName)")
        self.assertLess(clear_pos, validator_guard_pos)
        self.assertLess(validator_guard_pos, copy_pos)
        self.assertLess(copy_pos, ready_pos)
        self.assertIn("if (!behaviorPlanValidator.hasBehavior)", method)

    def test_unavailable_signal_clears_snapshot_before_notifying_sink(self):
        method_start = self.handoff_source.index(
            "public void OnCompanionBehaviorPlanUnavailable()"
        )
        method_end = self.handoff_source.index("private void ClearProposal()", method_start)
        method = self.handoff_source[method_start:method_end]
        self.assertRegex(
            method,
            r"ClearProposal\(\);\s+NotifyProposalCleared\(\);",
        )

    def test_clear_proposal_zeroes_all_body_fields(self):
        method_start = self.handoff_source.index("private void ClearProposal()")
        method_end = self.handoff_source.index(
            "private void NotifyProposalCleared()", method_start
        )
        method = self.handoff_source[method_start:method_end]
        for required in (
            'proposalAvailable = false;',
            'proposalRequestId = "";',
            'proposalAction = "";',
            'proposalGaze = "none";',
            'proposalDurationSeconds = 0f;',
            'proposalTargetDistanceMeters = 0f;',
        ):
            with self.subTest(required=required):
                self.assertIn(required, method)


if __name__ == "__main__":
    unittest.main()
