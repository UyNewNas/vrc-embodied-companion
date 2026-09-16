import copy
import json
import tempfile
import unittest
from pathlib import Path

from generate_static_pack import (
    ROUTE_COUNT,
    build_static_plan,
    generate_pack,
    iter_routes,
    live_relative_path,
    route_from_index,
    route_index,
    stable_static_request_id,
    static_relative_path,
)
from generate_unity_route_table import (
    build_unity_route_table,
    write_unity_route_table,
)


class StaticPackTests(unittest.TestCase):
    def test_default_matrix_has_288_routes(self):
        self.assertEqual(288, ROUTE_COUNT)
        self.assertEqual(ROUTE_COUNT, len(list(iter_routes("default"))))

    def test_invalid_persona_is_rejected(self):
        with self.assertRaises(ValueError):
            list(iter_routes("../bad"))

    def test_paths_are_bounded_and_live_static_paths_are_distinct(self):
        route = next(iter(iter_routes("default")))
        live_path = live_relative_path(route)
        static_path = static_relative_path(route).as_posix()
        self.assertTrue(live_path.startswith("v1/plan/default/"))
        self.assertFalse(live_path.endswith(".json"))
        self.assertEqual(f"{live_path}.json", static_path)
        self.assertNotIn("..", static_path)
        self.assertNotIn("?", static_path)

    def test_route_indices_form_dense_bijection(self):
        routes = list(iter_routes("default"))
        indices = [route_index(route) for route in routes]
        self.assertEqual(list(range(ROUTE_COUNT)), indices)
        self.assertEqual(routes, [route_from_index(i, "default") for i in indices])

    def test_route_index_anchor_is_stable(self):
        matching = [
            route
            for route in iter_routes("default")
            if route.intent == "quiet_company"
            and route.relation_band == "warm"
            and route.comfort_style == "quiet"
            and route.language == "zh"
            and route.turn_slot == 2
        ]
        self.assertEqual(1, len(matching))
        self.assertEqual(190, route_index(matching[0]))

    def test_invalid_route_indices_are_rejected(self):
        for invalid in (-1, ROUTE_COUNT, True, 1.5):
            with self.assertRaises(ValueError):
                route_from_index(invalid)

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

    def test_manifest_exposes_editor_route_index_contract(self):
        with tempfile.TemporaryDirectory() as tmp:
            manifest = generate_pack(Path(tmp), "default")
            contract = manifest["route_index_contract"]
            self.assertEqual("0.1", manifest["route_index_version"])
            self.assertEqual("mixed_radix_row_major", contract["layout"])
            self.assertEqual(
                ["intent", "relation_band", "comfort_style", "language", "turn_slot"],
                contract["dimension_order"],
            )
            self.assertEqual(
                {
                    "intent": 48,
                    "relation_band": 16,
                    "comfort_style": 8,
                    "language": 4,
                    "turn_slot": 1,
                },
                contract["strides"],
            )

            first = manifest["routes"][0]
            self.assertEqual(0, first["route_index"])
            self.assertTrue(first["live_relative_path"].startswith("v1/plan/default/"))
            self.assertEqual(
                f"{first['live_relative_path']}.json",
                first["static_relative_path"],
            )

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


class UnityRouteTableTests(unittest.TestCase):
    def _manifest(self):
        with tempfile.TemporaryDirectory() as tmp:
            return generate_pack(Path(tmp), "default")

    def test_unity_table_is_flat_dense_and_index_aligned(self):
        manifest = self._manifest()
        table = build_unity_route_table(manifest)

        self.assertEqual("0.1", table["schema_version"])
        self.assertEqual("0.1", table["route_index_version"])
        self.assertEqual(ROUTE_COUNT, table["route_count"])
        self.assertEqual(ROUTE_COUNT, len(table["route_keys"]))
        self.assertEqual(ROUTE_COUNT, len(table["live_relative_paths"]))
        self.assertEqual(ROUTE_COUNT, len(table["static_relative_paths"]))

        for index in (0, 1, 190, ROUTE_COUNT - 1):
            entry = manifest["routes"][index]
            self.assertEqual(entry["route_key"], table["route_keys"][index])
            self.assertEqual(
                entry["live_relative_path"], table["live_relative_paths"][index]
            )
            self.assertEqual(
                entry["static_relative_path"], table["static_relative_paths"][index]
            )

    def test_unity_table_rejects_index_gap_or_reordering(self):
        manifest = self._manifest()
        broken = copy.deepcopy(manifest)
        broken["routes"][10], broken["routes"][11] = broken["routes"][11], broken["routes"][10]
        with self.assertRaises(ValueError):
            build_unity_route_table(broken)

    def test_unity_table_rejects_path_widening(self):
        manifest = self._manifest()
        broken = copy.deepcopy(manifest)
        broken["routes"][0]["live_relative_path"] += "?player=secret"
        with self.assertRaises(ValueError):
            build_unity_route_table(broken)

    def test_unity_route_table_file_is_reproducible(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            generate_pack(root, "default")
            first = root / "unity-route-table-a.json"
            second = root / "unity-route-table-b.json"
            write_unity_route_table(root / "route-manifest.json", first)
            write_unity_route_table(root / "route-manifest.json", second)
            self.assertEqual(first.read_bytes(), second.read_bytes())

    def test_unity_route_table_schema_is_strict_and_parseable(self):
        schema_path = (
            Path(__file__).resolve().parent.parent
            / "schemas"
            / "unity-route-table.v0.1.schema.json"
        )
        schema = json.loads(schema_path.read_text(encoding="utf-8"))
        self.assertFalse(schema["additionalProperties"])
        self.assertEqual(288, schema["properties"]["route_count"]["const"])
        for field in ("route_keys", "live_relative_paths", "static_relative_paths"):
            self.assertEqual(288, schema["properties"][field]["minItems"])
            self.assertEqual(288, schema["properties"][field]["maxItems"])


if __name__ == "__main__":
    unittest.main()
