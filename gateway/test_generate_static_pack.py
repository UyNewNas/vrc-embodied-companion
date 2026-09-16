import json
import tempfile
import unittest
from pathlib import Path

from generate_static_pack import (
    build_static_plan,
    generate_pack,
    iter_routes,
    stable_static_request_id,
    static_relative_path,
)


class StaticPackTests(unittest.TestCase):
    def test_default_matrix_has_288_routes(self):
        self.assertEqual(288, len(list(iter_routes("default"))))

    def test_invalid_persona_is_rejected(self):
        with self.assertRaises(ValueError):
            list(iter_routes("../bad"))

    def test_paths_are_bounded_json_paths(self):
        route = next(iter(iter_routes("default")))
        path = static_relative_path(route).as_posix()
        self.assertTrue(path.startswith("v1/plan/default/"))
        self.assertTrue(path.endswith(".json"))
        self.assertNotIn("..", path)
        self.assertNotIn("?", path)

    def test_static_request_id_is_deterministic_and_bounded(self):
        route = next(iter(iter_routes("default")))
        first = stable_static_request_id(route)
        second = stable_static_request_id(route)
        self.assertEqual(first, second)
        self.assertTrue(first.startswith("static:"))
        self.assertLessEqual(len(first), 128)

    def test_generation_writes_manifest_and_all_routes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest = generate_pack(root, "default")
            self.assertEqual(288, manifest["route_count"])
            self.assertEqual(288, len(list((root / "v1" / "plan").rglob("*.json"))))

            loaded = json.loads(
                (root / "route-manifest.json").read_text(encoding="utf-8")
            )
            self.assertEqual(288, len(loaded["routes"]))

    def test_generated_plan_has_required_contract_shape(self):
        route = next(iter(iter_routes("default")))
        plan = build_static_plan(route)
        self.assertEqual("0.1", plan["schema_version"])
        self.assertTrue(plan["request_id"].startswith("static:"))
        self.assertIn("speech", plan)
        self.assertIn("behavior", plan)

    def test_generation_is_reproducible(self):
        with tempfile.TemporaryDirectory() as left, tempfile.TemporaryDirectory() as right:
            left_root = Path(left)
            right_root = Path(right)
            generate_pack(left_root, "default")
            generate_pack(right_root, "default")

            self.assertEqual(
                (left_root / "route-manifest.json").read_bytes(),
                (right_root / "route-manifest.json").read_bytes(),
            )

            sample = next(iter(iter_routes("default")))
            self.assertEqual(
                (left_root / static_relative_path(sample)).read_bytes(),
                (right_root / static_relative_path(sample)).read_bytes(),
            )


if __name__ == "__main__":
    unittest.main()
