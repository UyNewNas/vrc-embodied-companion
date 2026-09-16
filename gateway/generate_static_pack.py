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

TURN_SLOTS = tuple(range(4))


def iter_routes(persona_id: str):
    """Yield the complete bounded v0.1 route matrix in stable order."""
    if not PERSONA_RE.fullmatch(persona_id):
        raise ValueError("invalid persona_id")

    for intent, relation, comfort, language, slot in product(
        sorted(INTENTS),
        sorted(RELATION_BANDS),
        sorted(COMFORT_STYLES),
        sorted(LANGUAGES),
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


def static_relative_path(route: Route) -> Path:
    """Map one route to a GitHub-Pages-friendly static JSON path."""
    return Path(
        "v1",
        "plan",
        route.persona_id,
        route.intent,
        route.relation_band,
        route.comfort_style,
        route.language,
        f"{route.turn_slot}.json",
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
    # The live reference server emits a fresh UUID. A reviewed static pack should
    # be byte-for-byte reproducible, so replace only request_id with an artifact ID.
    plan["request_id"] = stable_static_request_id(route)
    return plan


def generate_pack(output_dir: Path, persona_id: str = "default") -> dict:
    """Generate all route JSON files plus a machine-readable route manifest."""
    output_dir = Path(output_dir)
    routes = []

    for route in iter_routes(persona_id):
        relative_path = static_relative_path(route)
        destination = output_dir / relative_path
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
                "route_key": canonical_route_key(route),
                "relative_path": relative_path.as_posix(),
                "intent": route.intent,
                "relation_band": route.relation_band,
                "comfort_style": route.comfort_style,
                "language": route.language,
                "turn_slot": route.turn_slot,
            }
        )

    manifest = {
        "schema_version": SCHEMA_VERSION,
        "persona_id": persona_id,
        "route_count": len(routes),
        "dimensions": {
            "intents": sorted(INTENTS),
            "relation_bands": sorted(RELATION_BANDS),
            "comfort_styles": sorted(COMFORT_STYLES),
            "languages": sorted(LANGUAGES),
            "turn_slots": list(TURN_SLOTS),
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
