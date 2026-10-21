"""Restore source-facing outer hair polygons flipped by extrude_face_region."""
import bpy,bmesh,json
from pathlib import Path
BASE=Path('C:/Backforge/Tripo3D/work')
bpy.ops.wm.open_mainfile(filepath=str(BASE/'luma_v01/Luma_rig_source.blend'))
src=next(o for o in bpy.context.scene.objects if o.type=='MESH')
original={tuple(sorted(p.vertices)):p.normal.copy() for p in src.data.polygons}
bpy.ops.wm.open_mainfile(filepath=str(BASE/'cast_v05/sources/Luma_polish.blend'))
m=next(o for o in bpy.context.scene.objects if o.type=='MESH');rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
uv=m.data.uv_layers.active
before={(p.index,l.vertex_index):tuple(uv.data[i].uv) for p in m.data.polygons for i in p.loop_indices for l in [m.data.loops[i]]}
shapes={k.name:[v.co.copy() for v in k.data] for k in m.data.shape_keys.key_blocks}
bm=bmesh.new();bm.from_mesh(m.data);bm.faces.ensure_lookup_table();bm.normal_update()
faces=[f for f in bm.faces if tuple(sorted(v.index for v in f.verts)) in original and original[tuple(sorted(v.index for v in f.verts))].dot(f.normal)<0]
assert len(faces)==1180,len(faces)
bmesh.ops.reverse_faces(bm,faces=faces);bm.to_mesh(m.data);bm.free();m.data.update()
uv=m.data.uv_layers.active
for p in m.data.polygons:
 for i in p.loop_indices:
  assert (uv.data[i].uv-__import__('mathutils').Vector(before[p.index,m.data.loops[i].vertex_index])).length<1e-7
for k in m.data.shape_keys.key_blocks:
 assert all((v.co-shapes[k.name][i]).length<1e-7 for i,v in enumerate(k.data))
out=BASE/'luma_v03';out.mkdir(exist_ok=True)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(out/'Luma_v03.blend'))
bpy.ops.object.select_all(action='DESELECT');m.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(out/'Luma_v03.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='COPY')
(out/'surface-repair.json').write_text(json.dumps(dict(flippedOuterHairPolygons=len(faces),vertices=len(m.data.vertices),uvPreserved=True,blendshapesPreserved=True,animationNames=[a.name for a in bpy.data.actions]),indent=2))
