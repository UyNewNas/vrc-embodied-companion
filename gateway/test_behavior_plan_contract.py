import copy
import unittest

from behavior_plan_contract import validate_behavior_plan
from reference_server import build_plan, parse_route


class BehaviorPlanContractTests(unittest.TestCase):
    def setUp(self):
        self.plan = build_plan(
            parse_route("/v1/plan/default/walk_with_me/familiar/neutral/en/0")
        )

    def assertRejected(self, plan, reason):
        ok, actual = validate_behavior_plan(plan)
        self.assertFalse(ok)
        self.assertEqual(actual, reason)

    def test_reference_plan_is_valid(self):
        self.assertEqual(validate_behavior_plan(self.plan), (True, ""))

    def test_root_must_be_object(self):
        self.assertRejected([], "root_not_object")

    def test_unknown_root_field_rejected(self):
        plan = copy.deepcopy(self.plan)
        plan["player_id"] = "must-not-pass"
        self.assertRejected(plan, "unknown_root_field")

    def test_schema_version_rejected(self):
        plan = copy.deepcopy(self.plan)
        plan["schema_version"] = "0.2"
        self.assertRejected(plan, "schema_version")

    def test_requires_speech_or_behavior(self):
        plan = {"schema_version": "0.1", "request_id": "r"}
        self.assertRejected(plan, "speech_or_behavior_required")

    def test_unknown_action_rejected(self):
        plan = copy.deepcopy(self.plan)
        plan["behavior"]["action"] = "arbitrary_world_control"
        self.assertRejected(plan, "behavior_action")

    def test_behavior_bounds_rejected(self):
        plan = copy.deepcopy(self.plan)
        plan["behavior"]["target_distance_m"] = 9.0
        self.assertRejected(plan, "behavior_target_distance_m")

    def test_bool_is_not_accepted_as_number(self):
        plan = copy.deepcopy(self.plan)
        plan["behavior"]["duration_s"] = True
        self.assertRejected(plan, "behavior_duration_s")

    def test_memory_proposal_contract(self):
        plan = copy.deepcopy(self.plan)
        plan["memory_proposals"] = [
            {
                "class": "preference",
                "key": "comfort_style",
                "value": "quiet",
                "reason": "player explicitly selected quiet mode",
            }
        ]
        self.assertEqual(validate_behavior_plan(plan), (True, ""))

    def test_memory_proposal_unknown_field_rejected(self):
        plan = copy.deepcopy(self.plan)
        plan["memory_proposals"] = [
            {
                "class": "preference",
                "key": "comfort_style",
                "value": "quiet",
                "reason": "explicit preference",
                "secret": "no",
            }
        ]
        self.assertRejected(plan, "memory_proposal_fields")

    def test_memory_key_grammar_rejected(self):
        plan = copy.deepcopy(self.plan)
        plan["memory_proposals"] = [
            {
                "class": "preference",
                "key": "Bad-Key",
                "value": True,
                "reason": "invalid key grammar",
            }
        ]
        self.assertRejected(plan, "memory_proposal_key")


if __name__ == "__main__":
    unittest.main()
