using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Diceforge.EditorTools
{
    public static class BetaPassValidation
    {
        private const string ResultsDirectory = "docs/Validation/BetaPass1";

        public static void RunCoreTests()
        {
            RunTests(ResultsDirectory);
        }

        public static void RunAdminPassTests()
        {
            RunTests("docs/Validation/BetaAdmin");
        }

        private static void RunTests(string directory)
        {
            Directory.CreateDirectory(directory);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callback = new ResultsCallback(directory);
            api.RegisterCallbacks(callback);
            try
            {
                api.Execute(new ExecutionSettings(new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = new[] { "Diceforge.Progression.Tests", "Diceforge.BattleRunner.Tests", "Diceforge.TokenPlacement.Tests" }
                }) { runSynchronously = true });
            }
            finally
            {
                api.UnregisterCallbacks(callback);
                Object.DestroyImmediate(api);
            }
        }

        public static void QueueBrowserBuild()
        {
            EditorApplication.delayCall += () =>
            {
                Directory.CreateDirectory(ResultsDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    locationPathName = "Builds/WebGLBeta",
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                File.WriteAllText(Path.Combine(ResultsDirectory, "browser-build.txt"),
                    $"{summary.result}: errors={summary.totalErrors}, warnings={summary.totalWarnings}, bytes={summary.totalSize}, duration={summary.totalTime}");
                Debug.Log("[BetaPass] Browser build: " + summary.result);
            };
        }

        public static string BuildSecondPass()
        {
            return BuildBrowserPass("docs/Validation/BetaPass2");
        }

        public static string BuildAdminPass()
        {
            return BuildBrowserPass("docs/Validation/BetaAdmin");
        }

        public static string BuildFeedbackPass()
        {
            return BuildBrowserPass("docs/Validation/BetaFeedback");
        }

        private static string BuildBrowserPass(string directory)
        {
            Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = "Builds/WebGLBeta",
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.DetailedBuildReport
            });
            var summary = report.summary;
            string result = $"{summary.result}: errors={summary.totalErrors}, warnings={summary.totalWarnings}, bytes={summary.totalSize}, duration={summary.totalTime}";
            File.WriteAllText(Path.Combine(directory, "browser-build.txt"), result);
            var assets = report.packedAssets.SelectMany(pack => pack.contents)
                .GroupBy(asset => asset.sourceAssetPath)
                .Select(group => new { Path = group.Key, Bytes = group.Sum(asset => (long)asset.packedSize) })
                .OrderByDescending(asset => asset.Bytes);
            File.WriteAllLines(Path.Combine(directory, "final-assets.tsv"),
                assets.Any() ? assets.Select(asset => asset.Bytes + "\t" + asset.Path)
                    : new[] { "# Unity returned no packed asset details for this incremental build; this does not mean zero assets." });
            File.WriteAllLines(Path.Combine(directory, "build-messages.txt"),
                report.steps.SelectMany(step => step.messages).Select(message => message.type + ": " + message.content));
            return result;
        }

        private sealed class ResultsCallback : ICallbacks
        {
            private readonly string _directory;
            internal ResultsCallback(string directory) { _directory = directory; }
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, Path.Combine(_directory, "core-tests.xml"));
                File.WriteAllText(Path.Combine(_directory, "core-tests.txt"),
                    $"{result.ResultState}: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}");
            }
        }
    }
}
