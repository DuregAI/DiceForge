using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Diceforge.View.Editor
{
    [InitializeOnLoad]
    public static class WoodlandValidationRunner
    {
        private const string Active="Woodland.Validation.Active";
        private const string Folder="docs/Art/WoodlandDiorama/Validation";
        private static readonly TestRunnerApi Api;
        static WoodlandValidationRunner()
        {
            Api=ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.RegisterCallbacks(new Results());
        }
        [MenuItem("Diceforge/Woodland/Run regression tests")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/progress.txt","Starting\n");
            SessionState.SetBool(Active,true);
            Api.Execute(new ExecutionSettings(new Filter {testMode=TestMode.EditMode,assemblyNames=new[]{"Diceforge.TokenPlacement.Tests","Diceforge.Tests.EditMode","Diceforge.BattleRunner.Tests","Diceforge.Progression.Tests"}}));
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun){}
            public void TestStarted(ITestAdaptor test){if(SessionState.GetBool(Active,false))File.AppendAllText(Folder+"/progress.txt","START "+test.FullName+"\n");}
            public void TestFinished(ITestResultAdaptor result){if(SessionState.GetBool(Active,false))File.AppendAllText(Folder+"/progress.txt",result.Test.FullName+": "+result.TestStatus+" "+result.Message+"\n");}
            public void RunFinished(ITestResultAdaptor result)
            {
                if(!SessionState.GetBool(Active,false))return;
                TestRunnerApi.SaveResultToFile(result,Folder+"/results.xml");
                File.AppendAllText(Folder+"/progress.txt",$"COMPLETE passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}\n");
                SessionState.SetBool(Active,false);
            }
        }
    }
}
