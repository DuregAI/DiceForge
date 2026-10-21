import bpy,sys,json,math
from mathutils import Vector
root='C:/Backforge/Tripo3D/work/luma_outfit_v01'
bpy.ops.wm.open_mainfile(filepath='C:/Backforge/Tripo3D/work/luma_v03/Luma_v03.blend')
s=bpy.context.scene;m=next(o for o in s.objects if o.type=='MESH');rig=next(o for o in s.objects if o.type=='ARMATURE')
rig.data.pose_position='REST'
im=next(n.image for n in m.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE');px=list(im.pixels);w,h=im.size;uv=m.data.uv_layers.active
hairGroup=m.vertex_groups.get('HairPolish')
hair={v.index for v in m.data.vertices if hairGroup and any(g.group==hairGroup.index and g.weight>.01 for g in v.groups)}
masks=[]
for p in m.data.polygons:
 pos=sum((m.matrix_world@m.data.vertices[i].co for i in p.vertices),Vector())/len(p.vertices)
 col=Vector()
 for li in p.loop_indices:
  u=uv.data[li].uv;ix=(int(u.y*h)%h*w+int(u.x*w)%w)*4;col+=Vector(px[ix:ix+3])
 col/=len(p.vertices);x,y,z=pos
 scarf=.640<z<.721 and abs(x)<.13 and y<-.045 and col.x>col.y*1.30
 brown=.055<col.x<.58 and col.x>col.y*1.18 and col.y>col.z*1.10
 bag=.31<z<.47 and .07<x<.23 and y<-.125
 belt=.455<z<.495
 # Satchel strap crosses from right shoulder (front left) to left hip.
 strap=y<-.08 and abs(x-(.13-(z-.39)*.75))<.022 and z<.69
 dress=.29<z<.658 and abs(x)<(.16 if z<.455 else .108) and brown and not (bag or belt or strap) and not(y>.025 and z>.412)
 masks.append(0 if any(v in hair for v in p.vertices) else 2 if scarf else 1 if dress else 0)
# Fill isolated one/two-face gaps within selected cloth surfaces, never across a seam.
edges={}
for p in m.data.polygons:
 for e in p.edge_keys:edges.setdefault(tuple(sorted(e)),[]).append(p.index)
for iteration in range(2):
 updates={}
 for p in m.data.polygons:
  if masks[p.index] or any(v in hair for v in p.vertices):continue
  neighbors={j for e in p.edge_keys for j in edges[tuple(sorted(e))] if j!=p.index}
  for k in (1,2):
   if sum(masks[j]==k for j in neighbors)>=2:updates[p.index]=k
 for i,k in updates.items():masks[i]=k
print('MASKCOUNTS',json.dumps({v:masks.count(v) for v in set(masks)}));print('BOUNDS',[(min((m.matrix_world@v.co)[i] for v in m.data.vertices),max((m.matrix_world@v.co)[i] for v in m.data.vertices)) for i in range(3)])
for name,color in [('Original',(0.30,.25,.20,1)),('Dress',(.02,.60,.58,1)),('Scarf',(.65,.025,.18,1))]:
 mat=bpy.data.materials.new('Mask '+name);mat.diffuse_color=color;m.data.materials.append(mat)
for p,k in zip(m.data.polygons,masks):p.material_index=len(m.data.materials)-3+k
s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='MATERIAL';s.display.shading.show_shadows=True;s.display.shading.show_cavity=True
s.render.resolution_x=700;s.render.resolution_y=850;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.film_transparent=False
for o in list(s.objects):
 if o.type=='CAMERA':bpy.data.objects.remove(o,do_unlink=True)
cam=bpy.data.objects.new('Mask Camera',bpy.data.cameras.new('Mask Camera'));s.collection.objects.link(cam);s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.15
for name,loc in [('front',(0,-3,.53)),('back',(0,3,.53))]:
 cam.location=loc;cam.rotation_euler=(Vector((0,0,.5))-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=root+'/mask-'+name+'.png';bpy.ops.render.render(write_still=True)
json.dump(masks,open(root+'/face-mask.json','w'))
