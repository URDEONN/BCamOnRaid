using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BodycamRaid.Editor
{
    /// <summary>Creates only missing project assets. Existing scenes are never replaced.</summary>
    [InitializeOnLoad]
    public static class BodycamSetup
    {
        private const string ScenePath = "Assets/Bodycam/Scenes/Training.unity";
        private const string SettingsFolder = "Assets/Bodycam/Settings";
        static BodycamSetup() { EditorApplication.delayCall += FirstOpen; }

        private static void FirstOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath)) return;
            CreateTrainingScene();
        }

        [MenuItem("Bodycam/Crear escena de entrenamiento")]
        public static void CreateTrainingScene()
        {
            if (EditorApplication.isPlaying) return;
            if (File.Exists(ScenePath)) { OpenTraining(); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Bodycam/Scenes");
            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.Refresh();
            ConfigurePipeline();
            ConfigureInput();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = new GameObject("Bodycam - Training").AddComponent<RaidWorld>();
            world.concrete = Palette("Concrete", new Color(.38f, .40f, .40f));
            world.darkConcrete = Palette("DarkConcrete", new Color(.15f, .18f, .19f));
            world.paint = Palette("Paint", new Color(.62f, .65f, .61f));
            world.yellow = Palette("SafetyYellow", new Color(.89f, .63f, .12f));
            world.metal = Palette("Metal", new Color(.12f, .15f, .17f), .7f);
            world.wood = Palette("Wood", new Color(.38f, .27f, .16f));
            world.cyan = Palette("Cyan", new Color(.10f, .52f, .61f));
            world.orange = Palette("Orange", new Color(.85f, .30f, .10f));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Independent";
            PlayerSettings.productName = "Bodycam Raid - Prototipo";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = false;
            QualitySettings.vSyncCount = 1;
            AssetDatabase.SaveAssets();
            Debug.Log("Bodycam listo. Abre Game y pulsa Play. Controles e instrucciones en LEEME.txt.");
        }

        [MenuItem("Bodycam/Abrir entrenamiento")]
        public static void OpenTraining()
        {
            if (EditorApplication.isPlaying) return;
            if (!File.Exists(ScenePath)) { CreateTrainingScene(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        private static void ConfigureInput()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0) return;
            var serialized = new SerializedObject(settings[0]);
            var input = serialized.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void ConfigurePipeline()
        {
            const string rendererPath = SettingsFolder + "/BodycamRenderer.asset";
            const string pipelinePath = SettingsFolder + "/BodycamPipeline.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "BodycamRenderer";
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                // Official factory initializes the default renderer and reloads package shader resources.
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "BodycamPipeline";
                var serialized = new SerializedObject(pipeline);
                var renderers = serialized.FindProperty("m_RendererDataList");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                var lightingMode = serialized.FindProperty("m_AdditionalLightsRenderingMode");
                if (lightingMode != null) lightingMode.intValue = (int)LightRenderingMode.PerPixel;
                var additionalShadows = serialized.FindProperty("m_AdditionalLightShadowsSupported");
                if (additionalShadows != null) additionalShadows.boolValue = true;
                var mainShadows = serialized.FindProperty("m_MainLightShadowsSupported");
                if (mainShadows != null) mainShadows.boolValue = true;
                var softShadows = serialized.FindProperty("m_SoftShadowsSupported");
                if (softShadows != null) softShadows.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                pipeline.msaaSampleCount = 2;
                pipeline.renderScale = 1f;
                pipeline.supportsHDR = true;
                pipeline.shadowDistance = 35f;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static Material Palette(string name, Color color, float metallic = 0f)
        {
            string path = SettingsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", .23f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
