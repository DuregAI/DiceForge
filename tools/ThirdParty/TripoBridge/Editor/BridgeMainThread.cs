using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Tripo3D.UnityBridge.Editor
{
    /// <summary>Drain socket callbacks on editor updates, even without a focused inspector.</summary>
    [InitializeOnLoad]
    internal static class BridgeMainThread
    {
        private static readonly ConcurrentQueue<Action> Pending = new ConcurrentQueue<Action>();
        private static readonly int MainThreadId;

        static BridgeMainThread()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += Drain;
        }

        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == MainThreadId;

        public static void Post(Action action) => Pending.Enqueue(action);

        private static void Drain()
        {
            for (int i = 0; i < 64 && Pending.TryDequeue(out Action action); i++)
            {
                try { action(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }
}
