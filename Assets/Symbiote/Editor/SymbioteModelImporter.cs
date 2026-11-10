using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SymbioteRaid.Editor
{
    /// <summary>
    /// Explicit, optional integration of models supplied by the project owner.
    /// FBX imports natively. A GLB needs a compatible glTF importer installed by
    /// the owner first; this utility never silently installs a package.
    /// </summary>
    public static class SymbioteModelImporter
    {
        const string ControllerPath = "Assets/Symbiote/Settings/SwatAnimator.controller";
        static readonly string[] ModelPriority = { "swat", "swatguy", "vanguardsoldier", "soldieranimated" };
        static readonly string[] ClipNames = {
            "Rifle_Aiming_Idle", "Firing_Rifle", "Rifle_Walk", "Rifle_Run",
            "Backwards_Rifle_Walk", "Backwards_Rifle_Run", "Crouch_Idle",
            "Idle", "Walking", "Running"
        };

        [MenuItem("Symbiote/Importar assets.zip (modelos y texturas)")]
        public static void ImportExternalZip()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Sal de Play antes de importar los recursos.");
                return;
            }
            string archivePath = EditorUtility.OpenFilePanel("Selecciona assets.zip", "", "zip");
            if (string.IsNullOrEmpty(archivePath)) return;
            string destination = Path.GetFullPath("Assets/External");
            string prefix = destination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var permitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".fbx", ".png", ".jpg", ".jpeg", ".tga", ".exr", ".glb", ".gltf", ".bin" };
            int imported = 0, skipped = 0;
            try
            {
                using (var stream = File.OpenRead(archivePath))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    var entries = new List<KeyValuePair<ZipArchiveEntry, string>>();
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string relative = entry.FullName.Replace('\\', '/');
                        // Keep only reviewed asset formats; never execute scripts from the ZIP.
                        if (!permitted.Contains(Path.GetExtension(relative))) { skipped++; continue; }
                        if (Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0 ||
                            relative.Split('/').Any(segment => segment == "..")) { skipped++; continue; }
                        string path = Path.GetFullPath(Path.Combine(destination, relative));
                        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                        entries.Add(new KeyValuePair<ZipArchiveEntry, string>(entry, path));
                    }
                    if (entries.Count == 0)
                    {
                        Debug.LogWarning("El ZIP no contiene modelos o texturas compatibles con esta herramienta.");
                        return;
                    }
                    bool overwrite = false;
                    if (entries.Any(pair => File.Exists(pair.Value)))
                    {
                        int choice = EditorUtility.DisplayDialogComplex("Recursos ya existentes",
                            "Algunos archivos de Assets/External ya existen. ¿Cómo quieres continuar?",
                            "Reemplazar", "Cancelar", "Omitir existentes");
                        if (choice == 1) return;
                        overwrite = choice == 0;
                    }
                    Directory.CreateDirectory(destination);
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            var pair = entries[i];
                            EditorUtility.DisplayProgressBar("Importando modelos y texturas",
                                pair.Key.FullName, (i + 1f) / entries.Count);
                            if (File.Exists(pair.Value) && !overwrite) { skipped++; continue; }
                            Directory.CreateDirectory(Path.GetDirectoryName(pair.Value));
                            using (Stream input = pair.Key.Open())
                            using (var output = new FileStream(pair.Value, FileMode.Create, FileAccess.Write))
                                input.CopyTo(output);
                            imported++;
                        }
                    }
                    finally { AssetDatabase.StopAssetEditing(); }
                }
                AssetDatabase.Refresh();
                Debug.Log("Recursos extraídos en Assets/External: " + imported + ". Omitidos: " + skipped +
                    ". Se excluyeron scripts, shaders, HTML y metadatos externos; sus implementaciones requieren revisión. " +
                    "Los GLB necesitan un importador glTF compatible ya instalado. Se integrarán los FBX válidos disponibles.");
                Integrate();
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException ||
                exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogError("No se pudo completar la importación del ZIP: " + exception.Message +
                    ". Revisa la ruta y los archivos parcialmente extraídos en Assets/External.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem("Symbiote/Integrar modelos y animaciones FBX")]
        public static void Integrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Sal de Play antes de integrar modelos y animaciones.");
                return;
            }
            string[] settings = AssetDatabase.FindAssets("t:SymbioteAssets", new[] { "Assets" });
            if (settings.Length == 0)
            {
                Debug.LogWarning("Primero crea el prototipo desde el menú Symbiote para generar sus recursos.");
                return;
            }
            var config = AssetDatabase.LoadAssetAtPath<SymbioteAssets>(AssetDatabase.GUIDToAssetPath(settings[0]));
            if (config == null) return;
            string[] paths = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                    (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                     p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            int symbioteModels = IntegrateSymbioteModels(config, paths);
            if (symbioteModels > 0)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log("Modelos de biomasa FBX asignados: " + symbioteModels + ". Rig Generic, huesos conservados.");
            }
            string modelPath = FindModel(paths);
            if (string.IsNullOrEmpty(modelPath))
            {
                Debug.LogWarning("No se encontró Swat.fbx, Swat_Guy.fbx, Vanguard_Soldier.fbx ni Soldier_Animated. " +
                    "Copia los recursos reales dentro de Assets y vuelve a ejecutar este menú. " +
                    "Los archivos GLB requieren un importador glTF y un modelo ya importado como GameObject.");
                return;
            }
            if (modelPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) PrepareHumanoid(modelPath);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Avatar avatar = LoadAvatar(modelPath, model);
            if (model == null || avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning("El modelo seleccionado no tiene un Avatar Humanoid válido: " + modelPath +
                    ". Revisa Rig > Configure y la asignación de huesos en el Inspector. No se sustituyeron los recursos del juego.");
                return;
            }

            var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            foreach (string label in ClipNames)
            {
                string path = paths.FirstOrDefault(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                    Normalize(Path.GetFileNameWithoutExtension(p)) == Normalize(label));
                if (string.IsNullOrEmpty(path)) continue;
                PrepareHumanoid(path);
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) && c.humanMotion);
                if (clip != null) clips[label] = clip;
                else Debug.LogWarning("No hay un clip Humanoid válido en " + path + ". Se omitió esta animación.");
            }
            // An already imported, animated model may contain usable Idle/Walk/Run clips.
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>())
            {
                if (clip.name.StartsWith("__preview__", StringComparison.Ordinal) || !clip.humanMotion) continue;
                string key = ClipNames.FirstOrDefault(n => Normalize(n) == Normalize(clip.name));
                if (key != null && !clips.ContainsKey(key)) clips[key] = clip;
            }
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
                Debug.Log("Se conserva el Animator existente en " + ControllerPath + "; no se sobrescribieron sus estados.");
            else if (clips.Count > 0)
                controller = BuildController(clips);
            else
                Debug.LogWarning("Modelo válido, pero no se encontraron animaciones Humanoid utilizables. " +
                    "Se asignará el modelo y se conservará el controlador anterior si lo había.");
            Undo.RecordObject(config, "Integrar modelo SWAT");
            config.swatModel = model;
            if (controller != null) config.swatAnimations = controller;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("Modelo Humanoid asignado: " + modelPath + ". Clips válidos encontrados: " + clips.Count +
                ". Pulsa Play y comprueba tamaño, orientación, rifle y animaciones. " +
                "El importador no crea ni simula archivos ausentes.");
        }

        static int IntegrateSymbioteModels(SymbioteAssets config, string[] paths)
        {
            int assigned = 0;
            GameObject full = LoadGenericModel(paths, "Symbiote_Full_Rigged");
            GameObject core = LoadGenericModel(paths, "Symbiote_Core_Blob");
            GameObject tentacle = LoadGenericModel(paths, "Symbiote_Tentacle_Rigged");
            if (full != null) { config.symbioteModel = full; assigned++; }
            if (core != null) { config.symbioteCoreModel = core; assigned++; }
            if (tentacle != null) { config.symbioteTentacleModel = tentacle; assigned++; }
            return assigned;
        }

        static GameObject LoadGenericModel(string[] paths, string label)
        {
            string name = Normalize(label);
            string path = paths.FirstOrDefault(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                Normalize(Path.GetFileNameWithoutExtension(p)) == name);
            if (string.IsNullOrEmpty(path))
                path = paths.FirstOrDefault(p => p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) &&
                    Normalize(Path.GetFileNameWithoutExtension(p)) == name && AssetDatabase.LoadAssetAtPath<GameObject>(p) != null);
            if (string.IsNullOrEmpty(path)) return null;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && (importer.animationType != ModelImporterAnimationType.Generic || importer.optimizeGameObjects))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static string FindModel(string[] paths)
        {
            foreach (string name in ModelPriority)
            {
                string fbx = paths.FirstOrDefault(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                    Normalize(Path.GetFileNameWithoutExtension(p)) == name);
                if (!string.IsNullOrEmpty(fbx)) return fbx;
                string glb = paths.FirstOrDefault(p => p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) &&
                    Normalize(Path.GetFileNameWithoutExtension(p)) == name && AssetDatabase.LoadAssetAtPath<GameObject>(p) != null);
                if (!string.IsNullOrEmpty(glb)) return glb;
            }
            return null;
        }

        static string Normalize(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        static void PrepareHumanoid(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            bool changed = importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                importer.optimizeGameObjects || !importer.importAnimation;
            string clipFile = Normalize(Path.GetFileNameWithoutExtension(path));
            bool isKnownAnimation = ClipNames.Any(n => Normalize(n) == clipFile);
            if (isKnownAnimation)
            {
                ModelImporterClipAnimation[] animationClips = importer.clipAnimations;
                if (animationClips.Length == 0) animationClips = importer.defaultClipAnimations;
                bool loop = clipFile != Normalize("Firing_Rifle");
                foreach (ModelImporterClipAnimation animation in animationClips)
                {
                    if (animation.loopTime != loop || animation.loopPose != loop) changed = true;
                    animation.loopTime = loop;
                    animation.loopPose = loop;
                }
                if (changed && animationClips.Length > 0) importer.clipAnimations = animationClips;
            }
            if (!changed) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            // Keep Mixamo transforms available for Spine2 / Head / arm attachments.
            importer.optimizeGameObjects = false;
            importer.importAnimation = true;
            importer.SaveAndReimport();
        }

        static Avatar LoadAvatar(string path, GameObject model)
        {
            if (model != null)
            {
                Animator animator = model.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.avatar != null) return animator.avatar;
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(a => a.isValid && a.isHuman);
        }

        static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            AssetDatabase.Refresh();
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = machine.AddState("Tactical locomotion");
            machine.defaultState = locomotion;
            AnimationClip fallback = clips.Values.First();
            var tree = new BlendTree { name = "Signed speed", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            var children = new List<ChildMotion>();
            AddMotion(children, Find(clips, "Backwards_Rifle_Run"), -1f);
            AddMotion(children, Find(clips, "Backwards_Rifle_Walk"), -.5f);
            AddMotion(children, Find(clips, "Rifle_Aiming_Idle", "Idle") ?? fallback, 0f);
            AddMotion(children, Find(clips, "Rifle_Walk", "Walking") ?? fallback, .5f);
            AddMotion(children, Find(clips, "Rifle_Run", "Running") ?? fallback, 1f);
            tree.children = children.ToArray();
            locomotion.motion = tree;
            AnimatorState crouch = null;
            AnimationClip crouchClip = Find(clips, "Crouch_Idle");
            if (crouchClip != null)
            {
                crouch = machine.AddState("Crouch");
                crouch.motion = crouchClip;
                Transition(locomotion, crouch, "Crouch", AnimatorConditionMode.If);
                Transition(crouch, locomotion, "Crouch", AnimatorConditionMode.IfNot);
            }
            AnimationClip fireClip = Find(clips, "Firing_Rifle");
            if (fireClip != null)
            {
                AnimatorState fire = machine.AddState("Fire");
                fire.motion = fireClip;
                AnimatorStateTransition entry = machine.AddAnyStateTransition(fire);
                entry.hasExitTime = false;
                entry.duration = .04f;
                entry.canTransitionToSelf = false;
                entry.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
                AnimatorStateTransition exit = fire.AddTransition(locomotion);
                exit.hasExitTime = true;
                exit.exitTime = .92f;
                exit.duration = .08f;
                if (crouch != null)
                {
                    exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouch");
                    AnimatorStateTransition crouchExit = fire.AddTransition(crouch);
                    crouchExit.hasExitTime = true;
                    crouchExit.exitTime = .92f;
                    crouchExit.duration = .08f;
                    crouchExit.AddCondition(AnimatorConditionMode.If, 0f, "Crouch");
                }
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static void AddMotion(List<ChildMotion> children, AnimationClip clip, float threshold)
        {
            if (clip != null) children.Add(new ChildMotion { motion = clip, threshold = threshold, timeScale = 1f });
        }

        static AnimationClip Find(Dictionary<string, AnimationClip> clips, params string[] names)
        {
            foreach (string name in names) if (clips.TryGetValue(name, out AnimationClip clip)) return clip;
            return null;
        }

        static void Transition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = .12f;
            transition.AddCondition(mode, 0f, parameter);
        }
    }
}
