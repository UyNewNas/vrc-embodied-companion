from __future__ import annotations

import argparse
import json
from pathlib import Path

from generate_static_pack import (
    ROUTE_COUNT,
    ROUTE_INDEX_VERSION,
    canonical_route_key,
    live_relative_path,
    route_from_index,
    static_relative_path,
)

UNITY_ROUTE_TABLE_SCHEMA_VERSION = "0.1"


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def build_unity_route_table(manifest: dict) -> dict:
    """Build a flat, JsonUtility-friendly route table from a reviewed manifest.

    The returned parallel arrays are ordered so array index == route_index.
    This is editor input only: hosts are intentionally omitted so the Unity
    importer can combine reviewed relative paths with separately configured
    editor-time HTTPS base URLs before constructing serialized VRCUrl values.
    """
    _require(isinstance(manifest, dict), "manifest must be an object")
    _require(manifest.get("schema_version") == "0.1", "unsupported manifest schema_version")
    _require(
        manifest.get("route_index_version") == ROUTE_INDEX_VERSION,
        "unsupported route_index_version",
    )

    persona_id = manifest.get("persona_id")
    _require(isinstance(persona_id, str), "persona_id must be a string")
    _require(manifest.get("route_count") == ROUTE_COUNT, "unexpected route_count")

    contract = manifest.get("route_index_contract")
    _require(isinstance(contract, dict), "route_index_contract must be an object")
    _require(
        contract.get("layout") == "mixed_radix_row_major",
        "unsupported route-index layout",
    )
    _require(
        contract.get("dimension_order")
        == ["intent", "relation_band", "comfort_style", "language", "turn_slot"],
        "route-index dimension order mismatch",
    )
    _require(
        contract.get("strides")
        == {
            "intent": 48,
            "relation_band": 16,
            "comfort_style": 8,
            "language": 4,
            "turn_slot": 1,
        },
        "route-index stride mismatch",
    )

    routes = manifest.get("routes")
    _require(isinstance(routes, list), "routes must be an array")
    _require(len(routes) == ROUTE_COUNT, "manifest route array must contain 288 entries")

    route_keys = []
    live_paths = []
    static_paths = []

    for expected_index, entry in enumerate(routes):
        _require(isinstance(entry, dict), "route entry must be an object")
        _require(entry.get("route_index") == expected_index, "route indices must be dense and ordered")

        expected_route = route_from_index(expected_index, persona_id)
        expected_key = canonical_route_key(expected_route)
        expected_live = live_relative_path(expected_route)
        expected_static = static_relative_path(expected_route).as_posix()

        _require(entry.get("route_key") == expected_key, "route_key does not match route_index")
        _require(
            entry.get("live_relative_path") == expected_live,
            "live_relative_path does not match route_index",
        )
        _require(
            entry.get("static_relative_path") == expected_static,
            "static_relative_path does not match route_index",
        )

        for field_name, expected_value in (
            ("intent", expected_route.intent),
            ("relation_band", expected_route.relation_band),
            ("comfort_style", expected_route.comfort_style),
            ("language", expected_route.language),
            ("turn_slot", expected_route.turn_slot),
        ):
            _require(entry.get(field_name) == expected_value, f"{field_name} does not match route_index")

        _require("?" not in expected_live and "#" not in expected_live, "live path widened transport surface")
        _require("?" not in expected_static and "#" not in expected_static, "static path widened transport surface")
        _require(not expected_live.startswith("/"), "live path must stay relative")
        _require(not expected_static.startswith("/"), "static path must stay relative")

        route_keys.append(expected_key)
        live_paths.append(expected_live)
        static_paths.append(expected_static)

    return {
        "schema_version": UNITY_ROUTE_TABLE_SCHEMA_VERSION,
        "route_index_version": ROUTE_INDEX_VERSION,
        "persona_id": persona_id,
        "route_count": ROUTE_COUNT,
        "route_keys": route_keys,
        "live_relative_paths": live_paths,
        "static_relative_paths": static_paths,
    }


def write_unity_route_table(manifest_path: Path, output_path: Path) -> dict:
    manifest_path = Path(manifest_path)
    output_path = Path(output_path)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    table = build_unity_route_table(manifest)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        json.dumps(table, ensure_ascii=False, separators=(",", ":")) + "\n",
        encoding="utf-8",
    )
    return table


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Flatten route-manifest.json into Unity editor route-table input."
    )
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    table = write_unity_route_table(args.manifest, args.output)
    print(f"wrote {table['route_count']} indexed routes to {args.output}")


if __name__ == "__main__":
    main()
