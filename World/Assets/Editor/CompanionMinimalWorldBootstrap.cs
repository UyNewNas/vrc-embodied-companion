using System;
using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UyNewNas.VRCEmbodiedCompanion;
using VRC.SDK3.Components;

internal static class CompanionMinimalWorldBootstrap
{
    private const string ScenePath = "Assets/Scenes/CompanionMinimal.unity";
    private const string UnitySmokePassMarker = "VRC_COMPANION_UNITY_SMOKE_PASS:v1";

    [MenuItem("VRC Companion/Create or Reset Minimal Test World")]
    public static void CreateOrResetMinimalWorld()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject world = new GameObject("VRCWorld");
        VRCSceneDescriptor descriptor = world.AddComponent<VRCSceneDescriptor>();

        GameObject spawn = new GameObject("Spawn");
        spawn.transform.SetParent(world.transform, false);
        spawn.transform.position = new Vector3(0f, 0.05f, -2.5f);
        spawn.transform.rotation = Quaternion.identity;
        descriptor.spawns = new[] { spawn.transform };

        GameObject room = new GameObject("TestRoom");
        CreateBlock(room.transform, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f));
        CreateBlock(room.transform, "BackWall", new Vector3(0f, 1.5f, 4f), new Vector3(8f, 3f, 0.1f));
        CreateBlock(room.transform, "LeftWall", new Vector3(-4f, 1.5f, 0f), new Vector3(0.1f, 3f, 8f));
        CreateBlock(room.transform, "RightWall", new Vector3(4f, 1.5f, 0f), new Vector3(0.1f, 3f, 8f));

        GameObject companion = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        companion.name = "CompanionPlaceholder";
        companion.transform.position = new Vector3(0f, 1f, 1.5f);

        // Issue #4 logical lifecycle integration. Keep the lifecycle behaviour on the PlayerObject
        // root so Networking.GetOwner(gameObject) reads the ownership anchor VRChat assigns to that
        // PlayerObject instead of relying on ownership semantics of an unsynced child object.
        // Visual/audio presentation remains a separate runtime experiment (#11).
        GameObject playerObjectTemplate = new GameObject("CompanionPlayerObjectTemplate");
        playerObjectTemplate.AddComponent<VRCPlayerObject>();
        playerObjectTemplate.AddUdonSharpComponent<CompanionPlayerLifecycle>();

        // Development-only runtime evidence for #4/#12. The probe logs each restore/leave callback
        // and then emits a one-frame-delayed snapshot from the same PlayerObject root. It never owns
        // lifecycle decisions or synchronized state, and can be removed from production worlds.
        playerObjectTemplate.AddUdonSharpComponent<CompanionPlayerLifecycleDebugProbe>();

        GameObject runtimeServices = new GameObject("CompanionRuntime");
        runtimeServices.AddUdonSharpComponent<CompanionPlayerLookup>();

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new IOException("Unity failed to save " + ScenePath);
        }

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Created minimal VRChat companion test world at " + ScenePath + ". Runtime verification is still required via VRChat Build & Test.");
    }

    [MenuItem("VRC Companion/Create, Save, Reopen and Verify Minimal Test World")]
    public static void CreateSaveReopenAndVerifyMinimalWorld()
    {
        CreateOrResetMinimalWorld();
        VerifySerializedMinimalWorld();
    }

    [MenuItem("VRC Companion/Verify Serialized Minimal Test World")]
    public static void VerifySerializedMinimalWorld()
    {
        if (!File.Exists(ScenePath))
        {
            throw new FileNotFoundException("Minimal world scene does not exist. Run the bootstrap first.", ScenePath);
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("Unity could not reopen " + ScenePath);
        }

        GameObject world = GameObject.Find("VRCWorld");
        if (world == null)
        {
            throw new InvalidOperationException("Serialized scene is missing VRCWorld.");
        }

        VRCSceneDescriptor descriptor = world.GetComponent<VRCSceneDescriptor>();
        if (descriptor == null)
        {
            throw new InvalidOperationException("Serialized scene is missing VRCSceneDescriptor on VRCWorld.");
        }

        if (descriptor.spawns == null || descriptor.spawns.Length != 1 || descriptor.spawns[0] == null)
        {
            throw new InvalidOperationException("Serialized VRCSceneDescriptor must contain exactly one non-null spawn.");
        }

        Transform spawn = descriptor.spawns[0];
        if (spawn.name != "Spawn" || !spawn.IsChildOf(world.transform))
        {
            throw new InvalidOperationException("Serialized spawn must be the Spawn child of VRCWorld.");
        }

        if (GameObject.Find("CompanionPlaceholder") == null)
        {
            throw new InvalidOperationException("Serialized scene is missing CompanionPlaceholder.");
        }

        GameObject playerObjectTemplate = GameObject.Find("CompanionPlayerObjectTemplate");
        if (playerObjectTemplate == null)
        {
            throw new InvalidOperationException("Serialized scene is missing CompanionPlayerObjectTemplate.");
        }

        if (playerObjectTemplate.GetComponent<VRCPlayerObject>() == null)
        {
            throw new InvalidOperationException("CompanionPlayerObjectTemplate is missing VRCPlayerObject.");
        }

        if (playerObjectTemplate.GetComponent<CompanionPlayerLifecycle>() == null)
        {
            throw new InvalidOperationException("CompanionPlayerObjectTemplate is missing CompanionPlayerLifecycle on the PlayerObject root.");
        }

        CompanionPlayerLifecycleDebugProbe debugProbe = playerObjectTemplate.GetComponent<CompanionPlayerLifecycleDebugProbe>();
        if (debugProbe == null || !debugProbe.loggingEnabled)
        {
            throw new InvalidOperationException("CompanionPlayerObjectTemplate is missing the enabled lifecycle debug probe required by the acceptance world.");
        }

        GameObject runtimeServices = GameObject.Find("CompanionRuntime");
        if (runtimeServices == null || runtimeServices.GetComponent<CompanionPlayerLookup>() == null)
        {
            throw new InvalidOperationException("Serialized scene is missing CompanionRuntime with CompanionPlayerLookup.");
        }

        bool buildSceneEnabled = false;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (buildScene.enabled && buildScene.path == ScenePath)
            {
                buildSceneEnabled = true;
                break;
            }
        }

        if (!buildSceneEnabled)
        {
            throw new InvalidOperationException(ScenePath + " is not enabled in EditorBuildSettings.");
        }

        // Keep a short machine-readable marker stable even as the human diagnostics below evolve.
        // Invoke-UnitySmoke.ps1 keys off this token so adding new verifier assertions cannot silently
        // turn a real successful Unity run into a false-negative evidence result.
        Debug.Log(UnitySmokePassMarker);
        Debug.Log("Verified serialized minimal VRChat companion scene: descriptor, spawn, placeholder, PlayerObject lifecycle template, enabled evidence probe, lookup service, and build-scene entry survived save/reopen. SDK validation and VRChat Build & Test are still required.");
    }

    private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.position = position;
        block.transform.localScale = scale;
        return block;
    }
}
