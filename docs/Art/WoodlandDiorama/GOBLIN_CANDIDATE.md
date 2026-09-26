# Goblin friend: campaign prototype

The supplied front-view PNG is a visual reference only. Its gray background and white sticker outline are not imported into the mesh or materials.

- Blender source: `Goblin_Friend_Candidate.blend`.
- Rebuild: run `build_goblin_candidate.py` with Blender 5.2 in background.
- Unity FBX: `Assets/_Project/07_Art/GoblinFriendCandidate/GoblinFriend_Candidate.fbx`.
- Unity comparison scene: `Assets/_Project/06_Scenes/Art/FirstTrailGoblinCandidate.unity`; the new model stands on cell 2, the old goblin remains on cell 0. The comparison scene disables solo interaction and plays the candidate's looping idle.
- The main FBX contains a generic skinned rig in a T-pose bind state and a 2-second `GF_Idle` clip. `GoblinFriend_Walk.fbx` carries a 0.8-second movement reaction; the board mover supplies the jump arc. The preview's Animator Controller is `Assets/_Project/07_Art/GoblinFriendCandidate/GoblinFriend_IdlePreview.controller`.
- The campaign uses `GoblinFriend_Red.prefab` and `GoblinFriend_Blue.prefab`, with distinct scarf and scarf-fold materials, through `GoblinFriend_Gameplay.controller`. `GoblinLife` changes between `Idle` and `Walk` as the existing board mover starts and stops.
- Eight color and normal-map pairs live in `Assets/_Project/07_Art/GoblinFriendCandidate/Textures`. Unity imports the normal maps as NormalMap textures and assigns them to the FBX materials.
- Blender previews: `Captures/goblin-candidate-front.png`, `Captures/goblin-candidate-three-quarter.png`, `Captures/goblin-candidate-back.png`.
- Unity previews: `Captures/goblin-candidate-unity-idle.png` at board scale and `Captures/goblin-candidate-unity-close-sharp.png` for material inspection. The close view temporarily disabled depth of field in Play Mode; the saved scene keeps its original camera and volume.

This is an original manually modeled playable prototype, not a finished production character. Its procedural color/normal detail, idle and movement reaction work in Unity, but the face, ear volume, cloth joints, back silhouette and deformation still need sculpt and rig passes. It also needs authored selection, landing, hit, exit and win clips. The Blender generator avoids any paid or externally configured AI service; Tripo and Meshy are installed in Unity but have no configured provider key, and Blender's Hunyuan/Hyper3D integrations are disabled.
