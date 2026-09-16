using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Minimal runtime probe for the bounded transport route table.
    ///
    /// It proves the request discipline required by issue #18 once a runnable world
    /// exists: one request in flight, a conservative >=6 second start cadence, timeout
    /// recovery, callback URL matching, and fail-closed BehaviorPlan validation.
    ///
    /// It intentionally stops before fallback selection or behavior execution.
    /// Unity/UdonSharp compilation and VRChat runtime behavior remain unverified until
    /// issue #2 provides a runnable VCC World project.
    /// </summary>
    public class CompanionTransportProbe : UdonSharpBehaviour
    {
        private const int ExpectedRouteCount = 288;
        private const float PlatformCadenceFloorSeconds = 6f;

        [Header("Route table")]
        public CompanionTransportRouteBinding routeBinding;

        [Header("Response validation")]
        public CompanionBehaviorPlanValidator behaviorPlanValidator;

        [Header("Request discipline")]
        [Tooltip("Clamped to at least 6 seconds to stay above VRChat's documented 5 second string-download limit.")]
        public float minimumRequestIntervalSeconds = PlatformCadenceFloorSeconds;

        [Tooltip("Local timeout only. A later callback is treated as stale and ignored.")]
        public float requestTimeoutSeconds = 20f;

        [Header("Debug state - runtime evidence only")]
        public bool requestInFlight;
        public int inFlightRouteIndex = -1;
        public bool inFlightUsesStaticRoute;
        public string inFlightUrl = "";
        public float nextAllowedRequestAt;
        public int acceptedCallbackCount;
        public int staleCallbackCount;
        public int timeoutCount;
        public int rejectedStartCount;
        public int validPlanCount;
        public int rejectedPlanCount;
        public int lastAcceptedRouteIndex = -1;
        public bool lastAcceptedUsedStaticRoute;
        public string lastAcceptedUrl = "";
        public string lastAcceptedPayload = "";
        public int lastErrorCode;
        public string lastError = "";
        public string lastRejectedReason = "";
        public string lastPlanRejectReason = "";
        public string lastIgnoredCallbackUrl = "";

        private float inFlightStartedAt;

        public bool TryRequestLiveRoute(int routeIndex)
        {
            return TryStartRequest(routeIndex, false);
        }

        public bool TryRequestStaticRoute(int routeIndex)
        {
            return TryStartRequest(routeIndex, true);
        }

        private void Update()
        {
            if (!requestInFlight)
            {
                return;
            }

            float timeout = Mathf.Max(1f, requestTimeoutSeconds);
            if (Time.time - inFlightStartedAt < timeout)
            {
                return;
            }

            timeoutCount++;
            lastErrorCode = 0;
            lastError = "timeout";
            ClearInFlightState();
        }

        private bool TryStartRequest(int routeIndex, bool useStaticRoute)
        {
            if (requestInFlight)
            {
                return RejectStart("request_in_flight");
            }

            float now = Time.time;
            if (now < nextAllowedRequestAt)
            {
                return RejectStart("cadence_gate");
            }

            if (!BindingIsReady())
            {
                return RejectStart("route_binding_not_ready");
            }

            if (routeIndex < 0 || routeIndex >= routeBinding.routeCount)
            {
                return RejectStart("route_index_out_of_range");
            }

            VRCUrl[] urls = useStaticRoute ? routeBinding.staticUrls : routeBinding.liveUrls;
            VRCUrl targetUrl = urls[routeIndex];
            if (VRCUrl.IsNullOrEmpty(targetUrl))
            {
                return RejectStart("route_url_empty");
            }

            string targetUrlString = targetUrl.Get();
            if (string.IsNullOrEmpty(targetUrlString))
            {
                return RejectStart("route_url_empty");
            }

            requestInFlight = true;
            inFlightRouteIndex = routeIndex;
            inFlightUsesStaticRoute = useStaticRoute;
            inFlightUrl = targetUrlString;
            inFlightStartedAt = now;
            nextAllowedRequestAt = now + Mathf.Max(PlatformCadenceFloorSeconds, minimumRequestIntervalSeconds);
            lastRejectedReason = "";
            lastPlanRejectReason = "";
            lastErrorCode = 0;
            lastError = "";

            VRCStringDownloader.LoadUrl(targetUrl, this);
            return true;
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            string callbackUrl = ReadCallbackUrl(result);
            if (!CallbackMatchesCurrentRequest(callbackUrl))
            {
                RecordStaleCallback(callbackUrl);
                return;
            }

            acceptedCallbackCount++;
            lastAcceptedRouteIndex = inFlightRouteIndex;
            lastAcceptedUsedStaticRoute = inFlightUsesStaticRoute;
            lastAcceptedUrl = callbackUrl;
            lastErrorCode = 0;

            string payload = result.Result;
            if (behaviorPlanValidator == null)
            {
                rejectedPlanCount++;
                lastAcceptedPayload = "";
                lastPlanRejectReason = "validator_missing";
                lastError = "behavior_plan_invalid";
                ClearInFlightState();
                return;
            }

            if (!behaviorPlanValidator.TryValidate(payload))
            {
                rejectedPlanCount++;
                lastAcceptedPayload = "";
                lastPlanRejectReason = behaviorPlanValidator.lastRejectReason;
                lastError = "behavior_plan_invalid";
                ClearInFlightState();
                return;
            }

            validPlanCount++;
            lastAcceptedPayload = payload;
            lastPlanRejectReason = "";
            lastError = "";
            ClearInFlightState();
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            string callbackUrl = ReadCallbackUrl(result);
            if (!CallbackMatchesCurrentRequest(callbackUrl))
            {
                RecordStaleCallback(callbackUrl);
                return;
            }

            acceptedCallbackCount++;
            lastAcceptedRouteIndex = inFlightRouteIndex;
            lastAcceptedUsedStaticRoute = inFlightUsesStaticRoute;
            lastAcceptedUrl = callbackUrl;
            lastAcceptedPayload = "";
            lastPlanRejectReason = "";
            lastErrorCode = result.ErrorCode;
            lastError = result.Error;
            ClearInFlightState();
        }

        private bool BindingIsReady()
        {
            if (routeBinding == null)
            {
                return false;
            }

            if (routeBinding.routeCount != ExpectedRouteCount)
            {
                return false;
            }

            if (routeBinding.liveUrls == null || routeBinding.staticUrls == null)
            {
                return false;
            }

            return routeBinding.liveUrls.Length == ExpectedRouteCount
                && routeBinding.staticUrls.Length == ExpectedRouteCount;
        }

        private string ReadCallbackUrl(IVRCStringDownload result)
        {
            if (result == null || VRCUrl.IsNullOrEmpty(result.Url))
            {
                return "";
            }

            return result.Url.Get();
        }

        private bool CallbackMatchesCurrentRequest(string callbackUrl)
        {
            if (!requestInFlight)
            {
                return false;
            }

            if (string.IsNullOrEmpty(callbackUrl) || string.IsNullOrEmpty(inFlightUrl))
            {
                return false;
            }

            return callbackUrl == inFlightUrl;
        }

        private void RecordStaleCallback(string callbackUrl)
        {
            staleCallbackCount++;
            lastIgnoredCallbackUrl = callbackUrl;
        }

        private bool RejectStart(string reason)
        {
            rejectedStartCount++;
            lastRejectedReason = reason;
            return false;
        }

        private void ClearInFlightState()
        {
            requestInFlight = false;
            inFlightRouteIndex = -1;
            inFlightUsesStaticRoute = false;
            inFlightUrl = "";
            inFlightStartedAt = 0f;
        }
    }
}
