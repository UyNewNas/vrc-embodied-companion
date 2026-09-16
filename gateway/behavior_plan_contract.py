from __future__ import annotations

import math
import re
from typing import Any

SCHEMA_VERSION = "0.1"

ROOT_FIELDS = {
    "schema_version",
    "request_id",
    "speech",
    "style",
    "behavior",
    "memory_proposals",
}
STYLE_VALUES = {"neutral", "gentle", "quiet", "playful"}
ACTION_VALUES = {
    "idle",
    "look_at_player",
    "look_away",
    "approach",
    "keep_distance",
    "sit_near",
    "follow",
    "stay",
    "react_headpat",
    "offer_hug",
    "wave",
    "sleep_idle",
}
GAZE_VALUES = {"none", "brief", "soft_track", "track", "look_away"}
BEHAVIOR_FIELDS = {"action", "duration_s", "target_distance_m", "gaze"}
MEMORY_FIELDS = {"class", "key", "value", "reason"}
MEMORY_CLASSES = {"explicit", "preference", "episode"}
MEMORY_KEY_RE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")


def _is_json_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def validate_behavior_plan(plan: Any) -> tuple[bool, str]:
    """Validate the exact BehaviorPlan v0.1 contract without third-party packages."""
    if not isinstance(plan, dict):
        return False, "root_not_object"

    unknown = set(plan) - ROOT_FIELDS
    if unknown:
        return False, "unknown_root_field"

    if plan.get("schema_version") != SCHEMA_VERSION:
        return False, "schema_version"

    request_id = plan.get("request_id")
    if not isinstance(request_id, str) or not 1 <= len(request_id) <= 128:
        return False, "request_id"

    has_speech = "speech" in plan
    has_behavior = "behavior" in plan
    if not has_speech and not has_behavior:
        return False, "speech_or_behavior_required"

    if has_speech:
        speech = plan["speech"]
        if not isinstance(speech, str) or len(speech) > 1000:
            return False, "speech"

    if "style" in plan:
        style = plan["style"]
        if not isinstance(style, str) or style not in STYLE_VALUES:
            return False, "style"

    if has_behavior:
        behavior = plan["behavior"]
        if not isinstance(behavior, dict):
            return False, "behavior_not_object"
        if set(behavior) - BEHAVIOR_FIELDS:
            return False, "unknown_behavior_field"
        action = behavior.get("action")
        if not isinstance(action, str) or action not in ACTION_VALUES:
            return False, "behavior_action"
        if "duration_s" in behavior:
            duration = behavior["duration_s"]
            if not _is_json_number(duration) or not 0 <= duration <= 120:
                return False, "behavior_duration_s"
        if "target_distance_m" in behavior:
            distance = behavior["target_distance_m"]
            if not _is_json_number(distance) or not 0.2 <= distance <= 5.0:
                return False, "behavior_target_distance_m"
        if "gaze" in behavior:
            gaze = behavior["gaze"]
            if not isinstance(gaze, str) or gaze not in GAZE_VALUES:
                return False, "behavior_gaze"

    if "memory_proposals" in plan:
        proposals = plan["memory_proposals"]
        if not isinstance(proposals, list) or len(proposals) > 4:
            return False, "memory_proposals"
        for proposal in proposals:
            if not isinstance(proposal, dict):
                return False, "memory_proposal_not_object"
            if set(proposal) != MEMORY_FIELDS:
                return False, "memory_proposal_fields"
            memory_class = proposal["class"]
            if not isinstance(memory_class, str) or memory_class not in MEMORY_CLASSES:
                return False, "memory_proposal_class"
            key = proposal["key"]
            if not isinstance(key, str) or MEMORY_KEY_RE.fullmatch(key) is None:
                return False, "memory_proposal_key"
            value = proposal["value"]
            if isinstance(value, str):
                if len(value) > 256:
                    return False, "memory_proposal_value"
            elif isinstance(value, bool):
                pass
            elif not _is_json_number(value):
                return False, "memory_proposal_value"
            reason = proposal["reason"]
            if not isinstance(reason, str) or not 1 <= len(reason) <= 256:
                return False, "memory_proposal_reason"

    return True, ""
