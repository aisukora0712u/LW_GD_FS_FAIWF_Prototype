using System.IO;
using System.Linq;
using Game.Foundation;
using Game.Infrastructure;
using Game.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class ProjectScaffolder
    {
        private const string SettingsRoot = "Assets/_Game/Settings";
        private const string ScenesRoot = "Assets/_Game/Scenes";

        [MenuItem("Game/Setup/Create Initial Project Assets")]
        public static void CreateInitialAssets()
        {
            EnsureDirectory(SettingsRoot);
            EnsureDirectory(ScenesRoot);
            EnsureDirectory(ScenesRoot + "/Levels");

            var config = LoadOrCreate<GameConfig>(SettingsRoot + "/GameConfig.asset");
            var inputActions = CreateInputActions();
            CreateRenderPipeline();
            CreateAddressables();
            CreateLevelProfile();
            CreateScenes(config, inputActions);
            InstallVerticalSlice();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Initial project assets created successfully.");
        }

        [MenuItem("Game/Setup/Install Vertical Slice")]
        public static void InstallVerticalSlice()
        {
            const string bootstrapPath = ScenesRoot + "/Bootstrap.unity";
            const string contentPath = "Assets/_Game/Content/Source/vertical-slice.json";
            const string englishPath = "Assets/_Game/Content/Localization/vertical-slice.en.json";
            const string chinesePath = "Assets/_Game/Content/Localization/vertical-slice.zh-Hans.json";
            if (!File.Exists(bootstrapPath))
            {
                throw new FileNotFoundException("Bootstrap scene must exist before installing the vertical slice.", bootstrapPath);
            }

            var scene = EditorSceneManager.OpenScene(bootstrapPath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.GetComponent<CompositionRoot>() != null);
            if (root == null)
            {
                throw new InvalidDataException("Bootstrap scene has no CompositionRoot.");
            }

            var presenter = root.GetComponent<VerticalSlicePresenter>() ?? root.AddComponent<VerticalSlicePresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("contentSource").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(contentPath);
            serialized.FindProperty("englishSource").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(englishPath);
            serialized.FindProperty("simplifiedChineseSource").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(chinesePath);
            serialized.FindProperty("initialLocale").stringValue = "zh-Hans";
            serialized.FindProperty("runSeed").longValue = 4242L;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Vertical slice presenter installed in Bootstrap.");
        }

        private static InputActionAsset CreateInputActions()
        {
            const string path = SettingsRoot + "/GameInputActions.asset";
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var gameplay = asset.AddActionMap("Gameplay");
            var move = gameplay.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            gameplay.AddAction("Interact", InputActionType.Button).AddBinding("<Keyboard>/e");
            gameplay["Interact"].AddBinding("<Gamepad>/buttonSouth");
            gameplay.AddAction("Pause", InputActionType.Button).AddBinding("<Keyboard>/escape");
            gameplay["Pause"].AddBinding("<Gamepad>/start");

            var ui = asset.AddActionMap("UI");
            ui.AddAction("Navigate", InputActionType.PassThrough, "<Gamepad>/leftStick");
            ui.AddAction("Submit", InputActionType.Button, "<Gamepad>/buttonSouth");
            ui.AddAction("Cancel", InputActionType.Button, "<Gamepad>/buttonEast");

            asset.AddControlScheme("Keyboard&Mouse").WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");
            asset.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void CreateRenderPipeline()
        {
            const string rendererPath = SettingsRoot + "/ForwardRenderer.asset";
            const string pipelinePath = SettingsRoot + "/UniversalRenderPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static void CreateAddressables()
        {
            if (UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings == null)
            {
                EnsureDirectory("Assets/AddressableAssetsData");
                var settings = AddressableAssetSettings.Create(
                    SettingsRoot + "/AddressableAssetsData",
                    "AddressableAssetSettings",
                    true,
                    true);
                UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings = settings;
            }
        }

        private static void CreateLevelProfile()
        {
            const string path = SettingsRoot + "/Level_Example_Profile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<LevelProductionProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<LevelProductionProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("stableId").stringValue = "level.example";
            serialized.FindProperty("sceneName").stringValue = "Level_Example";
            var owner = serialized.FindProperty("owner");
            if (string.IsNullOrWhiteSpace(owner.stringValue) || owner.stringValue == "Unassigned")
            {
                owner.stringValue = "Level Design";
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void CreateScenes(GameConfig config, InputActionAsset inputActions)
        {
            var bootstrapPath = ScenesRoot + "/Bootstrap.unity";
            if (!File.Exists(bootstrapPath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("GameCompositionRoot");
                var playerInput = root.AddComponent<PlayerInput>();
                playerInput.actions = inputActions;
                playerInput.defaultActionMap = "Gameplay";
                playerInput.defaultControlScheme = "Keyboard&Mouse";
                var input = root.AddComponent<PlayerInputService>();
                var composition = root.AddComponent<CompositionRoot>();
                var serialized = new SerializedObject(composition);
                serialized.FindProperty("sourceConfig").objectReferenceValue = config;
                serialized.FindProperty("inputService").objectReferenceValue = input;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, bootstrapPath);
            }

            CreateEmptyScene(ScenesRoot + "/Persistent.unity", "PersistentSystems");
            CreateExampleLevel(ScenesRoot + "/Levels/Level_Example.unity");
            CreateEmptyScene(ScenesRoot + "/Levels/Level_Example_Lighting.unity", "LightingRoot");
            CreateEmptyScene(ScenesRoot + "/Levels/Level_Example_Presentation.unity", "PresentationRoot");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(bootstrapPath, true),
                new EditorBuildSettingsScene(ScenesRoot + "/Persistent.unity", true),
                new EditorBuildSettingsScene(ScenesRoot + "/Levels/Level_Example.unity", true),
                new EditorBuildSettingsScene(ScenesRoot + "/Levels/Level_Example_Lighting.unity", true),
                new EditorBuildSettingsScene(ScenesRoot + "/Levels/Level_Example_Presentation.unity", true)
            };
        }

        private static void CreateExampleLevel(string path)
        {
            if (File.Exists(path))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 4f, -8f), Quaternion.Euler(18f, 0f, 0f));
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Blockout_Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Blockout_GameplayMarker";
            marker.transform.position = new Vector3(0f, 0.5f, 0f);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateEmptyScene(string path, string rootName)
        {
            if (File.Exists(path))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.MoveGameObjectToScene(new GameObject(rootName), scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
