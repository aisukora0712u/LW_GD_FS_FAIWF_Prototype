using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    public static class BuildCommand
    {
        private const string GeneratedInfoPath = "Assets/Resources/Generated/build-info.json";

        public static void BuildDevelopment() => Build(true);
        public static void BuildRelease() => Build(false);

        private static void Build(bool development)
        {
            var arguments = ParseArguments(Environment.GetCommandLineArgs());
            var output = Get(arguments, "outputPath", development ? "BuildOutput/Development/Game.exe" : "BuildOutput/Release/Game.exe");
            var version = Get(arguments, "buildVersion", "0.1.0");
            var buildNumber = Get(arguments, "buildNumber", "local");
            var channel = Get(arguments, "channel", development ? "development" : "release");
            var commit = Get(arguments, "commit", Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "working-tree");

            if (!Game.Core.SemanticVersion.TryParse(version, out _))
            {
                throw new ArgumentException($"Invalid semantic version: {version}");
            }

            ProjectValidator.ValidateOrThrow();
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            var originalVersion = PlayerSettings.bundleVersion;
            var originalBackend = PlayerSettings.GetScriptingBackend(target);
            var originalCompilerConfiguration = PlayerSettings.GetIl2CppCompilerConfiguration(target);
            PlayerSettings.bundleVersion = version;
            PlayerSettings.SetScriptingBackend(target, development ? ScriptingImplementation.Mono2x : ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(target, Il2CppCompilerConfiguration.Release);
            WriteBuildInfo(version, buildNumber, commit, channel);

            try
            {
                var fullOutput = Path.GetFullPath(output);
                Directory.CreateDirectory(Path.GetDirectoryName(fullOutput) ?? throw new InvalidOperationException("Invalid output path."));
                var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                var options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.CleanBuildCache;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = fullOutput,
                    target = BuildTarget.StandaloneWindows64,
                    options = options
                });

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException($"Build failed: {report.summary.result}, errors: {report.summary.totalErrors}");
                }

                Debug.Log($"Build succeeded: {fullOutput} ({report.summary.totalSize} bytes)");
            }
            finally
            {
                AssetDatabase.DeleteAsset(GeneratedInfoPath);
                PlayerSettings.bundleVersion = originalVersion;
                PlayerSettings.SetScriptingBackend(target, originalBackend);
                PlayerSettings.SetIl2CppCompilerConfiguration(target, originalCompilerConfiguration);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void WriteBuildInfo(string version, string buildNumber, string commit, string channel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GeneratedInfoPath) ?? "Assets/Resources");
            var data = new BuildInfoData
            {
                version = version,
                buildNumber = buildNumber,
                commit = commit,
                channel = channel,
                unityVersion = Application.unityVersion
            };
            File.WriteAllText(GeneratedInfoPath, JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(GeneratedInfoPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static Dictionary<string, string> ParseArguments(IReadOnlyList<string> arguments)
        {
            var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < arguments.Count - 1; index++)
            {
                if (arguments[index].StartsWith("-", StringComparison.Ordinal) && !arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    parsed[arguments[index].TrimStart('-')] = arguments[index + 1];
                }
            }

            return parsed;
        }

        private static string Get(IReadOnlyDictionary<string, string> arguments, string key, string fallback) =>
            arguments.TryGetValue(key, out var value) ? value : fallback;

        [Serializable]
        private sealed class BuildInfoData
        {
            public string version;
            public string buildNumber;
            public string commit;
            public string channel;
            public string unityVersion;
        }
    }
}
