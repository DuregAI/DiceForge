using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Diceforge.EditorTools
{
    /// <summary>Builds the current beta art without changing project or player settings.</summary>
    public static class BetaWebBuild
    {
        public const string OutputDirectory = "C:/Backforge/Builds/GlimblehopBetaPolish";
        public const string ReportDirectory = "docs/Validation/BetaPolishWeb";

        /// <summary>Include runtime-switched pipelines in URP's build analysis without changing menu quality.</summary>
        public static string PrepareRuntimePipelines()
        {
            var board = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab");
            if (board == null) throw new BuildFailedException("FirstTrail8 prefab is missing.");
            var pipelines = new HashSet<UniversalRenderPipelineAsset>();
            foreach (var component in board.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                foreach (var field in new[] { "desktopPipeline", "mobilePipeline", "pipelineOverride" })
                {
                    var property = serialized.FindProperty(field);
                    if (property?.objectReferenceValue is UniversalRenderPipelineAsset pipeline) pipelines.Add(pipeline);
                }
            }
            if (pipelines.Count == 0) throw new BuildFailedException("No runtime diorama pipelines are assigned.");
            var inclusion = GraphicsSettings.GetRenderPipelineSettings<IncludeAdditionalRPAssets>();
            if (inclusion == null) throw new BuildFailedException("URP additional asset inclusion settings are missing.");
            string label = string.IsNullOrWhiteSpace(inclusion.labelToInclude)
                ? "GlimbleHopRuntimePipeline" : inclusion.labelToInclude;
            inclusion.labelToInclude = label;
            inclusion.includeAssetsByLabel = true;
            foreach (var pipeline in pipelines)
            {
                var labels = AssetDatabase.GetLabels(pipeline);
                if (!labels.Contains(label)) AssetDatabase.SetLabels(pipeline, labels.Concat(new[] { label }).ToArray());
            }
            var globalSettings = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(
                "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
            if (globalSettings == null) throw new BuildFailedException("URP global settings asset is missing.");
            EditorUtility.SetDirty(globalSettings);
            AssetDatabase.SaveAssets();

            var collected = new List<UniversalRenderPipelineAsset>();
            if (!BuildTarget.WebGL.TryGetRenderPipelineAssets(collected) || pipelines.Any(p => !collected.Contains(p)))
                throw new BuildFailedException("URP build collection still excludes a runtime diorama pipeline.");
            var result = JsonUtility.ToJson(new PipelineInclusion
            {
                label = label,
                defaultPipeline = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline),
                currentQualityPipeline = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline),
                included = collected.Select(AssetDatabase.GetAssetPath).OrderBy(path => path).ToArray()
            }, true);
            Directory.CreateDirectory(ReportDirectory);
            File.WriteAllText(Path.Combine(ReportDirectory, "render-pipeline-inclusion.json"), result);
            return result;
        }
        // Invoke directly: unfocused Editor delayCall scheduling can stall.
        // Long builds may outlive an MCP timeout; poll ReportDirectory/status.json instead of retrying.
        // Also usable with Unity.exe -batchmode -buildTarget WebGL -executeMethod ...Build -quit.
        public static void Build()
        {
            ValidateEditor();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new BuildFailedException("Install Web Build Support for this Unity Editor before building.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new BuildFailedException("Select the WebGL build target before invoking this helper.");

            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || scenes.Any(scene => !File.Exists(scene)))
                throw new BuildFailedException("Enabled build scenes are empty or missing.");

            PrepareRuntimePipelines();

            Directory.CreateDirectory(OutputDirectory);
            Directory.CreateDirectory(ReportDirectory);
            AssetDatabase.SaveAssets();
            var startedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(Path.Combine(ReportDirectory, "status.json"), JsonUtility.ToJson(new Status
            {
                result = "Building", startedUtc = startedUtc, output = OutputDirectory,
                unityVersion = Application.unityVersion, productVersion = PlayerSettings.bundleVersion, scenes = scenes
            }, true));

            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = OutputDirectory,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.DetailedBuildReport
                });
                var summary = report.summary;
                File.WriteAllText(Path.Combine(ReportDirectory, "status.json"), JsonUtility.ToJson(new Status
                {
                    result = summary.result.ToString(), startedUtc = startedUtc, finishedUtc = DateTime.UtcNow.ToString("O"),
                    output = OutputDirectory, unityVersion = Application.unityVersion,
                    productVersion = PlayerSettings.bundleVersion, scenes = scenes,
                    errors = summary.totalErrors, warnings = summary.totalWarnings,
                    bytes = summary.totalSize, seconds = summary.totalTime.TotalSeconds
                }, true));
                File.WriteAllLines(Path.Combine(ReportDirectory, "build-messages.txt"), report.steps
                    .SelectMany(step => step.messages).Select(message => message.type + ": " + message.content));
                var assets = report.packedAssets.SelectMany(pack => pack.contents)
                    .GroupBy(asset => asset.sourceAssetPath)
                    .Select(group => new { Path = group.Key, Bytes = group.Sum(asset => (long)asset.packedSize) })
                    .OrderByDescending(asset => asset.Bytes).ToArray();
                File.WriteAllLines(Path.Combine(ReportDirectory, "packed-assets.tsv"), assets.Length > 0
                    ? assets.Select(asset => asset.Bytes + "\t" + asset.Path)
                    : new[] { "# No packed asset details returned by Unity for this build." });

                if (summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Beta Web build " + summary.result + "; inspect " + ReportDirectory);
                if (!File.Exists(Path.Combine(OutputDirectory, "index.html")))
                    throw new BuildFailedException("Unity reported success but the browser entry point is absent.");
                Debug.Log("[BetaWebBuild] Succeeded: " + OutputDirectory);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(ReportDirectory, "exception.txt"), exception.ToString());
                var statusPath = Path.Combine(ReportDirectory, "status.json");
                var status = JsonUtility.FromJson<Status>(File.ReadAllText(statusPath));
                status.result = "Failed";
                status.finishedUtc = DateTime.UtcNow.ToString("O");
                File.WriteAllText(statusPath, JsonUtility.ToJson(status, true));
                throw;
            }
        }

        private static void ValidateEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Wait until Unity is idle in Edit Mode before building the beta.");
        }

        [Serializable]
        private sealed class PipelineInclusion
        {
            public string label, defaultPipeline, currentQualityPipeline;
            public string[] included;
        }

        [Serializable]
        private sealed class Status
        {
            public string result, startedUtc, finishedUtc, output, unityVersion, productVersion;
            public string[] scenes;
            public int errors, warnings;
            public ulong bytes;
            public double seconds;
        }
    }
}
