# Tish v03 / Luma v02 — Unity integration

Current master files and review gallery: `C:/Backforge/Tripo3D/work/cast_v03/index.html`.

Both characters use a Generic 65-bone rig and the same animation controller contract:
`DialogueMode` 0 = scene idle, 1 = listen, 2 = speak. Gesture triggers: `Greet`, `Agree`, `Laugh`, `Doubt`.
The seven clips have common endpoint poses, planted feet, and no root motion. Board controllers play only `Idle_Scene`; `BoardLayoutTokenMover` retains the existing hop presentation. The chapter map retains the 2D atlas hero.

Both meshes have independent `Blink`, `LookLeft`, `LookRight`, `BrowRaise`, `MouthOpen`, `Smile`, and `BrowConcern` blendshapes. `TishFaceMotion` is the shared runtime driver for both characters. It uses its own random source, normal time on the board, unscaled time in dialogue, and respects reduced motion. Speech accents are stylized and are not audio/phoneme lip sync.

Tish's right hip satchel, buckles and loose strap ends (1,073 vertices) follow the pelvis. The diagonal strap (148 vertices) follows the torso. None of these vertices retain arm or hand influences. A sweep of all seven clips measured zero satchel motion relative to the hip.

Luma retains the approved Jo face, side braid, intact ears, warm olive-gold skin and left hip satchel. The hair back has more depth, thin hair regions have an underside, hair weights no longer follow arms, and satchel weights exclude fingers. Source body polygon winding is preserved: a global normal recalculation on this open mesh flips face/body shells inward. Only new inward hair surfaces are reversed. Eyelid winding is verified separately under Unity backface culling.

## Rebuild

1. Run `C:/Backforge/Tripo3D/work/polish_tish_v03.py` and `polish_luma_v02.py` with Blender 5.2.
2. Copy exported FBXs to the stable Tish source path and `Incoming/Tripo/Luma_v02` respectively; preserve Unity `.meta` files.
3. Run `TishDialogueSetup.Build()`, `TishDialogueSetup.ApplyGameplay()`, `LumaDialogueSetup.Build()`, and `LumaDialogueSetup.ApplyGameplay()` in Unity.
4. Run `CharacterVerification.RunFace()` and `CharacterVerification.Run()`. Native XML results are captured after domain reload; inspect `docs/Validation/CastReady/validation-summary.json` and the copied XML files.
5. Capture actual Unity neutral/speech/blink poses using `C:/Backforge/Tripo3D/work/capture_unity_faces.cs.txt`. CPU baking is intentional for editor screenshots: several `Camera.Render()` calls in one editor frame may reuse the same skinned pose.

Blender files contain packed textures. FBX exports retain adjacent `.fbm` texture directories. Character identities and asset GUIDs remain stable. Final art approval and target-device/WebGL profiling remain outstanding.
