from __future__ import annotations

import argparse
import hashlib
import json
from itertools import product
from pathlib import Path

from reference_server import (
    COMFORT_STYLES,
    INTENTS,
    LANGUAGES,
    PERSONA_RE,
    RELATION_BANDS,
    SCHEMA_VERSION,
    Route,
    build_plan,
)

ROUTE_INDEX_VERSION = "0.1"

# These orders are a compatibility contract for the future Unity/Udon route table.
# They intentionally preserve the ordering emitted by the first v0.1 generator.
INTENT_ORDER = (
    "goodbye",
    "greet",
    "offer_hug",
    "quiet_company",
    "talk_light",
    "walk_with_me",
)
RELATION_BAND_ORDER = ("familiar", "new", "warm")
COMFORT_STYLE_ORDER = ("neutral", "quiet")
LANGUAGE_ORDER = ("en", "zh")
TURN_SLOTS = (0, 1, 2, 3)

ROUTE_COUNT = (
    len(INTENT_ORDER)
    * len(RELATION_BAND_ORDER)
    * len(COMFORT_STYLE_ORDER)
    * len(LANGUAGE_ORDER)
    * len(TURN_SLOTS)
)

TURN_SLOT_STRIDE = 1
LANGUAGE_STRIDE = len(TURN_SLOTS)
COMFORT_STYLE_STRIDE = len(LANGUAGE_ORDER) * LANGUAGE_STRIDE
RELATION_BAND_STRIDE = len(COMFORT_STYLE_ORDER) * COMFORT_STYLE_STRIDE
INTENT_STRIDE = len(RELATION_BAND_ORDER) * RELATION_BAND_STRIDE

INTENT_TO_INDEX = {value: index for index, value in enumerate(INTENT_ORDER)}
RELATION_BAND_TO_INDEX = {
    value: index for index, value in enumerate(RELATION_BAND_ORDER)
}
COMFORT_STYLE_TO_INDEX = {
    value: index for index, value in enumerate(COMFORT_STYLE_ORDER)
}
LANGUAGE_TO_INDEX = {value: index for index, value in enumerate(LANGUAGE_ORDER)}
TURN_SLOT_TO_INDEX = {value: index for index, value in enumerate(TURN_SLOTS)}


def _verify_index_contract() -> None:
    dimensions = (
        ("intent", INTENT_ORDER, INTENTS),
        ("relation_band", RELATION_BAND_ORDER, RELATION_BANDS),
        ("comfort_style", COMFORT_STYLE_ORDER, COMFORT_STYLES),
        ("language", LANGUAGE_ORDER, LANGUAGES),
    )
    for name, order, allowed in dimensions:
        if len(order) != len(set(order)) or set(order) != set(allowed):
            raise RuntimeError(f"{name} order drifted from the bounded transport contract")

    if TURN_SLOTS != tuple(range(4)):
        raise RuntimeError("turn-slot order drifted from transport v0.1")


_verify_index_contract()


def iter_routes(persona_id: str):
    """Yield the complete bounded v0.1 route matrix in route-index order."""
    if not PERSONA_RE.fullmatch(persona_id):
        raise ValueError("invalid persona_id")

    for intent, relation, comfort, language, slot in product(
        INTENT_ORDER,
        RELATION_BAND_ORDER,
        COMFORT_STYLE_ORDER,
        LANGUAGE_ORDER,
        TURN_SLOTS,
    ):
        yield Route(persona_id, intent, relation, comfort, language, slot)


def canonical_route_key(route: Route) -> str:
    """Return a bounded route key without scheme, host, query, or user data."""
    return "/".join(
        (
            route.persona_id,
            route.intent,
            route.relation_band,
            route.comfort_style,
            route.language,
            str(route.turn_slot),
        )
    )


def live_relative_path(route: Route) -> str:
    """Return the live-gateway route path, intentionally without a JSON suffix."""
    return f"v1/plan/{canonical_route_key(route)}"


def static_relative_path(route: Route) -> Path:
    """Map one route to a GitHub-Pages-friendly static JSON path."""
    return Path(f"{live_relative_path(route)}.json")


def route_index(route: Route) -> int:
    """Map one bounded route to a dense stable integer in [0, ROUTE_COUNT)."""
    if not PERSONA_RE.fullmatch(route.persona_id):
        raise ValueError("invalid persona_id")

    try:
        intent_index = INTENT_TO_INDEX[route.intent]
        relation_index = RELATION_BAND_TO_INDEX[route.relation_band]
        comfort_index = COMFORT_STYLE_TO_INDEX[route.comfort_style]
        language_index = LANGUAGE_TO_INDEX[route.language]
        slot_index = TURN_SLOT_TO_INDEX[route.turn_slot]
    except KeyError as exc:
        raise ValueError("route is outside transport v0.1") from exc

    return (
        intent_index * INTENT_STRIDE
        + relation_index * RELATION_BAND_STRIDE
        + comfort_index * COMFORT_STYLE_STRIDE
        + language_index * LANGUAGE_STRIDE
        + slot_index * TURN_SLOT_STRIDE
    )


