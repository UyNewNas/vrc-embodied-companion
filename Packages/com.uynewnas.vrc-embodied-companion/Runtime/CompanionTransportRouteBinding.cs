using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace UyNewNas.VRCEmbodiedCompanion
{
    /// <summary>
    /// Serialized world-side handoff for the bounded v0.1 transport route table.
    ///
    /// The Editor importer owns population of these fields. Runtime code should treat
    /// the two URL arrays as immutable lookup tables whose array index is route_index.
    /// No downloader or model-provider logic belongs in this binding component.
    ///
    /// Unity/UdonSharp compilation and VRChat runtime behavior remain unverified until
    /// issue #2 provides a runnable VCC World project.
    /// </summary>
    public class CompanionTransportRouteBinding : UdonSharpBehaviour
    {
        [Header("Generated transport route table - do not hand edit")]
        public string routeTableSchemaVersion;
        public string routeIndexVersion;
        public string personaId;
        public int routeCount;
        public VRCUrl[] liveUrls;
        public VRCUrl[] staticUrls;
    }
}
