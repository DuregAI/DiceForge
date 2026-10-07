"""Reimport the exported FBX files in a separate background Blender process."""
import bpy
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
ART=ROOT/"docs/Art/DemoRCCharacters"
MODELS=ROOT/"Assets/_Project/07_Art/DemoRCCharacters/Models"
PARENTS={"Root":None,"Hips":"Root","Torso":"Hips","Head":"Torso",
         "Arm.L":"Torso","Arm.R":"Torso","Leg.L":"Hips","Leg.R":"Hips"}
records=[]
for key in ("tish","luma","bum","ryzh","bark"):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(MODELS/(key+".fbx")))
    armatures=[o for o in bpy.data.objects if o.type=="ARMATURE"]
    meshes=[o for o in bpy.data.objects if o.type=="MESH"]
    assert len(armatures)==1 and len(meshes)==1,(key,len(armatures),len(meshes))
    arm=armatures[0];mesh=meshes[0]
    hierarchy={b.name:b.parent.name if b.parent else None for b in arm.data.bones}
    assert hierarchy==PARENTS,(key,hierarchy)
    assert arm.name=="GoblinFriend_Rig",(key,arm.name)
    assert len(mesh.data.materials)==1,(key,len(mesh.data.materials))
    assert len(mesh.data.uv_layers)==1,(key,len(mesh.data.uv_layers))
    assert arm.animation_data and arm.animation_data.action,(key,"missing animation")
    missing=[]
    for v in mesh.data.vertices:
        total=sum(g.weight for g in v.groups)
        if abs(total-1)>.00001:missing.append(v.index)
    assert not missing,(key,"incorrect skin weights",len(missing))
    for mat in mesh.data.materials:
        images=[n.image for n in mat.node_tree.nodes if n.type=="TEX_IMAGE" and n.image]
        assert len(images)>=2,(key,"missing texture binding")
        assert all(Path(bpy.path.abspath(i.filepath)).exists() for i in images),(key,"texture path")
    mesh.data.calc_loop_triangles()
    records.append({"id":key,"passed":True,"triangles":len(mesh.data.loop_triangles),
        "bones":hierarchy,"mesh":mesh.name,"materials":len(mesh.data.materials),
        "animation":arm.animation_data.action.name,"all_vertices_weighted":True,
        "uv_layers":len(mesh.data.uv_layers),"external_textures_found":True})
result={"blender":bpy.app.version_string,"passed":len(records),"total":5,"characters":records}
(ART/"fbx_validation.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
print("DEMO_FBX_VALIDATION "+json.dumps(result))
