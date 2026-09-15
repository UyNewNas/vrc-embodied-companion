using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Uncompiled v0.1 prototype for issue #5.
    ///
    /// This behaviour intentionally exposes normalized/debug state as public fields so it can be
    /// inspected in ClientSim / Build & Test before the data model is optimized. It is not claimed
    /// to be runtime-verified until issue #2 provides a runnable world and issue #13 calibrates it.
    /// </summary>
    public class CompanionPerceptionProbe : UdonSharpBehaviour
    {
        [Header("Companion anchors")]
        public Transform companionRoot;
        public Transform companionGazeAnchor;
        public Transform companionHeadTouchAnchor;

        [Header("Optional event sink")]
        public UdonBehaviour eventReceiver;
        public string eventReceiverMethod = "OnCompanionPerceptionEvent";

        [Header("Sampling")]
        public float sampleIntervalSeconds = 0.10f;
        public float teleportResetMeters = 1.0f;

        [Header("Distance hysteresis (meters)")]
        public float contactEnter = 0.30f;
        public float contactExit = 0.40f;
        public float nearEnter = 1.10f;
        public float nearExit = 1.30f;
        public float socialEnter = 2.85f;
        public float socialExit = 3.15f;
        public int spatialStableSamples = 2;

        [Header("Facing")]
        public float towardEnterDot = 0.65f;
        public float towardExitDot = 0.50f;
        public float awayEnterDot = -0.25f;
        public float awayExitDot = -0.10f;
        public int facingStableSamples = 2;

        [Header("Player motion")]
        public float radialMotionEmaAlpha = 0.35f;
        public float radialStartSpeed = 0.12f;
        public float radialStopSpeed = 0.06f;
        public float movingOtherSpeed = 0.08f;
        public int motionStableSamples = 3;

        [Header("Headpat")]
        public bool touchResponseAllowed = true;
        public float headpatEnterRadius = 0.16f;
        public float headpatExitRadius = 0.22f;
        public float headpatEnterDwellSeconds = 0.20f;
        public float headpatExitDwellSeconds = 0.30f;
        public float headpatWindowSeconds = 0.60f;
        public float headpatMinPathMeters = 0.08f;
        public float handDiscontinuityMeters = 0.50f;
        public float reversalDotThreshold = 0.25f;

        [Header("Inactivity")]
        public float inactivityShortSeconds = 10f;
        public float inactivityLongSeconds = 60f;
        public float activityRootDisplacement = 0.10f;
        public float activityHeadDisplacement = 0.12f;

        [Header("Debug: raw")]
        public bool perceptionReady;
        public bool userInVr;
        public float sampleTimestamp;
        public int sampleSequence;
        public float avatarEyeHeightMeters;
        public float distanceMeters;
        public float facingDot;
        public float rawPlayerRadialSpeed;
        public float filteredPlayerRadialSpeed;
        public float leftHandHeadDistance;
        public float rightHandHeadDistance;
        public float inactivitySeconds;

        [Header("Debug: normalized")]
        public string distanceBand = "unknown";
        public string facing = "unknown";
        public string motion = "unknown";
        public string activeHeadpatSource = "none";
        public string lastEventType = "";
        public string lastEventSource = "";
        public float lastEventConfidence;
        public int eventSequence;

        private VRCPlayerApi _localPlayer;
        private bool _hasPreviousRootSample;
        private Vector3 _previousPlayerRoot;
        private Vector3 _previousCompanionRoot;

        private int _distanceBandCode;
        private int _pendingDistanceBandCode;
        private int _pendingDistanceBandSamples;

        private int _facingCode;
        private int _pendingFacingCode;
        private int _pendingFacingSamples;

        private int _motionCode;
        private int _pendingMotionCode;
        private int _pendingMotionSamples;
        private bool _hasMotionFilter;

        // 0 = left, 1 = right
        private readonly Vector3[] _previousHandPosition = new Vector3[2];
        private readonly Vector3[] _previousHandStep = new Vector3[2];
        private readonly bool[] _hasPreviousHand = new bool[2];
        private readonly float[] _headpatDwell = new float[2];
        private readonly float[] _headpatWindow = new float[2];
        private readonly float[] _headpatPath = new float[2];
        private readonly int[] _headpatReversals = new int[2];
        private readonly float[] _headpatOutsideDwell = new float[2];
        private int _activeHeadpatHand = -1;

        private Vector3 _activityRootAnchor;
        private Vector3 _activityHeadAnchor;
        private bool _hasActivityAnchor;
        private float _lastMeaningfulActivityTime;
        private bool _shortInactivityEmitted;
        private bool _longInactivityEmitted;

        public void Start()
        {
            _localPlayer = Networking.LocalPlayer;
            _lastMeaningfulActivityTime = Time.time;
            SendCustomEventDelayedSeconds("SamplePerception", sampleIntervalSeconds);
        }

        public void SamplePerception()
        {
            sampleTimestamp = Time.time;
            sampleSequence++;

            if (!Utilities.IsValid(_localPlayer))
            {
                _localPlayer = Networking.LocalPlayer;
            }

            if (!Utilities.IsValid(_localPlayer) || companionRoot == null || companionGazeAnchor == null || companionHeadTouchAnchor == null)
            {
                perceptionReady = false;
                ResetTransientClassification();
                SendCustomEventDelayedSeconds("SamplePerception", sampleIntervalSeconds);
                return;
            }

            perceptionReady = true;
            userInVr = _localPlayer.IsUserInVR();
            avatarEyeHeightMeters = _localPlayer.GetAvatarEyeHeightAsMeters();

            Vector3 playerRoot = _localPlayer.GetPosition();
            Vector3 companionRootPosition = companionRoot.position;
            VRCPlayerApi.TrackingData head = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            VRCPlayerApi.TrackingData leftHand = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);
            VRCPlayerApi.TrackingData rightHand = _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);

            bool discontinuity = DetectDiscontinuity(playerRoot, companionRootPosition);
            if (discontinuity)
            {
                ResetMotionFilter();
                ResetHeadpatCandidates();
            }

            UpdateDistance(playerRoot, companionRootPosition, discontinuity);
            UpdateFacing(head);
            UpdateMotion(playerRoot, companionRootPosition, _localPlayer.GetVelocity(), discontinuity);
            UpdateHeadpat(leftHand.position, rightHand.position);
            UpdateInactivity(playerRoot, head.position);

            _previousPlayerRoot = playerRoot;
            _previousCompanionRoot = companionRootPosition;
            _hasPreviousRootSample = true;

            SendCustomEventDelayedSeconds("SamplePerception", sampleIntervalSeconds);
        }

        public void SuppressTouch()
        {
            touchResponseAllowed = false;
            EndActiveHeadpat("intent");
            ResetHeadpatCandidates();
            MarkMeaningfulActivity();
        }

        public void AllowTouch()
        {
            touchResponseAllowed = true;
            MarkMeaningfulActivity();
        }

        private bool DetectDiscontinuity(Vector3 playerRoot, Vector3 companionRootPosition)
        {
            if (!_hasPreviousRootSample)
            {
                return false;
            }

            return Vector3.Distance(playerRoot, _previousPlayerRoot) > teleportResetMeters
                || Vector3.Distance(companionRootPosition, _previousCompanionRoot) > teleportResetMeters;
        }

        private void UpdateDistance(Vector3 playerRoot, Vector3 companionRootPosition, bool discontinuity)
        {
            Vector3 delta = companionRootPosition - playerRoot;
            delta.y = 0f;
            distanceMeters = delta.magnitude;

            int candidate = EvaluateDistanceBand(distanceMeters);
            if (_distanceBandCode == 0 || discontinuity)
            {
                _distanceBandCode = candidate;
                _pendingDistanceBandCode = 0;
                _pendingDistanceBandSamples = 0;
                distanceBand = DistanceBandName(_distanceBandCode);
                return;
            }

            if (candidate == _distanceBandCode)
            {
                _pendingDistanceBandCode = 0;
                _pendingDistanceBandSamples = 0;
                return;
            }

            if (_pendingDistanceBandCode != candidate)
            {
                _pendingDistanceBandCode = candidate;
                _pendingDistanceBandSamples = 1;
                return;
            }

            _pendingDistanceBandSamples++;
            if (_pendingDistanceBandSamples < spatialStableSamples)
            {
                return;
            }

            _distanceBandCode = candidate;
            _pendingDistanceBandCode = 0;
            _pendingDistanceBandSamples = 0;
            distanceBand = DistanceBandName(_distanceBandCode);
            EmitEvent(DistanceBandEvent(_distanceBandCode), "distance", 1f);
            MarkMeaningfulActivity();
        }

        private int EvaluateDistanceBand(float distance)
        {
            // 1 contact, 2 near, 3 social, 4 far
            if (_distanceBandCode == 1)
            {
                if (distance < contactExit) return 1;
            }
            else if (_distanceBandCode == 2)
            {
                if (distance <= contactEnter) return 1;
                if (distance < nearExit) return 2;
            }
            else if (_distanceBandCode == 3)
            {
                if (distance <= contactEnter) return 1;
                if (distance <= nearEnter) return 2;
                if (distance < socialExit) return 3;
            }
            else if (_distanceBandCode == 4)
            {
                if (distance <= contactEnter) return 1;
                if (distance <= nearEnter) return 2;
                if (distance <= socialEnter) return 3;
                return 4;
            }

            if (distance < 0.35f) return 1;
            if (distance < 1.20f) return 2;
            if (distance < 3.00f) return 3;
            return 4;
        }

        private void UpdateFacing(VRCPlayerApi.TrackingData head)
        {
            Vector3 toCompanion = companionGazeAnchor.position - head.position;
            if (toCompanion.sqrMagnitude < 0.0001f)
            {
                SetFacingImmediately(0);
                return;
            }

            toCompanion.Normalize();
            Vector3 headForward = head.rotation * Vector3.forward;
            facingDot = Vector3.Dot(headForward, toCompanion);

            int candidate = EvaluateFacing(facingDot);
            if (_facingCode == 0)
            {
                SetFacingImmediately(candidate);
                return;
            }

            if (candidate == _facingCode)
            {
                _pendingFacingCode = 0;
                _pendingFacingSamples = 0;
                return;
            }

            if (_pendingFacingCode != candidate)
            {
                _pendingFacingCode = candidate;
                _pendingFacingSamples = 1;
                return;
            }

            _pendingFacingSamples++;
            if (_pendingFacingSamples < facingStableSamples)
            {
                return;
            }

            int previous = _facingCode;
            SetFacingImmediately(candidate);
            if (candidate == 1 && previous != 1)
            {
                EmitEvent("facing_toward_started", "tracking", FacingConfidence(facingDot, true));
                MarkMeaningfulActivity();
            }
            else if (candidate == 3 && previous != 3)
            {
                EmitEvent("facing_away_started", "tracking", FacingConfidence(facingDot, false));
                MarkMeaningfulActivity();
            }
        }

        private int EvaluateFacing(float dot)
        {
            // 1 toward, 2 sideways, 3 away
            if (_facingCode == 1 && dot > towardExitDot) return 1;
            if (_facingCode == 3 && dot < awayExitDot) return 3;
            if (dot >= towardEnterDot) return 1;
            if (dot <= awayEnterDot) return 3;
            return 2;
        }

        private void SetFacingImmediately(int code)
        {
            _facingCode = code;
            _pendingFacingCode = 0;
            _pendingFacingSamples = 0;
            facing = code == 1 ? "toward_companion" : code == 2 ? "sideways" : code == 3 ? "away" : "unknown";
        }

        private float FacingConfidence(float dot, bool toward)
        {
            if (toward)
            {
                return Mathf.Clamp01((dot - towardExitDot) / Mathf.Max(0.001f, 1f - towardExitDot));
            }
            return Mathf.Clamp01((awayExitDot - dot) / Mathf.Max(0.001f, awayExitDot + 1f));
        }

        private void UpdateMotion(Vector3 playerRoot, Vector3 companionRootPosition, Vector3 playerVelocity, bool discontinuity)
        {
            if (discontinuity)
            {
                motion = "unknown";
                return;
            }

            Vector3 toward = companionRootPosition - playerRoot;
            toward.y = 0f;
            Vector3 horizontalVelocity = playerVelocity;
            horizontalVelocity.y = 0f;

            if (toward.sqrMagnitude < 0.0001f)
            {
                rawPlayerRadialSpeed = 0f;
            }
            else
            {
                toward.Normalize();
                // Positive means the player's own velocity points toward the companion.
                rawPlayerRadialSpeed = Vector3.Dot(horizontalVelocity, toward);
            }

            if (!_hasMotionFilter)
            {
                filteredPlayerRadialSpeed = rawPlayerRadialSpeed;
                _hasMotionFilter = true;
            }
            else
            {
                filteredPlayerRadialSpeed = Mathf.Lerp(filteredPlayerRadialSpeed, rawPlayerRadialSpeed, radialMotionEmaAlpha);
            }

            int candidate;
            if (filteredPlayerRadialSpeed >= radialStartSpeed)
            {
                candidate = 2; // approaching
            }
            else if (filteredPlayerRadialSpeed <= -radialStartSpeed)
            {
                candidate = 3; // departing
            }
            else if (horizontalVelocity.magnitude >= movingOtherSpeed && Mathf.Abs(filteredPlayerRadialSpeed) > radialStopSpeed)
            {
                candidate = 4; // moving_other
            }
            else if (Mathf.Abs(filteredPlayerRadialSpeed) <= radialStopSpeed)
            {
                candidate = horizontalVelocity.magnitude >= movingOtherSpeed ? 4 : 1; // still / moving_other
            }
            else
            {
                candidate = _motionCode == 0 ? 1 : _motionCode;
            }

            UpdateStableMotion(candidate);
        }

        private void UpdateStableMotion(int candidate)
        {
            if (_motionCode == 0)
            {
                SetMotionImmediately(candidate);
                return;
            }

            if (candidate == _motionCode)
            {
                _pendingMotionCode = 0;
                _pendingMotionSamples = 0;
                return;
            }

            if (_pendingMotionCode != candidate)
            {
                _pendingMotionCode = candidate;
                _pendingMotionSamples = 1;
                return;
            }

            _pendingMotionSamples++;
            if (_pendingMotionSamples < motionStableSamples)
            {
                return;
            }

            int previous = _motionCode;
            SetMotionImmediately(candidate);
            if (candidate == 2 && previous != 2)
            {
                EmitEvent("approach_started", "tracking", 1f);
                MarkMeaningfulActivity();
            }
            else if (candidate == 3 && previous != 3)
            {
                EmitEvent("departure_started", "tracking", 1f);
                MarkMeaningfulActivity();
            }
        }

        private void SetMotionImmediately(int code)
        {
            _motionCode = code;
            _pendingMotionCode = 0;
            _pendingMotionSamples = 0;
            motion = code == 1 ? "still" : code == 2 ? "approaching" : code == 3 ? "departing" : code == 4 ? "moving_other" : "unknown";
        }

        private void UpdateHeadpat(Vector3 leftHand, Vector3 rightHand)
        {
            leftHandHeadDistance = Vector3.Distance(leftHand, companionHeadTouchAnchor.position);
            rightHandHeadDistance = Vector3.Distance(rightHand, companionHeadTouchAnchor.position);

            if (!touchResponseAllowed || !userInVr)
            {
                EndActiveHeadpat("tracking");
                ResetHeadpatCandidates();
                return;
            }

            float dt = Mathf.Max(0.001f, sampleIntervalSeconds);

            if (_activeHeadpatHand >= 0)
            {
                float activeDistance = _activeHeadpatHand == 0 ? leftHandHeadDistance : rightHandHeadDistance;
                if (activeDistance >= headpatExitRadius)
                {
                    _headpatOutsideDwell[_activeHeadpatHand] += dt;
                    if (_headpatOutsideDwell[_activeHeadpatHand] >= headpatExitDwellSeconds)
                    {
                        EndActiveHeadpat(HandSource(_activeHeadpatHand));
                    }
                }
                else
                {
                    _headpatOutsideDwell[_activeHeadpatHand] = 0f;
                }
                return;
            }

            bool leftQualifies = UpdateHeadpatCandidate(0, leftHand, leftHandHeadDistance, dt);
            bool rightQualifies = UpdateHeadpatCandidate(1, rightHand, rightHandHeadDistance, dt);

            if (!leftQualifies && !rightQualifies)
            {
                return;
            }

            int selected = leftQualifies && rightQualifies
                ? (leftHandHeadDistance <= rightHandHeadDistance ? 0 : 1)
                : (leftQualifies ? 0 : 1);

            _activeHeadpatHand = selected;
            activeHeadpatSource = HandSource(selected);
            _headpatOutsideDwell[selected] = 0f;
            EmitEvent("headpat_started", activeHeadpatSource, HeadpatConfidence(selected));
            MarkMeaningfulActivity();
        }

        private bool UpdateHeadpatCandidate(int hand, Vector3 position, float distance, float dt)
        {
            if (distance > headpatEnterRadius)
            {
                ResetHeadpatCandidate(hand);
                _previousHandPosition[hand] = position;
                _hasPreviousHand[hand] = true;
                return false;
            }

            _headpatDwell[hand] += dt;
            _headpatWindow[hand] += dt;

            if (_hasPreviousHand[hand])
            {
                Vector3 step = position - _previousHandPosition[hand];
                float stepLength = step.magnitude;
                if (stepLength > handDiscontinuityMeters)
                {
                    ResetHeadpatCandidate(hand);
                    _previousHandPosition[hand] = position;
                    _hasPreviousHand[hand] = true;
                    return false;
                }

                _headpatPath[hand] += stepLength;
                if (stepLength > 0.005f && _previousHandStep[hand].magnitude > 0.005f)
                {
                    float directionDot = Vector3.Dot(step.normalized, _previousHandStep[hand].normalized);
                    if (directionDot <= reversalDotThreshold)
                    {
                        _headpatReversals[hand]++;
                    }
                }
                if (stepLength > 0.005f)
                {
                    _previousHandStep[hand] = step;
                }
            }

            _previousHandPosition[hand] = position;
            _hasPreviousHand[hand] = true;

            bool qualifies = _headpatDwell[hand] >= headpatEnterDwellSeconds
                && _headpatPath[hand] >= headpatMinPathMeters
                && _headpatReversals[hand] >= 1;

            if (!qualifies && _headpatWindow[hand] >= headpatWindowSeconds)
            {
                // Keep proximity dwell but require meaningful motion inside a fresh window.
                _headpatWindow[hand] = 0f;
                _headpatPath[hand] = 0f;
                _headpatReversals[hand] = 0;
                _previousHandStep[hand] = Vector3.zero;
            }

            return qualifies;
        }

        private float HeadpatConfidence(int hand)
        {
            float distance = hand == 0 ? leftHandHeadDistance : rightHandHeadDistance;
            float proximity = Mathf.Clamp01(1f - distance / Mathf.Max(0.001f, headpatEnterRadius));
            float dwell = Mathf.Clamp01(_headpatDwell[hand] / Mathf.Max(0.001f, headpatEnterDwellSeconds));
            float path = Mathf.Clamp01(_headpatPath[hand] / Mathf.Max(0.001f, headpatMinPathMeters));
            float reversal = _headpatReversals[hand] > 0 ? 1f : 0f;
            return Mathf.Clamp01((proximity + dwell + path + reversal) * 0.25f);
        }

        private void EndActiveHeadpat(string source)
        {
            if (_activeHeadpatHand < 0)
            {
                return;
            }

            string actualSource = HandSource(_activeHeadpatHand);
            _activeHeadpatHand = -1;
            activeHeadpatSource = "none";
            EmitEvent("headpat_ended", source == "intent" ? "intent" : actualSource, 1f);
            MarkMeaningfulActivity();
        }

        private void ResetHeadpatCandidates()
        {
            ResetHeadpatCandidate(0);
            ResetHeadpatCandidate(1);
            _hasPreviousHand[0] = false;
            _hasPreviousHand[1] = false;
        }

        private void ResetHeadpatCandidate(int hand)
        {
            _headpatDwell[hand] = 0f;
            _headpatWindow[hand] = 0f;
            _headpatPath[hand] = 0f;
            _headpatReversals[hand] = 0;
            _headpatOutsideDwell[hand] = 0f;
            _previousHandStep[hand] = Vector3.zero;
        }

        private void UpdateInactivity(Vector3 playerRoot, Vector3 headPosition)
        {
            if (!_hasActivityAnchor)
            {
                _activityRootAnchor = playerRoot;
                _activityHeadAnchor = headPosition;
                _hasActivityAnchor = true;
                MarkMeaningfulActivity();
            }

            Vector3 rootDelta = playerRoot - _activityRootAnchor;
            rootDelta.y = 0f;
            if (rootDelta.magnitude >= activityRootDisplacement || Vector3.Distance(headPosition, _activityHeadAnchor) >= activityHeadDisplacement)
            {
                MarkMeaningfulActivity();
                _activityRootAnchor = playerRoot;
                _activityHeadAnchor = headPosition;
            }

            inactivitySeconds = Mathf.Max(0f, Time.time - _lastMeaningfulActivityTime);

            if (!_shortInactivityEmitted && inactivitySeconds >= inactivityShortSeconds)
            {
                _shortInactivityEmitted = true;
                EmitEvent("inactivity_short", "ambient", 1f);
            }

            if (!_longInactivityEmitted && inactivitySeconds >= inactivityLongSeconds)
            {
                _longInactivityEmitted = true;
                EmitEvent("inactivity_long", "ambient", 1f);
            }
        }

        private void MarkMeaningfulActivity()
        {
            _lastMeaningfulActivityTime = Time.time;
            inactivitySeconds = 0f;
            _shortInactivityEmitted = false;
            _longInactivityEmitted = false;
        }

        private void ResetMotionFilter()
        {
            _hasMotionFilter = false;
            filteredPlayerRadialSpeed = 0f;
            rawPlayerRadialSpeed = 0f;
            _motionCode = 0;
            _pendingMotionCode = 0;
            _pendingMotionSamples = 0;
            motion = "unknown";
        }

        private void ResetTransientClassification()
        {
            distanceBand = "unknown";
            facing = "unknown";
            motion = "unknown";
            _distanceBandCode = 0;
            _facingCode = 0;
            ResetMotionFilter();
            EndActiveHeadpat("tracking");
            ResetHeadpatCandidates();
        }

        private void EmitEvent(string type, string source, float confidence)
        {
            if (string.IsNullOrEmpty(type))
            {
                return;
            }

            eventSequence++;
            lastEventType = type;
            lastEventSource = source;
            lastEventConfidence = Mathf.Clamp01(confidence);

            if (Utilities.IsValid(eventReceiver) && !string.IsNullOrEmpty(eventReceiverMethod))
            {
                eventReceiver.SendCustomEvent(eventReceiverMethod);
            }
        }

        private string DistanceBandName(int code)
        {
            if (code == 1) return "contact";
            if (code == 2) return "near";
            if (code == 3) return "social";
            if (code == 4) return "far";
            return "unknown";
        }

        private string DistanceBandEvent(int code)
        {
            if (code == 1) return "entered_contact";
            if (code == 2) return "entered_near";
            if (code == 3) return "entered_social";
            if (code == 4) return "entered_far";
            return "";
        }

        private string HandSource(int hand)
        {
            return hand == 0 ? "left_hand" : "right_hand";
        }
    }
}
