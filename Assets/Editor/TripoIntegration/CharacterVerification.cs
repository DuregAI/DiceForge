using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Diceforge.Editor
{
    // Register again after EnterPlayMode/domain reload so result capture remains available.
    [InitializeOnLoad]
    public static class CharacterVerification
    {
        private const string Active = "Diceforge.CharacterVerification.Active";
        private const string DirectoryPath = "docs/Validation/TishReady";
        static CharacterVerification() { TestRunnerApi.RegisterTestCallback(new Results()); }

        [MenuItem("Diceforge/Characters/Tripo/Validate Tish campaign")]
        public static void Run()
        {
            Directory.CreateDirectory(DirectoryPath);
            SessionState.SetString(Active + ".Report", "tests.xml");
            SessionState.SetBool(Active, true);
            SessionState.SetString("DemoCampaignCaptureDirectory", Path.GetFullPath(DirectoryPath + "/Campaign"));
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[]
                {
                    "Diceforge.Tests.BattleTermination.RuntimeServiceRecoveryTests.AudioAndDiagnosticsRecoverTheirServicesWithoutDuplicatingSources",
                    "Diceforge.Tests.BattleTermination.DemoCampaignFlowTests.MapToAllSixLevelsSavesExactlyOnceAndKeepsNamedHeroes",
                    "Diceforge.Tests.BattleTermination.DioramaHopPresentationTests.TishHopDoesNotBlendIntoRunningOrRestartIdleAtLanding",
                    "Diceforge.Tests.BattleTermination.DioramaHopPresentationTests.HopKeepsFormationScalePausesAndCommitsDestinationAfterContactRecovery",
                    "Diceforge.Tests.BattleTermination.DioramaHopPresentationTests.InterruptedHopResetsBodyAndReducedMotionHasNoLiftOrSquash"
                }
            }));
        }

        public static void RunServiceRecovery()
        {
            Directory.CreateDirectory(DirectoryPath);
            SessionState.SetString(Active + ".Report", "service-recovery.xml");
            SessionState.SetBool(Active, true);
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[] { "Diceforge.Tests.BattleTermination.RuntimeServiceRecoveryTests.AudioAndDiagnosticsRecoverTheirServicesWithoutDuplicatingSources" }
            }));
        }

        public static void RunFace()
        {
            Directory.CreateDirectory(DirectoryPath);
            SessionState.SetString(Active + ".Report", "facial-motion.xml");
            SessionState.SetBool(Active, true);
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[]
                {
                    "Diceforge.Tests.BattleTermination.RuntimeServiceRecoveryTests.TishFacialAccentsBlinkAndReactWithoutConsumingGameplayRandom",
                    "Diceforge.Tests.BattleTermination.RuntimeServiceRecoveryTests.ClosedEyelidsKeepTheirFrontFacesVisibleWithBackfaceCulling",
                    "Diceforge.Tests.BattleTermination.RuntimeServiceRecoveryTests.BothHeroesSpeakAndReturnToListeningAndRespectReducedMotion"
                }
            }));
        }

        public static void RunLayouts()
        {
            Directory.CreateDirectory(DirectoryPath);
            SessionState.SetString(Active + ".Report", "responsive-campaign.xml");
            SessionState.SetBool(Active + ".RestoreLayouts", SessionState.GetBool("DemoInterfaceCaptureLayouts", false));
            SessionState.SetBool("DemoInterfaceCaptureLayouts", true);
            SessionState.SetBool(Active, true);
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[] { "Diceforge.Tests.BattleTermination.DemoCampaignFlowTests.MapToAllSixLevelsSavesExactlyOnceAndKeepsNamedHeroes" }
            }));
        }

        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(Active, false)) return;
                SessionState.SetBool(Active, false);
                Directory.CreateDirectory(DirectoryPath);
                string report = SessionState.GetString(Active + ".Report", "tests.xml");
                if (report == "responsive-campaign.xml")
                    SessionState.SetBool("DemoInterfaceCaptureLayouts", SessionState.GetBool(Active + ".RestoreLayouts", false));
                TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(DirectoryPath + "/" + report));
                File.WriteAllText(DirectoryPath + "/" + Path.GetFileNameWithoutExtension(report) + "-summary.txt", "Passed=" + result.PassCount + "; Failed=" + result.FailCount + "; Skipped=" + result.SkipCount);
            }
        }
    }
}
