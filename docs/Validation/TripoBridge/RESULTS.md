# Tripo Bridge integration checks — 18 October 2026

- Unity 6000.6.0f1 / URP, local package `com.tripo3d.unitybridge` 1.0.14 with documented project patch.
- Blender 5.2.1 LTS, supplied Tripo Bridge 1.0.34 enabled persistently.
- Both receivers passed WebSocket handshake, ping/pong, single-chunk OBJ transfer and successful import completion. Wire responses are in `protocol-60600.json` and `protocol-60610.json`.
- Original Unity receiver acknowledged upload but postponed `delayCall` work until editor focus returned; requests timed out before import completion. Replaced worker callbacks with an editor-update queue. The final probe imported and acknowledged in approximately one second without an additional focus action.
- Unity console: zero error entries on the final check. Existing unrelated obsolete-API warnings are not part of this change.
- Unity auto scene placement false; current pipeline URP; Play Mode false. Scene root count remained 2; scene dirty flag remained false. No smoke-test GameObjects remained in the scene.
- Three synthetic OBJ triangles (3 vertices each) were verified, then archived outside Assets under `smoke-test-assets`. These are diagnostic fixtures, not game assets.
- Runtime character catalog SHA-256 before/after: `64B9876E1970BDEAA5060D77299FD0FCA4A8CE00BFD9A3A3AACD8790FD46B1D8`.
- A real FBX named `fantasy goblin on ladybug 3d model` arrived from Tripo Studio into Blender. It was preserved in `C:/Backforge/Tripo3D/work/Tripo-received-2026-10-18.blend`; reported mesh vertex count 9514. This is evidence of transport/import, not character or rig approval.
- Tish images visually reviewed and final ear asymmetry corrected. Final PNG files: turnaround 1536×1024, single front 1145×1374. HTML local links and JSON files checked. Images and prompt history use the built-in imagegen route.

Limits: Unity's full live Studio-to-Unity FBX-with-animation path was not exercised; local protocol validation used OBJ. No new Tish mesh, rig, animation or gameplay replacement was produced in this setup step. Exact orthographic dimensions must be established from one model. The existing rigged-FBX character importer still applies directly to the active catalog; candidate/apply separation remains a next integration task.
