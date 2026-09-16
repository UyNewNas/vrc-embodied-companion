using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class CompanionMinimalWorldBootstrap
{
    private const string ScenePath = "Assets/Scenes/CompanionMinimal.unity";
    private const string SceneDescriptorTypeName = "VRC.SDK3.Components.VRCSceneDescriptor";

    [MenuItem("VRC Companion/Create or Reset Minimal Test World")]
    public static void CreateOrResetMinimalWorld()
    {
        Type descriptorType = FindType(SceneDescriptorTypeName);
        if (descriptorType == null)
        {
            throw new InvalidOperationException(
                "VRChat Worlds SDK is not resolved. Open this project through VRChat Creator Companion, " +
                "let VPM dependencies finish resolving, then run this command again.");
        }

        if (!typeof(Component).IsAssignableFrom(descriptorType))
        {
            throw new InvalidOperationException(SceneDescriptorTypeName + " is not a Unity Component.");
        }

        Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject world = new GameObject("VRCWorld");
        Component descriptor = world.AddComponent(descriptorType);

        GameObject spawn = new GameObject("Spawn");
        spawn.transform.SetParent(world.transform, false);
        spawn.transform.position = new Vector3(0f, 0.05f, -2.5f);
        spawn.transform.rotation = Quaternion.identity;

        FieldInfo spawnsField = descriptorType.GetField("spawns", BindingFlags.Instance | BindingFlags.Public);
        if (spawnsField == null || spawnsField.FieldType != typeof(Transform[]))
        {
            throw new MissingFieldException(SceneDescriptorTypeName, "spawns");
        }
        spawnsField.SetValue(descriptor, new[] { spawn.transform });

        GameObject room = new GameObject("TestRoom");
        CreateBlock(room.transform, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f));
        CreateBlock(room.transform, "BackWall", new Vector3(0f, 1.5f, 4f), new Vector3(8f, 3f, 0.1f));
        CreateBlock(room.transform, "LeftWall", new Vector3(-4f, 1.5f, 0f), new Vector3(0.1f, 3f, 8f));
        CreateBlock(room.transform, "RightWall", new Vector3(4f, 1.5f, 0f), new Vector3(0.1f, 3f, 8f));

        GameObject companion = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        companion.name = "CompanionPlaceholder";
        companion.transform.position = new Vector3(0f, 1f, 1.5f);

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

    private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.position = position;
        block.transform.localScale = scale;
        return block;
    }

    private static Type FindType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }
        return null;
    }
}
