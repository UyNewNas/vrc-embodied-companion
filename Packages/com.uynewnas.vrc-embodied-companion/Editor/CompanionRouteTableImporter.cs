using System;
using System.IO;
using UnityEngine;
using VRC.SDKBase;

namespace UyNewNas.VrcEmbodiedCompanion.Editor
{
    /// <summary>
    /// Editor-only compiler for the reviewed unity-route-table.v0.1 artifact.
    ///
    /// This class deliberately constructs VRCUrl values only in editor code.
    /// The resulting arrays are intended to be copied/serialized onto a world-side
    /// UdonSharp transport component once #2 provides a runnable World project.
    /// </summary>
    public static class CompanionRouteTableImporter
    {
        public const string SupportedSchemaVersion = "0.1";
        public const string SupportedRouteIndexVersion = "0.1";
        public const int ExpectedRouteCount = 288;

        private const int IntentStride = 48;
        private const int RelationStride = 16;
        private const int ComfortStride = 8;
        private const int LanguageStride = 4;

        private static readonly string[] IntentOrder =
        {
            "goodbye",
            "greet",
            "offer_hug",
            "quiet_company",
            "talk_light",
            "walk_with_me",
        };

        private static readonly string[] RelationOrder =
        {
            "familiar",
            "new",
            "warm",
        };

        private static readonly string[] ComfortOrder =
        {
            "neutral",
            "quiet",
        };

        private static readonly string[] LanguageOrder =
        {
            "en",
            "zh",
        };

        [Serializable]
        private sealed class RouteTableJson
        {
            public string schema_version;
            public string route_index_version;
            public string persona_id;
            public int route_count;
            public string[] route_keys;
            public string[] live_relative_paths;
            public string[] static_relative_paths;
        }

        public sealed class CompiledRouteTable
        {
            public string PersonaId { get; }
            public string[] RouteKeys { get; }
            public VRCUrl[] LiveUrls { get; }
            public VRCUrl[] StaticUrls { get; }

            internal CompiledRouteTable(
                string personaId,
                string[] routeKeys,
                VRCUrl[] liveUrls,
                VRCUrl[] staticUrls)
            {
                PersonaId = personaId;
                RouteKeys = routeKeys;
                LiveUrls = liveUrls;
                StaticUrls = staticUrls;
            }
        }

        public static CompiledRouteTable Compile(
            TextAsset routeTableAsset,
            string liveBaseUrl,
            string staticBaseUrl)
        {
            if (routeTableAsset == null)
            {
                throw new ArgumentNullException(nameof(routeTableAsset));
            }

            return Compile(routeTableAsset.text, liveBaseUrl, staticBaseUrl);
        }

        public static CompiledRouteTable Compile(
            string routeTableJson,
            string liveBaseUrl,
            string staticBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(routeTableJson))
            {
                throw new InvalidDataException("Route table JSON must not be empty.");
            }

            RouteTableJson table;
            try
            {
                table = JsonUtility.FromJson<RouteTableJson>(routeTableJson);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Route table JSON could not be parsed.", exception);
            }

            ValidateTable(table);

            Uri liveBase = ValidateBaseUrl(liveBaseUrl, nameof(liveBaseUrl));
            Uri staticBase = ValidateBaseUrl(staticBaseUrl, nameof(staticBaseUrl));

            string[] routeKeys = new string[ExpectedRouteCount];
            VRCUrl[] liveUrls = new VRCUrl[ExpectedRouteCount];
            VRCUrl[] staticUrls = new VRCUrl[ExpectedRouteCount];

            for (int routeIndex = 0; routeIndex < ExpectedRouteCount; routeIndex++)
            {
                ExpectedRoute expected = GetExpectedRoute(routeIndex, table.persona_id);

                RequireEqual(
                    table.route_keys[routeIndex],
                    expected.RouteKey,
                    routeIndex,
                    "route_key");
                RequireEqual(
                    table.live_relative_paths[routeIndex],
                    expected.LiveRelativePath,
                    routeIndex,
                    "live_relative_path");
                RequireEqual(
                    table.static_relative_paths[routeIndex],
                    expected.StaticRelativePath,
                    routeIndex,
                    "static_relative_path");

                string liveAbsoluteUrl = new Uri(liveBase, expected.LiveRelativePath).AbsoluteUri;
                string staticAbsoluteUrl = new Uri(staticBase, expected.StaticRelativePath).AbsoluteUri;

                // VRCUrl(string) is intentionally called only from this Editor-folder class.
                routeKeys[routeIndex] = expected.RouteKey;
                liveUrls[routeIndex] = new VRCUrl(liveAbsoluteUrl);
                staticUrls[routeIndex] = new VRCUrl(staticAbsoluteUrl);
            }

