import bpy,bmesh,os,sys,json,math
from mathutils import Vector,Quaternion,Matrix
sys.path.insert(0,'C:/Backforge/Tripo3D/work')
from character_face import face
ROOT='C:/Backforge/Tripo3D/work/luma_v02';os.makedirs(ROOT,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath='C:/Backforge/Tripo3D/work/luma_v01/Luma_rig_source.blend')
s=bpy.context.scene;rig=next(o for o in s.objects if o.type=='ARMATURE');m=next(o for o in s.objects if o.type=='MESH')
s.render.fps=24
native=rig.animation_data.action;s.frame_set(1)
base={b.name:(b.location.copy(),b.rotation_quaternion.copy(),b.scale.copy()) for b in rig.pose.bones}
rig.animation_data_clear()
for a in list(bpy.data.actions):bpy.data.actions.remove(a)
uv=m.data.uv_layers.active;im=next(n.image for n in m.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE');px=list(im.pixels);tw,th=im.size;colors={}
for loop in m.data.loops:
 u=uv.data[loop.index].uv;idx=(int(u.y*th)%th*tw+int(u.x*tw)%tw)*4;colors[loop.vertex_index]=px[idx:idx+3]
points=[m.matrix_world@v.co for v in m.data.vertices]
hair=[];bag=[]
for i,p in enumerate(points):
 c=colors[i]
 isHair=c[0]<.42 and c[1]<.29 and p.z>.37 and (p.y>.065 or p.z>.87 or (abs(p.x)>.12 and p.z>.52))
 if isHair:
  hair.append(i)
  # Round the back into broad, staggered locks; preserve the established front/braid.
  if p.y>.08 and p.z<.94:
   fall=math.sin(math.pi*max(0,min(1,(p.z-.36)/.60)))**2
   ridge=.5+.5*math.cos(p.x*54+math.sin(p.z*14)*1.2)
   p.y+=fall*(.016+.015*ridge)*max(0,min(1,(p.y-.08)/.06))
   m.data.vertices[i].co=m.matrix_world.inverted()@p
  # Hair is owned by the head/upper torso, never the arms or fingers.
  for g in m.vertex_groups:
   if g.name.startswith('mixamorig:'):g.remove([i])
  h=max(.32,min(1,(p.z-.40)/.40))
  m.vertex_groups['mixamorig:Head'].add([i],h,'REPLACE');m.vertex_groups['mixamorig:Spine2'].add([i],1-h,'REPLACE')
 elif .33<p.z<.466 and .076<p.x<.21 and p.y<-.146:
  bag.append(i)
  for g in m.vertex_groups:
   if g.name.startswith('mixamorig:'):g.remove([i])
  m.vertex_groups['mixamorig:Hips'].add([i],1,'REPLACE')
 elif p.z>.724 and abs(p.x)<.31:
  for g in m.vertex_groups:
   if g.name.startswith('mixamorig:'):g.remove([i])
  m.vertex_groups['mixamorig:Head'].add([i],1,'REPLACE')
group=m.vertex_groups.new(name='HairPolish');group.add(hair,1,'REPLACE')
smooth=m.modifiers.new('Soften hair spikes','SMOOTH');smooth.factor=.15;smooth.iterations=3;smooth.vertex_group=group.name
# Smooth in rest space before skinning.
bpy.context.view_layer.objects.active=m
while m.modifiers.find(smooth.name)>0:bpy.ops.object.modifier_move_up(modifier=smooth.name)
bpy.ops.object.modifier_apply(modifier=smooth.name)
# Give paper-thin hair flaps an underside and a rounded edge without subdividing the body.
bm=bmesh.new();bm.from_mesh(m.data);bm.verts.ensure_lookup_table();bm.normal_update()
hairSet=set(hair);hairFaces=[f for f in bm.faces if all(v.index in hairSet for v in f.verts)]
normalByCo={tuple(v.co):v.normal.copy() for f in hairFaces for v in f.verts}
originalFaces=set(bm.faces)
originalNormals={f:f.normal.copy() for f in originalFaces}
extruded=bmesh.ops.extrude_face_region(bm,geom=hairFaces,use_keep_orig=True)
for v in extruded['geom']:
 if isinstance(v,bmesh.types.BMVert):v.co-=normalByCo.get(tuple(v.co),v.normal)*.003
# The source has open/overlapping surfaces. Recalculating the entire character would
# flip some face/body shells inward (Blender displays both sides; Unity culls them).
# Preserve every original polygon and reverse only the new inward extrusion.
bmesh.ops.reverse_faces(bm,faces=[f for f in bm.faces if f not in originalFaces]);bm.normal_update()
# extrude_face_region(use_keep_orig=True) reverses the retained cap as well.
# Restore that outer cap instead of leaving both skins facing inward in Unity.
bmesh.ops.reverse_faces(bm,faces=[f for f in originalFaces if f.is_valid and f.normal.dot(originalNormals[f])<0]);bm.normal_update()
bm.to_mesh(m.data);bm.free();m.data.update()
facial=face(m,'Luma')
with bpy.data.libraries.load('C:/Backforge/Tripo3D/work/tish_v02/Tish_v02.blend',link=False) as (src,dst):
 dst.objects=['Armature'];dst.actions=list(src.actions)
source=dst.objects[0];s.collection.objects.link(source);source.name='Motion source'
actions=list(dst.actions)
def activate(r,a):
 r.animation_data_create();r.animation_data.action=a
 if len(a.slots):r.animation_data.action_slot=a.slots[0]
idle=next(a for a in actions if a.name=='Idle_Scene');activate(source,idle);s.frame_set(1)
sourceBase={b.name:b.rotation_quaternion.copy() for b in source.pose.bones}
corrections={n:(rig.data.bones[n].matrix_local.to_quaternion().inverted()@source.data.bones[n].matrix_local.to_quaternion()) for n in sourceBase}
samples={}
for a in actions:
 activate(source,a);frames=[]
 for f in range(1,int(a.frame_range[1])+1):
  s.frame_set(f);frames.append({b.name:b.rotation_quaternion.copy() for b in source.pose.bones})
 samples[a.name]=frames
bpy.data.objects.remove(source,do_unlink=True)
for a in actions:bpy.data.actions.remove(a)
report={}
for name,frames in samples.items():
 a=bpy.data.actions.new(name);a.use_fake_user=True;activate(rig,a)
 for f,pose in enumerate(frames,1):
  s.frame_set(f)
  for b in rig.pose.bones:
   n=b.name;b.rotation_mode='QUATERNION';b.location=base[n][0];b.scale=base[n][2]
   if any(k in n for k in ['Hips','UpLeg','Leg','Foot','Toe']):b.rotation_quaternion=base[n][1]
   else:
    delta=sourceBase[n].inverted()@pose[n];c=corrections[n]
    amount={'Dialogue_Talk':.82,'Dialogue_Laugh':.85,'Dialogue_Listen':.75,'Dialogue_Doubt':.65}.get(name,1)
    b.rotation_quaternion=base[n][1]@Quaternion().slerp(c@delta@c.inverted(),amount)
  bpy.context.view_layer.update()
  # Open the left low arm to clear Luma's left hip satchel, including its leaf charm.
  hand=rig.pose.bones['mixamorig:LeftHand'];w=max(0,min(1,(.58-hand.head.z)/.15))
  b=rig.pose.bones['mixamorig:LeftArm'];mat=b.matrix.copy();direction=(b.tail-b.head)+Vector((.046,-.025,0))*w
  q=(b.tail-b.head).normalized().rotation_difference(direction.normalized());b.matrix=Matrix.LocRotScale(mat.translation,q@mat.to_quaternion(),mat.to_scale())
  bpy.context.view_layer.update()
  for b in rig.pose.bones:
   b.keyframe_insert('location',frame=f,group=b.name);b.keyframe_insert('rotation_quaternion',frame=f,group=b.name);b.keyframe_insert('scale',frame=f,group=b.name)
 for layer in a.layers:
  for strip in layer.strips:
   for bagData in strip.channelbags:
    for curve in bagData.fcurves:
     for k in curve.keyframe_points:k.interpolation='LINEAR'
 activate(rig,a);s.frame_set(1);first={b.name:b.matrix.copy() for b in rig.pose.bones};feet={b.name:b.matrix.translation.copy() for b in rig.pose.bones if 'Foot' in b.name};drift=0
 for f in range(1,len(frames)+1):
  s.frame_set(f);drift=max(drift,max((rig.pose.bones[n].matrix.translation-v).length for n,v in feet.items()))
 report[name]=dict(frames=len(frames),seconds=(len(frames)-1)/24,maxFootDrift=drift,endpointPosition=max((b.matrix.translation-first[b.name].translation).length for b in rig.pose.bones))
activate(rig,bpy.data.actions['Idle_Scene']);s.frame_set(1);s.frame_start=1;s.frame_end=369
json.dump(dict(hairVertices=len(hair),satchelVertices=len(bag),facial=facial,animations=report),open(ROOT+'/production-report.json','w'),indent=2)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Luma_v02.blend')
bpy.ops.object.select_all(action='DESELECT');m.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=ROOT+'/Luma_v02.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='COPY')
print('LUMA_READY',json.dumps(report))
