from pathlib import Path
import re
import unittest


REPO_ROOT = Path(__file__).resolve().parents[1]
PROBE_PATH = (
    REPO_ROOT
    / "Packages"
    / "com.uynewnas.vrc-embodied-companion"
    / "Runtime"
    / "CompanionTransportProbe.cs"
)


class RuntimeSourceContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = PROBE_PATH.read_text(encoding="utf-8")

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


if __name__ == "__main__":
    unittest.main()