            return new CompiledRouteTable(table.persona_id, routeKeys, liveUrls, staticUrls);
        }

        private static void ValidateTable(RouteTableJson table)
        {
            if (table == null)
            {
                throw new InvalidDataException("Route table JSON produced no object.");
            }

            if (!string.Equals(table.schema_version, SupportedSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Unsupported route-table schema_version '{table.schema_version ?? "<null>"}'.");
            }

            if (!string.Equals(
                    table.route_index_version,
                    SupportedRouteIndexVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Unsupported route_index_version '{table.route_index_version ?? "<null>"}'.");
            }

            if (!IsValidPersonaId(table.persona_id))
            {
                throw new InvalidDataException("persona_id is outside the v0.1 bounded identifier grammar.");
            }

            if (table.route_count != ExpectedRouteCount)
            {
                throw new InvalidDataException(
                    $"route_count must be {ExpectedRouteCount}, got {table.route_count}.");
            }

            RequireArrayLength(table.route_keys, nameof(table.route_keys));
            RequireArrayLength(table.live_relative_paths, nameof(table.live_relative_paths));
            RequireArrayLength(table.static_relative_paths, nameof(table.static_relative_paths));
        }

        private static Uri ValidateBaseUrl(string baseUrl, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL must not be empty.", parameterName);
            }

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri parsed))
            {
                throw new ArgumentException("Base URL must be absolute.", parameterName);
            }

            if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Base URL must use HTTPS.", parameterName);
            }

            if (string.IsNullOrEmpty(parsed.Host))
            {
                throw new ArgumentException("Base URL must include a host.", parameterName);
            }

            if (!string.IsNullOrEmpty(parsed.UserInfo))
            {
                throw new ArgumentException("Base URL must not contain user info.", parameterName);
            }

            if (!string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment))
            {
                throw new ArgumentException("Base URL must not contain query or fragment data.", parameterName);
            }

            string normalized = parsed.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
                ? parsed.AbsoluteUri
                : parsed.AbsoluteUri + "/";

            return new Uri(normalized, UriKind.Absolute);
        }

        private static bool IsValidPersonaId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return false;
            }

            if (value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool allowed =
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '_' ||
                    character == '-';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RequireArrayLength(string[] values, string fieldName)
        {
            if (values == null || values.Length != ExpectedRouteCount)
            {
                int actual = values == null ? -1 : values.Length;
                throw new InvalidDataException(
                    $"{fieldName} must contain exactly {ExpectedRouteCount} entries; got {actual}.");
            }
        }

        private static void RequireEqual(
            string actual,
            string expected,
            int routeIndex,
            string fieldName)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"{fieldName} drift at route_index {routeIndex}: expected '{expected}', got '{actual ?? "<null>"}'.");
            }
        }

        private static ExpectedRoute GetExpectedRoute(int routeIndex, string personaId)
        {
            if (routeIndex < 0 || routeIndex >= ExpectedRouteCount)
            {
                throw new ArgumentOutOfRangeException(nameof(routeIndex));
            }

            int remainder = routeIndex;
            int intentIndex = remainder / IntentStride;
            remainder %= IntentStride;
            int relationIndex = remainder / RelationStride;
            remainder %= RelationStride;
            int comfortIndex = remainder / ComfortStride;
            remainder %= ComfortStride;
            int languageIndex = remainder / LanguageStride;
            int turnSlot = remainder % LanguageStride;

            string routeKey = string.Join(
                "/",
                personaId,
                IntentOrder[intentIndex],
                RelationOrder[relationIndex],
                ComfortOrder[comfortIndex],
                LanguageOrder[languageIndex],
                turnSlot.ToString());

            string liveRelativePath = "v1/plan/" + routeKey;
            return new ExpectedRoute(
                routeKey,
                liveRelativePath,
                liveRelativePath + ".json");
        }

        private sealed class ExpectedRoute
        {
            public string RouteKey { get; }
            public string LiveRelativePath { get; }
            public string StaticRelativePath { get; }

            public ExpectedRoute(
                string routeKey,
                string liveRelativePath,
                string staticRelativePath)
            {
                RouteKey = routeKey;
                LiveRelativePath = liveRelativePath;
                StaticRelativePath = staticRelativePath;
            }
        }
    }
}
