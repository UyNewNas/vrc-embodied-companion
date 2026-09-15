from __future__ import annotations

import argparse
import json
import re
import uuid
from dataclasses import dataclass
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import unquote, urlsplit

SCHEMA_VERSION = "0.1"
INTENTS = {"greet", "quiet_company", "talk_light", "walk_with_me", "offer_hug", "goodbye"}
RELATION_BANDS = {"new", "familiar", "warm"}
COMFORT_STYLES = {"neutral", "quiet"}
LANGUAGES = {"zh", "en"}
PERSONA_RE = re.compile(r"^[a-z][a-z0-9_-]{0,63}$")

SPEECH = {
    "zh": {
        "greet": "你来了。要先坐一会儿，还是一起走走？",
        "quiet_company": "好，我就在这里陪你，不用急着说什么。",
        "talk_light": "那就随便聊一点轻松的。今天有什么小事让你记住了吗？",
        "walk_with_me": "好，我们走一会儿。我跟着你的节奏。",
        "offer_hug": "可以。你想靠近的时候，我会在这里。",
        "goodbye": "好，下次见。今天记得给自己留一点休息的时间。",
    },
    "en": {
        "greet": "You're here. Want to sit for a bit, or take a walk?",
        "quiet_company": "Okay. I'll stay nearby; you don't have to explain anything.",
        "talk_light": "Let's keep it light. Was there one small thing you noticed today?",
        "walk_with_me": "Sure. Let's walk for a while; I'll match your pace.",
        "offer_hug": "Okay. Come closer when you want to.",
        "goodbye": "See you next time. Leave a little room to rest today.",
    },
}

ACTION_FOR_INTENT = {
    "greet": ("wave", "brief"),
    "quiet_company": ("sit_near", "soft_track"),
    "talk_light": ("look_at_player", "soft_track"),
    "walk_with_me": ("follow", "track"),
    "offer_hug": ("offer_hug", "soft_track"),
    "goodbye": ("wave", "brief"),
}


@dataclass(frozen=True)
class Route:
    persona_id: str
    intent: str
    relation_band: str
    comfort_style: str
    language: str
    turn_slot: int


class RouteError(ValueError):
    pass


def parse_route(path: str) -> Route:
    """Parse /v1/plan/<persona>/<intent>/<relation>/<comfort>/<language>/<slot>."""
    parsed = urlsplit(path)
    if parsed.query:
        raise RouteError("query parameters are not part of transport v0.1")

    parts = [unquote(part) for part in parsed.path.split("/") if part]
    if len(parts) != 8 or parts[:2] != ["v1", "plan"]:
        raise RouteError(
            "expected /v1/plan/<persona>/<intent>/<relation>/<comfort>/<language>/<slot>"
        )

    persona, intent, relation, comfort, language, slot_text = parts[2:]
    if not PERSONA_RE.fullmatch(persona):
        raise RouteError("invalid persona_id")
    if intent not in INTENTS:
        raise RouteError("invalid intent")
    if relation not in RELATION_BANDS:
        raise RouteError("invalid relation_band")
    if comfort not in COMFORT_STYLES:
        raise RouteError("invalid comfort_style")
    if language not in LANGUAGES:
        raise RouteError("invalid language")

    try:
        slot = int(slot_text)
    except ValueError as exc:
        raise RouteError("invalid turn_slot") from exc
    if slot < 0 or slot > 3:
        raise RouteError("invalid turn_slot")

    return Route(persona, intent, relation, comfort, language, slot)


def build_plan(route: Route) -> dict:
    """Return a BehaviorPlan-v0.1-compatible deterministic reference response."""
    speech = SPEECH[route.language][route.intent]

    if route.relation_band == "warm" and route.intent == "greet":
        speech = ("你回来了。" if route.language == "zh" else "You're back.") + " " + speech
    if route.comfort_style == "quiet" and route.intent == "talk_light":
        speech = (
            "我们慢慢聊，不用说很多。"
            if route.language == "zh"
            else "We can keep it slow; you don't have to say much."
        )

    action, gaze = ACTION_FOR_INTENT[route.intent]
    behavior = {"action": action, "gaze": gaze}
    if action == "follow":
        behavior["target_distance_m"] = 1.4
    elif action == "sit_near":
        behavior["target_distance_m"] = 0.9

    return {
        "schema_version": SCHEMA_VERSION,
        "request_id": str(uuid.uuid4()),
        "speech": speech,
        "style": "quiet" if route.comfort_style == "quiet" else "gentle",
        "behavior": behavior,
    }


class Handler(BaseHTTPRequestHandler):
    server_version = "VRCCompanionReference/0.1"

    def do_GET(self) -> None:
        try:
            self._json(200, build_plan(parse_route(self.path)))
        except RouteError as exc:
            self._json(
                400,
                {
                    "schema_version": SCHEMA_VERSION,
                    "error": "invalid_route",
                    "message": str(exc),
                },
            )

    def do_POST(self) -> None:
        self._json(
            405,
            {
                "schema_version": SCHEMA_VERSION,
                "error": "method_not_allowed",
                "message": "transport v0.1 is GET-only",
            },
        )

    def _json(self, status: int, payload: dict) -> None:
        body = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format: str, *args) -> None:
        # Privacy-first reference behavior: do not log client addresses or routes.
        return


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8787)
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), Handler)
    print(f"reference transport listening on http://{args.host}:{args.port}")
    server.serve_forever()


if __name__ == "__main__":
    main()
