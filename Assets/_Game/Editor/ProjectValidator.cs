using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Foundation;
using Game.Gameplay;
using Game.Infrastructure;
using Game.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Editor
{
    public static class ProjectValidator
    {
        [MenuItem("Game/Validation/Run All Quality Gates")]
        public static void ValidateFromMenu() => ValidateOrThrow();

        public static void ValidateBatchMode()
        {
            try
            {
                ValidateOrThrow();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateOrThrow()
        {
            var failures = new List<string>();
            ValidateBaseline(failures);
            ValidateBuildScenes(failures);
            ValidateMissingScripts(failures);
            ValidateLevelProfiles(failures);
            ValidateAddressables(failures);
            ValidateContentCatalogs(failures);
            ValidateVerticalSlicePresenter(failures);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("Quality gates failed:\n - " + string.Join("\n - ", failures));
            }

            Debug.Log("All project quality gates passed.");
        }

        private static void ValidateBaseline(ICollection<string> failures)
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            var versionFile = Path.Combine(root, "ProjectSettings", "ProjectVersion.txt");
            var packageLock = Path.Combine(root, "Packages", "packages-lock.json");
            if (!File.ReadAllText(versionFile).Contains("6000.3.18f1"))
            {
                failures.Add("Unity version is not locked to 6000.3.18f1.");
            }

            if (!File.Exists(packageLock))
            {
                failures.Add("Packages/packages-lock.json is missing.");
            }
        }

        private static void ValidateBuildScenes(ICollection<string> failures)
        {
            var enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (enabled.Length == 0 || !enabled[0].path.EndsWith("/Bootstrap.unity", StringComparison.Ordinal))
            {
                failures.Add("Bootstrap must be the first enabled build scene.");
            }

            foreach (var scene in enabled.Where(scene => !File.Exists(scene.path)))
            {
                failures.Add($"Build scene is missing: {scene.path}");
            }
        }

        private static void ValidateMissingScripts(ICollection<string> failures)
        {
            foreach (var prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null && GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) > 0)
                {
                    failures.Add($"Prefab contains missing scripts: {path}");
                }
            }

            foreach (var sceneSetting in EditorBuildSettings.scenes.Where(scene => scene.enabled))
            {
                var scene = EditorSceneManager.OpenScene(sceneSetting.path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) > 0)
                        {
                            failures.Add($"Scene contains missing scripts: {sceneSetting.path}/{root.name}");
                        }

                        foreach (var compositionRoot in root.GetComponentsInChildren<CompositionRoot>(true))
                        {
                            var serialized = new SerializedObject(compositionRoot);
                            if (serialized.FindProperty("sourceConfig").objectReferenceValue == null)
                            {
                                failures.Add($"CompositionRoot has no GameConfig: {sceneSetting.path}/{root.name}");
                            }

                            if (serialized.FindProperty("inputService").objectReferenceValue == null)
                            {
                                failures.Add($"CompositionRoot has no input service: {sceneSetting.path}/{root.name}");
                            }
                        }

                        foreach (var playerInput in root.GetComponentsInChildren<PlayerInput>(true))
                        {
                            if (playerInput.actions == null)
                            {
                                failures.Add($"PlayerInput has no actions asset: {sceneSetting.path}/{root.name}");
                            }
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateLevelProfiles(ICollection<string> failures)
        {
            var profiles = AssetDatabase.FindAssets("t:LevelProductionProfile", new[] { "Assets/_Game/Settings" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelProductionProfile>)
                .Where(profile => profile != null)
                .ToArray();

            foreach (var duplicate in profiles.Where(profile => !string.IsNullOrWhiteSpace(profile.StableId)).GroupBy(profile => profile.StableId).Where(group => group.Count() > 1))
            {
                failures.Add($"Duplicate level stable ID: {duplicate.Key}");
            }

            foreach (var levelScene in EditorBuildSettings.scenes.Where(scene => scene.enabled && scene.path.Contains("/Levels/Level_") && !scene.path.Contains("_Lighting") && !scene.path.Contains("_Presentation")))
            {
                var name = Path.GetFileNameWithoutExtension(levelScene.path);
                var profile = profiles.FirstOrDefault(candidate => candidate.SceneName == name);
                if (profile == null)
                {
                    failures.Add($"Level has no production profile: {name}");
                }
                else if (string.IsNullOrWhiteSpace(profile.StableId))
                {
                    failures.Add($"Level has no stable content ID: {name}");
                }
                else if (string.IsNullOrWhiteSpace(profile.Owner) || profile.Owner == "Unassigned")
                {
                    Debug.LogWarning($"Level owner has not been assigned: {name}");
                }
            }
        }

        private static void ValidateAddressables(ICollection<string> failures)
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                failures.Add("Addressables settings have not been created.");
                return;
            }

            foreach (var group in settings.groups.Where(group => group != null))
            {
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                {
                    continue;
                }

                var loadPath = schema.LoadPath.GetValue(settings);
                if (loadPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"Remote Addressables are forbidden for v1: {group.Name}");
                }
            }
        }

        private static void ValidateContentCatalogs(ICollection<string> failures)
        {
            var paths = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Game/Content/Source" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (paths.Length == 0)
            {
                failures.Add("No runtime content catalogs were found in Assets/_Game/Content/Source.");
                return;
            }

            var contentCatalogs = new List<ContentCatalog>();
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                var result = asset == null ? default : ContentCatalogJson.ParseApproved(asset.text);
                if (asset == null)
                {
                    failures.Add($"Content catalog could not be loaded: {path}");
                }
                else if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        failures.Add($"Content catalog {path}: {error}");
                    }
                }
                else
                {
                    contentCatalogs.Add(result.Catalog);
                }
            }

            var localizationPaths = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Game/Content/Localization" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var localizations = new List<LocalizationCatalog>();
            foreach (var path in localizationPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                var result = asset == null ? default : LocalizationCatalogJson.ParseApproved(asset.text);
                if (asset == null)
                {
                    failures.Add($"Localization catalog could not be loaded: {path}");
                }
                else if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        failures.Add($"Localization catalog {path}: {error}");
                    }
                }
                else
                {
                    localizations.Add(result.Catalog);
                }
            }

            foreach (var content in contentCatalogs)
            {
                foreach (var error in LocalizationCatalogJson.ValidateCoverage(content, localizations, new[] { "en", "zh-Hans" }))
                {
                    failures.Add($"Localization coverage for {content.ContentVersion}: {error}");
                }
            }
        }

        private static void ValidateVerticalSlicePresenter(ICollection<string> failures)
        {
            const string path = "Assets/_Game/Scenes/Bootstrap.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var presenters = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<VerticalSlicePresenter>(true))
                    .ToArray();
                if (presenters.Length != 1)
                {
                    failures.Add($"Bootstrap must contain exactly one VerticalSlicePresenter; found {presenters.Length}.");
                    return;
                }

                var serialized = new SerializedObject(presenters[0]);
                foreach (var propertyName in new[] { "contentSource", "englishSource", "simplifiedChineseSource" })
                {
                    if (serialized.FindProperty(propertyName).objectReferenceValue == null)
                    {
                        failures.Add($"VerticalSlicePresenter is missing {propertyName}.");
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
