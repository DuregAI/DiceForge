using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Diceforge.Editor
{
    /// <summary>Install the checked-in bridge through UPM; verify without touching gameplay assets.</summary>
    public static class TripoBridgeSetup
    {
        private const string PendingKey = "Diceforge.TripoBridgeSetup.Pending";
        private static AddRequest request;
        private static double deadline;

        [MenuItem("Diceforge/Characters/Tripo/Install local bridge")]
        public static void Install()
        {
            if (request != null) return;
            string package = Path.GetFullPath("tools/ThirdParty/TripoBridge/package.json");
            if (!File.Exists(package)) throw new FileNotFoundException("Local bridge package is missing", package);
            SessionState.SetBool(PendingKey, true);
            deadline = EditorApplication.timeSinceStartup + 300;
            request = Client.Add("file:../tools/ThirdParty/TripoBridge");
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > deadline) Finish(false, "UPM installation timed out.");
                return;
            }
            Finish(request.Status == StatusCode.Success,
                request.Status == StatusCode.Success ? request.Result.name + "@" + request.Result.version : request.Error.message);
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Poll;
            request = null;
            SessionState.SetBool(PendingKey, false);
            if (success) Debug.Log("[Tripo setup] Installed " + message);
            else Debug.LogError("[Tripo setup] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            EditorApplication.delayCall += () =>
            {
                if (BridgeType("WebSocketSharpServer") != null) Finish(true, "bridge resolved after domain reload");
                else Finish(false, "Bridge assembly unavailable after reload.");
            };
        }

        private static Type BridgeType(string name) => Type.GetType(
            "Tripo3D.UnityBridge.Editor." + name + ", Tripo3D.UnityBridge.Editor");

        [MenuItem("Diceforge/Characters/Tripo/Open bridge (staging)")]
        public static void OpenBridge()
        {
            Type importer = BridgeType("ModelImporter");
            if (importer == null) throw new InvalidOperationException("Install the local Tripo bridge first.");
            importer.GetProperty("InstantiateOnImport").SetValue(null, false);
            BridgeType("TripoWebSocketWindow").GetMethod("ShowWindow").Invoke(null, null);
        }

        // Batch validation keeps the editor alive briefly for an external protocol smoke test.
        public static void ValidateReceiver()
        {
            Type serverType = BridgeType("WebSocketSharpServer");
            if (serverType == null) throw new InvalidOperationException("Tripo bridge did not compile.");
            Type importer = BridgeType("ModelImporter");
            importer.GetProperty("InstantiateOnImport").SetValue(null, false);
            importer.GetMethod("DetectAndSetRenderPipeline").Invoke(null, null);
            object server = Activator.CreateInstance(serverType);
            serverType.GetMethod("Start").Invoke(server, null);
            if (!(bool)serverType.GetProperty("IsRunning").GetValue(server))
                throw new InvalidOperationException("Tripo receiver did not start.");
            Debug.Log("[Tripo setup] RECEIVER_READY 127.0.0.1:60610 staging=true");
            double end = EditorApplication.timeSinceStartup + 90;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (EditorApplication.timeSinceStartup < end) return;
                EditorApplication.update -= tick;
                serverType.GetMethod("Stop").Invoke(server, null);
                Debug.Log("[Tripo setup] Receiver validation complete.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            };
            EditorApplication.update += tick;
        }
    }
}
