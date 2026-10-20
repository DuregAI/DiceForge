# Glimblehop local integration changes

Upstream: Tripo Bridge `com.tripo3d.unitybridge` 1.0.14, supplied by the user in `C:/Backforge/Tripo3D/integration/Tripo3d_Unity_Bridge-latest/Tripo3d_Unity_Bridge` on 2026-10-18. Original source directory remains unchanged.

- `ModelImporter.cs`: project-specific incoming folder; project-scoped `InstantiateOnImport`, default false; reject Play Mode intake.
- `TripoWebSocketWindow.cs`: expose scene placement toggle and incoming-folder guidance; dispatch worker UI callbacks through `BridgeMainThread`.
- `WebSocketSharpServer.cs`: enqueue status/progress/import callbacks on the editor update queue, replacing worker-thread writes to `EditorApplication.delayCall`.
- `BridgeMainThread.cs`: bounded main-thread queue drain on editor updates; catches and logs callback exceptions.
- `StartupCleanup.cs`: disable automatic registration of broad old-import/shared-temp cleanup. Explicit per-transfer cleanup remains in the importer.

Package version is retained to identify the supplied upstream version; this file records the project patch. Before upgrading, reapply the patch and rerun handshake, heartbeat, OBJ import, no-scene-change and URP checks. These editor-only files are not runtime gameplay dependencies.
