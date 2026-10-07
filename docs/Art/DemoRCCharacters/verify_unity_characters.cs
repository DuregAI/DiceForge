// Run through Unity CLI eval after BuildCandidates, outside Play Mode.
var ids = new[] { "tish", "luma", "bum", "ryzh", "bark" };
var walk = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>(
    "Assets/_Project/07_Art/DemoRCCharacters/Animations/CharacterWalk.anim");
var bindings = UnityEditor.AnimationUtility.GetCurveBindings(walk);
if (bindings.Length != 32 || bindings.Any(b => !b.propertyName.StartsWith("m_LocalRotation.")))
    throw new System.InvalidOperationException("Walk must contain only eight quaternion rotations.");
var models = ids.Select(id => {
    string name = char.ToUpperInvariant(id[0]) + id.Substring(1);
    string path = "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/" + name + ".prefab";
    var go = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path));
    try {
        var animator = go.GetComponentInChildren<UnityEngine.Animator>();
        var skins = go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>();
        var skin = skins.Single();
        if (skin.bones.Length != 8 || skin.bones.Any(b => b == null) || skin.sharedMaterials.Length != 1 || animator.applyRootMotion)
            throw new System.InvalidOperationException("Invalid candidate rig or material setup: " + id);
        var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
        var idle = (UnityEngine.AnimationClip)controller.layers[0].stateMachine.states
            .Single(s => s.state.name == "Idle").state.motion;
        idle.SampleAnimation(animator.gameObject, idle.length * .23f);
        var positions = skin.bones.Select(b => b.localPosition).ToArray();
        var scales = skin.bones.Select(b => b.localScale).ToArray();
        var rotations = skin.bones.Select(b => b.localRotation).ToArray();
        var move = (UnityEngine.AnimationClip)controller.layers[0].stateMachine.states
            .Single(s => s.state.name == "Walk").state.motion;
        move.SampleAnimation(animator.gameObject, move.length * .23f);
        float positionDelta = skin.bones.Select((b, i) => UnityEngine.Vector3.Distance(b.localPosition, positions[i])).Max();
        float scaleDelta = skin.bones.Select((b, i) => UnityEngine.Vector3.Distance(b.localScale, scales[i])).Max();
        float rotationDelta = skin.bones.Select((b, i) => UnityEngine.Quaternion.Angle(b.localRotation, rotations[i])).Max();
        if (positionDelta > .00001f || scaleDelta > .00001f || (id != "bark" && rotationDelta < 1f))
            throw new System.InvalidOperationException("Walk changes proportions or does not animate: " + id);
        var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(
            "Assets/_Project/07_Art/DemoRCCharacters/Models/" + id + ".fbx");
        if (!importer.clipAnimations.All(c => c.loopTime))
            throw new System.InvalidOperationException("Idle loop is not persisted: " + id);
        return new { id, path, bones = skin.bones.Select(b => b.name).ToArray(),
            materials = skin.sharedMaterials.Length, vertices = skin.sharedMesh.vertexCount,
            triangles = skin.sharedMesh.GetIndexCount(0) / 3, positionDelta, scaleDelta, rotationDelta,
            idle = idle.name, walk = move.name, modelScale = animator.transform.localScale.x };
    }
    finally { UnityEngine.Object.DestroyImmediate(go); }
}).ToArray();
var catalog = UnityEngine.Resources.Load<Diceforge.Dialogue.DemoNarrativeCatalog>("DemoRC/Narrative");
if (catalog.speakers.Length != 12 || catalog.speakers.Any(s => s.neutralPortrait == null || s.neutralPortrait.name != s.id + "_neutral"))
    throw new System.InvalidOperationException("Twelve matching portrait references are required.");
var report = new { models, walkRotationBindings = bindings.Length,
    portraits = catalog.speakers.Select(s => new { s.id, sprite = s.neutralPortrait.name, rect = s.neutralPortrait.rect.ToString() }).ToArray() };
System.IO.File.WriteAllText("docs/Validation/DemoRCStage5/unity-assets.json",
    Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
return report;
