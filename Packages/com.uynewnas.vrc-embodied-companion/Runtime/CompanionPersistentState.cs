using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Uncompiled v1 prototype for issue #7.
    ///
    /// Intended placement:
    /// - under a VRCPlayerObject;
    /// - with VRCEnablePersistence on the PlayerObject;
    /// - manual synchronization.
    ///
    /// This adapter persists only bounded low-sensitivity preferences. It deliberately has no
    /// free-form episodic/transcript field.
    ///
    /// Runtime verification remains blocked on issue #2.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class CompanionPersistentState : UdonSharpBehaviour
    {
        public const int CurrentSchemaVersion = 1;

        public const int ComfortUnset = 0;
        public const int ComfortQuiet = 1;
        public const int ComfortGentlePrompt = 2;
        public const int ComfortTalkative = 3;

        public const int TouchAsk = 0;
        public const int TouchAvoid = 1;
        public const int TouchHeadpatOk = 2;
        public const int TouchCloseContactOk = 3;

        public const int VerbosityBrief = 0;
        public const int VerbosityNormal = 1;
        public const int VerbosityVerbose = 2;

        private const float DefaultDistanceM = 1.10f;
        private const float MinimumDistanceM = 0.40f;
        private const float MaximumDistanceM = 3.00f;

        [Header("Persistent PlayerObject fields")]
        [UdonSynced] private int persistentSchemaVersion;
        [UdonSynced] private int persistentResetEpoch;
        [UdonSynced] private bool persistentMemoryEnabled;
        [UdonSynced] private int persistentComfortStyle;
        [UdonSynced] private int persistentTouchMode;
        [UdonSynced] private float persistentPreferredDistanceM = DefaultDistanceM;
        [UdonSynced] private int persistentResponseVerbosity = VerbosityNormal;
        [UdonSynced] private string persistentLanguageCode = "auto";

        [Header("Session view used by the companion")]
        public int comfortStyle = ComfortUnset;
        public int touchMode = TouchAsk;
        public float preferredDistanceM = DefaultDistanceM;
        public int responseVerbosity = VerbosityNormal;
        public string languageCode = "auto";

        [Header("Lifecycle / debug")]
        public bool restored;
        public bool localOwnerReady;
        public bool durableMemoryEnabled;
        public bool futureSchemaReadOnly;
        public int schemaVersion;
        public int resetEpoch;
        public int serializationRequests;
        public string lastPersistenceAction = "bootstrap";

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            if (!Utilities.IsValid(owner) || !Utilities.IsValid(player))
            {
                return;
            }

            if (owner.playerId != player.playerId)
            {
                return;
            }

            restored = true;

            // The native MVP consumes durable preferences only on the PlayerObject owned by the
            // local player. Remote/social presentation is a separate experiment (#11/#14).
            if (!IsLocalOwner())
            {
                localOwnerReady = false;
                lastPersistenceAction = "remote_owner_restored";
                return;
            }

            localOwnerReady = true;
            schemaVersion = persistentSchemaVersion;
            resetEpoch = persistentResetEpoch;

            if (persistentSchemaVersion > CurrentSchemaVersion)
            {
                futureSchemaReadOnly = true;
                durableMemoryEnabled = false;
                ApplyNeutralSessionDefaults();
                lastPersistenceAction = "future_schema_read_only";
                return;
            }

            futureSchemaReadOnly = false;

            if (persistentSchemaVersion == CurrentSchemaVersion && persistentMemoryEnabled)
            {
                LoadPersistentPreferencesIntoSession();
                durableMemoryEnabled = true;
                lastPersistenceAction = "v1_preferences_restored";
                return;
            }

            // schemaVersion == 0 is intentionally not auto-promoted. Joining the world alone must
            // not create a durable-memory record.
            durableMemoryEnabled = false;
            ApplyNeutralSessionDefaults();
            lastPersistenceAction = persistentSchemaVersion == 0
                ? "no_durable_record"
                : "durable_memory_disabled";
        }

        public void SetSessionComfortStyle(int value)
        {
            comfortStyle = ClampInt(value, ComfortUnset, ComfortTalkative);
        }

        public void SetSessionTouchMode(int value)
        {
            touchMode = ClampInt(value, TouchAsk, TouchCloseContactOk);
        }

        public void SetSessionPreferredDistance(float value)
        {
            preferredDistanceM = Mathf.Clamp(value, MinimumDistanceM, MaximumDistanceM);
        }

        public void SetSessionResponseVerbosity(int value)
        {
            responseVerbosity = ClampInt(value, VerbosityBrief, VerbosityVerbose);
        }

        public void SetSessionLanguageCode(string value)
        {
            languageCode = NormalizeLanguageCode(value);
        }

        /// <summary>
        /// Explicitly activates durable low-sensitivity preferences and serializes the current
        /// bounded session preferences as the first v1 record.
        /// </summary>
        public void EnableDurableMemory()
        {
            if (!CanWritePersistentState())
            {
                lastPersistenceAction = "enable_rejected_not_ready";
                return;
            }

            if (futureSchemaReadOnly)
            {
                lastPersistenceAction = "enable_rejected_future_schema";
                return;
            }

            durableMemoryEnabled = true;
            persistentMemoryEnabled = true;
            persistentSchemaVersion = CurrentSchemaVersion;
            schemaVersion = CurrentSchemaVersion;

            CopySessionPreferencesToPersistent();
            SerializePersistentState("durable_memory_enabled");
        }

        /// <summary>
        /// Persists the current bounded session preferences if durable memory is enabled.
        /// UI/integration code can batch several setters and call this once.
        /// </summary>
        public void CommitSessionPreferences()
        {
            if (!CanWritePersistentState())
            {
                lastPersistenceAction = "commit_rejected_not_ready";
                return;
            }

            if (futureSchemaReadOnly)
            {
                lastPersistenceAction = "commit_rejected_future_schema";
                return;
            }

            if (!durableMemoryEnabled || !persistentMemoryEnabled)
            {
                lastPersistenceAction = "commit_skipped_memory_disabled";
                return;
            }

            CopySessionPreferencesToPersistent();
            SerializePersistentState("preferences_committed");
        }

        /// <summary>
        /// Product-level "forget/reset": overwrite every meaningful durable preference with its
        /// neutral default and disable durable writes. This does not claim physical deletion of
        /// VRChat's backend record.
        /// </summary>
        public void ResetPersistentMemory()
        {
            if (!CanWritePersistentState())
            {
                lastPersistenceAction = "reset_rejected_not_ready";
                return;
            }

            if (futureSchemaReadOnly)
            {
                // Old package code must never overwrite a record written by a newer schema.
                lastPersistenceAction = "reset_rejected_future_schema";
                return;
            }

            persistentResetEpoch++;
            resetEpoch = persistentResetEpoch;
            persistentSchemaVersion = CurrentSchemaVersion;
            schemaVersion = CurrentSchemaVersion;
            persistentMemoryEnabled = false;
            durableMemoryEnabled = false;

            persistentComfortStyle = ComfortUnset;
            persistentTouchMode = TouchAsk;
            persistentPreferredDistanceM = DefaultDistanceM;
            persistentResponseVerbosity = VerbosityNormal;
            persistentLanguageCode = "auto";

            ApplyNeutralSessionDefaults();
            SerializePersistentState("memory_reset_to_neutral");
        }

        public bool CanAcceptDurableMemoryProposal()
        {
            return CanWritePersistentState()
                && !futureSchemaReadOnly
                && durableMemoryEnabled
                && persistentMemoryEnabled;
        }

        private bool CanWritePersistentState()
        {
            return restored && localOwnerReady && IsLocalOwner();
        }

        private bool IsLocalOwner()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            return Utilities.IsValid(localPlayer)
                && Networking.IsOwner(localPlayer, gameObject);
        }

        private void LoadPersistentPreferencesIntoSession()
        {
            comfortStyle = ClampInt(persistentComfortStyle, ComfortUnset, ComfortTalkative);
            touchMode = ClampInt(persistentTouchMode, TouchAsk, TouchCloseContactOk);
            preferredDistanceM = Mathf.Clamp(persistentPreferredDistanceM, MinimumDistanceM, MaximumDistanceM);
            responseVerbosity = ClampInt(persistentResponseVerbosity, VerbosityBrief, VerbosityVerbose);
            languageCode = NormalizeLanguageCode(persistentLanguageCode);
        }

        private void CopySessionPreferencesToPersistent()
        {
            persistentComfortStyle = ClampInt(comfortStyle, ComfortUnset, ComfortTalkative);
            persistentTouchMode = ClampInt(touchMode, TouchAsk, TouchCloseContactOk);
            persistentPreferredDistanceM = Mathf.Clamp(preferredDistanceM, MinimumDistanceM, MaximumDistanceM);
            persistentResponseVerbosity = ClampInt(responseVerbosity, VerbosityBrief, VerbosityVerbose);
            persistentLanguageCode = NormalizeLanguageCode(languageCode);

            // Keep the session view normalized as well so presentation and durable state cannot
            // silently diverge after an out-of-range proposal.
            LoadPersistentPreferencesIntoSession();
        }

        private void ApplyNeutralSessionDefaults()
        {
            comfortStyle = ComfortUnset;
            touchMode = TouchAsk;
            preferredDistanceM = DefaultDistanceM;
            responseVerbosity = VerbosityNormal;
            languageCode = "auto";
        }

        private void SerializePersistentState(string reason)
        {
            serializationRequests++;
            lastPersistenceAction = reason;
            RequestSerialization();
        }

        private int ClampInt(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private string NormalizeLanguageCode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "auto";
            }

            if (value.Length > 16)
            {
                return value.Substring(0, 16);
            }

            return value;
        }
    }
}