def route_from_index(index: int, persona_id: str = "default") -> Route:
    """Reverse the dense v0.1 index for editor tooling and contract tests."""
    if not PERSONA_RE.fullmatch(persona_id):
        raise ValueError("invalid persona_id")
    if isinstance(index, bool) or not isinstance(index, int):
        raise ValueError("route index must be an integer")
    if index < 0 or index >= ROUTE_COUNT:
        raise ValueError("route index outside transport v0.1")

    remainder = index
    intent_index, remainder = divmod(remainder, INTENT_STRIDE)
    relation_index, remainder = divmod(remainder, RELATION_BAND_STRIDE)
    comfort_index, remainder = divmod(remainder, COMFORT_STYLE_STRIDE)
    language_index, remainder = divmod(remainder, LANGUAGE_STRIDE)
    slot_index, remainder = divmod(remainder, TURN_SLOT_STRIDE)
    if remainder != 0:
        raise AssertionError("route-index decomposition left a remainder")

    return Route(
        persona_id=persona_id,
        intent=INTENT_ORDER[intent_index],
        relation_band=RELATION_BAND_ORDER[relation_index],
        comfort_style=COMFORT_STYLE_ORDER[comfort_index],
        language=LANGUAGE_ORDER[language_index],
        turn_slot=TURN_SLOTS[slot_index],
    )


def stable_static_request_id(route: Route) -> str:
    """Create a reproducible ID for reviewed static content.

    This ID identifies the static artifact, not a network attempt. World-side stale
    response protection must still use its local in-flight route/turn state and
    callback URL as defined by the RFC.
    """
    digest = hashlib.sha256(canonical_route_key(route).encode("utf-8")).hexdigest()[:24]
    return f"static:{digest}"


def build_static_plan(route: Route) -> dict:
    """Build deterministic BehaviorPlan-shaped content for a static route."""
    plan = build_plan(route)
    plan["request_id"] = stable_static_request_id(route)
    return plan


def generate_pack(output_dir: Path, persona_id: str = "default") -> dict:
    """Generate all route JSON files plus a Unity-editor-consumable manifest."""
    output_dir = Path(output_dir)
    routes = []

    for route in iter_routes(persona_id):
        index = route_index(route)
        static_path = static_relative_path(route)
        destination = output_dir / static_path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(
            json.dumps(
                build_static_plan(route),
                ensure_ascii=False,
                separators=(",", ":"),
            )
            + "\n",
            encoding="utf-8",
        )
        routes.append(
            {
                "route_index": index,
                "route_key": canonical_route_key(route),
                "live_relative_path": live_relative_path(route),
                "static_relative_path": static_path.as_posix(),
                "intent": route.intent,
                "relation_band": route.relation_band,
                "comfort_style": route.comfort_style,
                "language": route.language,
                "turn_slot": route.turn_slot,
            }
        )

    manifest = {
        "schema_version": SCHEMA_VERSION,
        "route_index_version": ROUTE_INDEX_VERSION,
        "persona_id": persona_id,
        "route_count": len(routes),
        "route_index_contract": {
            "layout": "mixed_radix_row_major",
            "dimension_order": [
                "intent",
                "relation_band",
                "comfort_style",
                "language",
                "turn_slot",
            ],
            "orders": {
                "intents": list(INTENT_ORDER),
                "relation_bands": list(RELATION_BAND_ORDER),
                "comfort_styles": list(COMFORT_STYLE_ORDER),
                "languages": list(LANGUAGE_ORDER),
                "turn_slots": list(TURN_SLOTS),
            },
            "strides": {
                "intent": INTENT_STRIDE,
                "relation_band": RELATION_BAND_STRIDE,
                "comfort_style": COMFORT_STYLE_STRIDE,
                "language": LANGUAGE_STRIDE,
                "turn_slot": TURN_SLOT_STRIDE,
            },
        },
        "routes": routes,
    }
    (output_dir / "route-manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Generate the bounded v0.1 trusted static response pack."
    )
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--persona", default="default")
    args = parser.parse_args()

    manifest = generate_pack(args.output, args.persona)
    print(f"generated {manifest['route_count']} routes under {args.output}")


if __name__ == "__main__":
    main()
