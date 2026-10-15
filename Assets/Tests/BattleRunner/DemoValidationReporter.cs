using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Diceforge.Tests.BattleTermination
{
    // Register again after Enter/ExitPlayMode domain reloads so long campaign runs keep evidence.
    [InitializeOnLoad]
    internal sealed class DemoValidationReporter : ICallbacks
    {
        private static string DirectoryPath => SessionState.GetString("DemoValidationDirectory", "docs/Validation/ScratchyDemo");
        static DemoValidationReporter() => TestRunnerApi.RegisterTestCallback(new DemoValidationReporter());
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite || !SessionState.GetBool("ScratchyValidation", false)) return;
            Directory.CreateDirectory(DirectoryPath);
            File.AppendAllText(Path.Combine(DirectoryPath, "tests.log"),
                result.FullName + " | " + result.TestStatus + " | " + result.Message + "\n");
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool("ScratchyValidation", false)) return;
            Directory.CreateDirectory(DirectoryPath);
            TestRunnerApi.SaveResultToFile(result, Path.Combine(DirectoryPath, "latest-results.xml"));
        }
    }
}
