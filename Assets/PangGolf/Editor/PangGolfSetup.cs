using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PangGolf.EditorTools
{
    /// <summary>
    /// One-time project setup: linear colour space, shaders needed by the
    /// procedurally built world, and a ready-to-play scene in the build list.
    /// Runs automatically the first time the project is opened, or via the menu.
    /// </summary>
    [InitializeOnLoad]
    public static class PangGolfSetup
    {
        const string ScenePath = "Assets/PangGolf/Scenes/PangGolf.unity";

        static readonly string[] Shaders =
        {
            "Standard",
            "Sprites/Default",
            "Unlit/Color",
            "Skybox/Procedural",
            "Particles/Standard Unlit",
            "Legacy Shaders/Particles/Alpha Blended",
            "Nature/Terrain/Standard",
            "Hidden/TerrainEngine/Splatmap/Standard-AddPass",
            "Hidden/TerrainEngine/Splatmap/Standard-Base",
            "Hidden/TerrainEngine/Splatmap/Standard-BaseGen",
            "Hidden/TerrainEngine/Details/BillboardWavingDoublePass",
            "Hidden/TerrainEngine/Details/WavingDoublePass",
            "Hidden/TerrainEngine/Details/Vertexlit",
        };

        static PangGolfSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(ScenePath)) return;
                Setup();
            };
        }

        [MenuItem("PangGolf/Setup Project")]
        public static void Setup()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            IncludeShaders();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("PangGolf").AddComponent<GolfGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("PangGolf: project set up. Open " + ScenePath + " and press Play.");
        }

        [MenuItem("PangGolf/Open Game Scene")]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath)) Setup();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        static void IncludeShaders()
        {
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            foreach (var name in Shaders)
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                bool found = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { found = true; break; }
                if (found) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedProperties();
        }
    }
}
