using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SymbioteRaid.Editor
{
    /// <summary>Creates a self-contained scene and saved render resources on the first import.</summary>
    [InitializeOnLoad]
    public static class SymbioteSetup
    {
        const string ScenePath = "Assets/Symbiote/Scenes/Containment.unity";
        const string Folder = "Assets/Symbiote/Settings";
        const string ConfigPath = Folder + "/SymbioteAssets.asset";
        static SymbioteSetup() { EditorApplication.delayCall += FirstOpen; }

        static void FirstOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += FirstOpen;
                return;
            }
            Create();
        }

        [MenuItem("Symbiote/Crear prototipo")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath)) { Open(); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Symbiote/Scenes");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            ConfigurePipeline();
            ConfigureInput();
            SymbioteAssets assets = AssetDatabase.LoadAssetAtPath<SymbioteAssets>(ConfigPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<SymbioteAssets>();
                assets.name = "Symbiote resources";
                AssetDatabase.CreateAsset(assets, ConfigPath);
            }
            if (assets.wallMaterial == null) assets.wallMaterial = SavedMaterial("Concrete", "Universal Render Pipeline/Lit", new Color(.22f, .27f, .29f), .05f, .28f);
            if (assets.floorMaterial == null) assets.floorMaterial = SavedMaterial("Floor", "Universal Render Pipeline/Lit", new Color(.11f, .15f, .17f), .12f, .35f);
            if (assets.metalMaterial == null) assets.metalMaterial = SavedMaterial("Metal", "Universal Render Pipeline/Lit", new Color(.055f, .075f, .085f), .6f, .4f);
            if (assets.accentMaterial == null) assets.accentMaterial = SavedMaterial("ContainmentGreen", "Universal Render Pipeline/Lit", new Color(.37f, .75f, .3f), .1f, .32f);
            if (assets.slimeMaterial == null) assets.slimeMaterial = SavedMaterial("PetroleumSlime", "SymbioteRaid/Slime", new Color(.012f, .019f, .025f), .25f, .93f);
            if (assets.infectionMaterial == null) assets.infectionMaterial = SavedMaterial("HostInfection", "SymbioteRaid/Infection", new Color(.17f, .22f, .25f), .08f, .5f);
            if (assets.swatModel == null) assets.swatModel = FindModel();
            if (assets.bodyDiffuse == null) assets.bodyDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/External/SWAT/Textures/Soldier_Body_diffuse.png");
            if (assets.headDiffuse == null) assets.headDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/External/SWAT/Textures/Soldier_head_diffuse.png");
            EditorUtility.SetDirty(assets);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var facility = new GameObject("Containment / facility generator").AddComponent<SymbioteFacility>();
            facility.assets = assets;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Independent";
            PlayerSettings.productName = "Umbra - Simbiosis";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = false;
            QualitySettings.vSyncCount = 1;
            AssetDatabase.SaveAssets();
            if (assets.swatModel != null) SymbioteModelImporter.Integrate();
            Debug.Log("Simbiosis lista. Pulsa Play y haz clic en Game. El edificio se genera al ejecutar la escena. Modelos externos: " + (assets.swatModel != null ? "detectados" : "pendientes; se usan modelos provisionales"));
        }

        [MenuItem("Symbiote/Abrir prototipo")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath)) { Create(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        static Material SavedMaterial(string name, string shaderName, Color color, float metallic, float smoothness)
        {
            string path = Folder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("No se encontró URP. Espera a que terminen de importar los paquetes.");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_PossessionAmount")) material.SetFloat("_PossessionAmount", 0);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject FindModel()
        {
            foreach (string name in new[] { "Swat", "Swat_Guy", "Vanguard_Soldier", "Soldier_Animated" })
            {
                string[] guids = AssetDatabase.FindAssets(name + " t:Model", new[] { "Assets" });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.Equals(Path.GetFileNameWithoutExtension(path), name, System.StringComparison.OrdinalIgnoreCase)) continue;
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model != null) return model;
                }
            }
            return null;
        }

        static void ConfigureInput()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0) return;
            var serialized = new SerializedObject(settings[0]);
            var input = serialized.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void ConfigurePipeline()
        {
            string rendererPath = Folder + "/SymbioteRenderer.asset";
            string pipelinePath = Folder + "/SymbiotePipeline.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "SymbioteRenderer";
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "SymbiotePipeline";
                var serialized = new SerializedObject(pipeline);
                var renderers = serialized.FindProperty("m_RendererDataList");
                if (renderers != null)
                {
                    renderers.arraySize = 1;
                    renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                }
                Bool(serialized, "m_MainLightShadowsSupported", true);
                Bool(serialized, "m_AdditionalLightShadowsSupported", true);
                Bool(serialized, "m_SoftShadowsSupported", true);
                var mode = serialized.FindProperty("m_AdditionalLightsRenderingMode");
                if (mode != null) mode.intValue = (int)LightRenderingMode.PerPixel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                pipeline.supportsHDR = true;
                pipeline.msaaSampleCount = 2;
                pipeline.renderScale = 1;
                pipeline.shadowDistance = 40;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        static void Bool(SerializedObject serialized, string field, bool value)
        {
            var property = serialized.FindProperty(field);
            if (property != null) property.boolValue = value;
        }
    }
}
