using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Fail-closed parser/validator for BehaviorPlan v0.1 JSON received from external
    /// string loading.
    ///
    /// This mirrors schemas/behavior-plan.v0.1.schema.json closely enough for the
    /// runtime transport boundary. It intentionally rejects unknown fields and
    /// unsupported schema versions instead of trying to guess future semantics.
    ///
    /// Unity/UdonSharp compilation and VRChat runtime behavior remain unverified until
    /// issue #2 provides a runnable VCC World project.
    /// </summary>
    public class CompanionBehaviorPlanValidator : UdonSharpBehaviour
    {
        private const string ExpectedSchemaVersion = "0.1";
        private const int MaximumPayloadCharacters = 16384;

        [Header("Latest validation result")]
        public bool lastValid;
        public string lastRejectReason = "";
        public int acceptedPlanCount;
        public int rejectedPlanCount;

        [Header("Latest accepted plan")]
        public string lastRequestId = "";
        public bool hasSpeech;
        public string lastSpeech = "";
        public string lastStyle = "";
        public bool hasBehavior;
        public string lastAction = "";
        public bool hasDurationSeconds;
        public float lastDurationSeconds;
        public bool hasTargetDistanceMeters;
        public float lastTargetDistanceMeters;
        public string lastGaze = "";
        public int lastMemoryProposalCount;

        public bool TryValidate(string payload)
        {
            ResetParsedPlan();

            if (string.IsNullOrEmpty(payload))
            {
                return Reject("payload_empty");
            }

            if (payload.Length > MaximumPayloadCharacters)
            {
                return Reject("payload_too_large");
            }

            DataToken rootToken;
            if (!VRCJson.TryDeserializeFromJson(payload, out rootToken))
            {
                return Reject("json_parse_error");
            }

            if (rootToken.TokenType != TokenType.DataDictionary)
            {
                return Reject("root_not_object");
            }

            DataDictionary root = rootToken.DataDictionary;
            if (!ValidateRootFields(root))
            {
                return false;
            }

            DataToken token;
            if (!root.TryGetValue("schema_version", TokenType.String, out token)
                || token.String != ExpectedSchemaVersion)
            {
                return Reject("schema_version");
            }

            if (!root.TryGetValue("request_id", TokenType.String, out token))
            {
                return Reject("request_id");
            }

            string requestId = token.String;
            if (string.IsNullOrEmpty(requestId) || requestId.Length > 128)
            {
                return Reject("request_id");
            }

            bool speechPresent = root.ContainsKey("speech");
            bool behaviorPresent = root.ContainsKey("behavior");
            if (!speechPresent && !behaviorPresent)
            {
                return Reject("speech_or_behavior_required");
            }

            if (speechPresent)
            {
                if (!root.TryGetValue("speech", TokenType.String, out token))
                {
                    return Reject("speech");
                }

                string speech = token.String;
                if (speech.Length > 1000)
                {
                    return Reject("speech");
                }

                hasSpeech = true;
                lastSpeech = speech;
            }

            if (root.ContainsKey("style"))
            {
                if (!root.TryGetValue("style", TokenType.String, out token))
                {
                    return Reject("style");
                }

                string style = token.String;
                if (!IsAllowedStyle(style))
                {
                    return Reject("style");
                }

                lastStyle = style;
            }

            if (behaviorPresent)
            {
                if (!root.TryGetValue("behavior", TokenType.DataDictionary, out token))
                {
                    return Reject("behavior_not_object");
                }

                if (!ValidateBehavior(token.DataDictionary))
                {
                    return false;
                }

                hasBehavior = true;
            }

            if (root.ContainsKey("memory_proposals"))
            {
                if (!root.TryGetValue("memory_proposals", TokenType.DataList, out token))
                {
                    return Reject("memory_proposals");
                }

                if (!ValidateMemoryProposals(token.DataList))
                {
                    return false;
                }
            }

            lastRequestId = requestId;
            lastValid = true;
            lastRejectReason = "";
            acceptedPlanCount++;
            return true;
        }

        private bool ValidateRootFields(DataDictionary root)
        {
            DataList keys = root.GetKeys();
            for (int i = 0; i < keys.Count; i++)
            {
                DataToken keyToken = keys[i];
                if (keyToken.TokenType != TokenType.String)
                {
                    return Reject("unknown_root_field");
                }

                string key = keyToken.String;
                if (key != "schema_version"
                    && key != "request_id"
                    && key != "speech"
                    && key != "style"
                    && key != "behavior"
                    && key != "memory_proposals")
                {
                    return Reject("unknown_root_field");
                }
            }

            return true;
        }

        private bool ValidateBehavior(DataDictionary behavior)
        {
            DataList keys = behavior.GetKeys();
            for (int i = 0; i < keys.Count; i++)
            {
                DataToken keyToken = keys[i];
                if (keyToken.TokenType != TokenType.String)
                {
                    return Reject("unknown_behavior_field");
                }

                string key = keyToken.String;
                if (key != "action"
                    && key != "duration_s"
                    && key != "target_distance_m"
                    && key != "gaze")
                {
                    return Reject("unknown_behavior_field");
                }
            }

            DataToken token;
            if (!behavior.TryGetValue("action", TokenType.String, out token))
            {
                return Reject("behavior_action");
            }

            string action = token.String;
            if (!IsAllowedAction(action))
            {
                return Reject("behavior_action");
            }
            lastAction = action;

            if (behavior.ContainsKey("duration_s"))
            {
                if (!behavior.TryGetValue("duration_s", TokenType.Double, out token))
                {
                    return Reject("behavior_duration_s");
                }

                double duration = token.Double;
                if (duration < 0.0 || duration > 120.0)
                {
                    return Reject("behavior_duration_s");
                }

                hasDurationSeconds = true;
                lastDurationSeconds = (float)duration;
            }

            if (behavior.ContainsKey("target_distance_m"))
            {
                if (!behavior.TryGetValue("target_distance_m", TokenType.Double, out token))
                {
                    return Reject("behavior_target_distance_m");
                }

                double distance = token.Double;
                if (distance < 0.2 || distance > 5.0)
                {
                    return Reject("behavior_target_distance_m");
                }

                hasTargetDistanceMeters = true;
                lastTargetDistanceMeters = (float)distance;
            }

            if (behavior.ContainsKey("gaze"))
            {
                if (!behavior.TryGetValue("gaze", TokenType.String, out token))
                {
                    return Reject("behavior_gaze");
                }

                string gaze = token.String;
                if (!IsAllowedGaze(gaze))
                {
                    return Reject("behavior_gaze");
                }

                lastGaze = gaze;
            }

            return true;
        }

        private bool ValidateMemoryProposals(DataList proposals)
        {
            if (proposals.Count > 4)
            {
                return Reject("memory_proposals");
            }

            for (int i = 0; i < proposals.Count; i++)
            {
                DataToken proposalToken = proposals[i];
                if (proposalToken.TokenType != TokenType.DataDictionary)
                {
                    return Reject("memory_proposal_not_object");
                }

                if (!ValidateMemoryProposal(proposalToken.DataDictionary))
                {
                    return false;
                }
            }

            lastMemoryProposalCount = proposals.Count;
            return true;
        }

        private bool ValidateMemoryProposal(DataDictionary proposal)
        {
            if (proposal.Count != 4)
            {
                return Reject("memory_proposal_fields");
            }

            DataList keys = proposal.GetKeys();
            for (int i = 0; i < keys.Count; i++)
            {
                DataToken keyToken = keys[i];
                if (keyToken.TokenType != TokenType.String)
                {
                    return Reject("memory_proposal_fields");
                }

                string key = keyToken.String;
                if (key != "class" && key != "key" && key != "value" && key != "reason")
                {
                    return Reject("memory_proposal_fields");
                }
            }

            DataToken token;
            if (!proposal.TryGetValue("class", TokenType.String, out token)
                || !IsAllowedMemoryClass(token.String))
            {
                return Reject("memory_proposal_class");
            }

            if (!proposal.TryGetValue("key", TokenType.String, out token)
                || !IsAllowedMemoryKey(token.String))
            {
                return Reject("memory_proposal_key");
            }

            if (!proposal.TryGetValue("value", out token))
            {
                return Reject("memory_proposal_value");
            }

            if (token.TokenType == TokenType.String)
            {
                string value = token.String;
                if (value.Length > 256)
                {
                    return Reject("memory_proposal_value");
                }
            }
            else if (token.TokenType != TokenType.Boolean && token.TokenType != TokenType.Double)
            {
                return Reject("memory_proposal_value");
            }

            if (!proposal.TryGetValue("reason", TokenType.String, out token))
            {
                return Reject("memory_proposal_reason");
            }

            string reason = token.String;
            if (string.IsNullOrEmpty(reason) || reason.Length > 256)
            {
                return Reject("memory_proposal_reason");
            }

            return true;
        }

        private bool IsAllowedStyle(string value)
        {
            return value == "neutral"
                || value == "gentle"
                || value == "quiet"
                || value == "playful";
        }

        private bool IsAllowedAction(string value)
        {
            return value == "idle"
                || value == "look_at_player"
                || value == "look_away"
                || value == "approach"
                || value == "keep_distance"
                || value == "sit_near"
                || value == "follow"
                || value == "stay"
                || value == "react_headpat"
                || value == "offer_hug"
                || value == "wave"
                || value == "sleep_idle";
        }

        private bool IsAllowedGaze(string value)
        {
            return value == "none"
                || value == "brief"
                || value == "soft_track"
                || value == "track"
                || value == "look_away";
        }

        private bool IsAllowedMemoryClass(string value)
        {
            return value == "explicit" || value == "preference" || value == "episode";
        }

        private bool IsAllowedMemoryKey(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return false;
            }

            char first = value[0];
            if (first < 'a' || first > 'z')
            {
                return false;
            }

            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i];
                bool lower = c >= 'a' && c <= 'z';
                bool digit = c >= '0' && c <= '9';
                if (!lower && !digit && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private bool Reject(string reason)
        {
            lastValid = false;
            lastRejectReason = reason;
            rejectedPlanCount++;
            return false;
        }

        private void ResetParsedPlan()
        {
            lastValid = false;
            lastRejectReason = "";
            lastRequestId = "";
            hasSpeech = false;
            lastSpeech = "";
            lastStyle = "";
            hasBehavior = false;
            lastAction = "";
            hasDurationSeconds = false;
            lastDurationSeconds = 0f;
            hasTargetDistanceMeters = false;
            lastTargetDistanceMeters = 0f;
            lastGaze = "";
            lastMemoryProposalCount = 0;
        }
    }
}
