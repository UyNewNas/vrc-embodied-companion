using System;
using UnityEditor;
using VRC.SDKBase;
using UyNewNas.VRCEmbodiedCompanion;

namespace UyNewNas.VrcEmbodiedCompanion.Editor
{
    /// <summary>
    /// Applies an already validated editor-time route table to the minimal UdonSharp
    /// serialization boundary used by the future world transport component.
    ///
    /// This does not perform network I/O. It only copies immutable VRCUrl lookup arrays
    /// onto a scene/prefab component so #18 can later exercise VRCStringDownloader.
    /// </summary>
    public static class CompanionRouteTableBinder
    {
        public static CompanionRouteTableImporter.CompiledRouteTable CompileAndApply(
            UnityEngine.TextAsset routeTableAsset,
            string liveBaseUrl,
            string staticBaseUrl,
            CompanionTransportRouteBinding target)
        {
            CompanionRouteTableImporter.CompiledRouteTable compiled =
                CompanionRouteTableImporter.Compile(routeTableAsset, liveBaseUrl, staticBaseUrl);
            Apply(compiled, target);
            return compiled;
        }

        public static void Apply(
            CompanionRouteTableImporter.CompiledRouteTable compiled,
            CompanionTransportRouteBinding target)
        {
            if (compiled == null)
            {
                throw new ArgumentNullException(nameof(compiled));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            ValidateCompiledArray(compiled.LiveUrls, nameof(compiled.LiveUrls));
            ValidateCompiledArray(compiled.StaticUrls, nameof(compiled.StaticUrls));

            Undo.RecordObject(target, "Apply Companion Transport Route Table");

            target.routeTableSchemaVersion = CompanionRouteTableImporter.SupportedSchemaVersion;
            target.routeIndexVersion = CompanionRouteTableImporter.SupportedRouteIndexVersion;
            target.personaId = compiled.PersonaId;
            target.routeCount = CompanionRouteTableImporter.ExpectedRouteCount;
            target.liveUrls = CloneUrls(compiled.LiveUrls);
            target.staticUrls = CloneUrls(compiled.StaticUrls);

            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }

        private static void ValidateCompiledArray(VRCUrl[] urls, string fieldName)
        {
            if (urls == null || urls.Length != CompanionRouteTableImporter.ExpectedRouteCount)
            {
                int actual = urls == null ? -1 : urls.Length;
                throw new InvalidOperationException(
                    $"{fieldName} must contain exactly " +
                    $"{CompanionRouteTableImporter.ExpectedRouteCount} entries; got {actual}.");
            }
        }

        private static VRCUrl[] CloneUrls(VRCUrl[] source)
        {
            VRCUrl[] clone = new VRCUrl[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }
    }
}
