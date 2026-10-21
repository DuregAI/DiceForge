"""Small reusable close-up assets. Existing gameplay/forest meshes are untouched."""
import bpy,bmesh,math,random,json
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/Backforge/Diceforge/DiceForge')
DOC=ROOT/'docs/Art/MapDesign/Production-v01'
OUT=ROOT/'Assets/_Project/07_Art/LivingForest/Models'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
objects=[]
with bpy.data.libraries.load(str(DOC/'Oak_Tripo_A.blend'),link=False) as (src,dst):dst.objects=['Oak_Tripo_A']
oak=dst.objects[0]
for name,twist in [('CanopyShrub_A',0),('CanopyShrub_B',.7)]:
    data=oak.data.copy();obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    bm=bmesh.new();bm.from_mesh(data)
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.z<1.65],context='VERTS')
    for v in bm.verts:
        p=v.co.copy();x=(p.x*math.cos(twist)-p.y*math.sin(twist))*.34;y=(p.x*math.sin(twist)+p.y*math.cos(twist))*.34
        v.co=(x,y,(p.z-1.65)*.32)
    bm.to_mesh(data);bm.free();data.update()
    colors=data.color_attributes.active_color
    for loop in data.loops:
        p=data.vertices[loop.vertex_index].co
        colors.data[loop.index].color=(min(1,p.z/.55)*.6,.55,.5,1)
    objects.append(obj)
bpy.data.objects.remove(oak,do_unlink=True)

# All droplets share one mesh/material. UV2 stores velocity, color stores phase.
rng=random.Random(21026);verts=[];faces=[];uvs=[];vel=[];col=[]
for j in range(28):
    a=rng.random()*math.tau;speed=rng.uniform(.20,.62);phase=rng.random();size=rng.uniform(.009,.022);lift=rng.uniform(.5,.9)
    center=Vector((-2.20+rng.uniform(-.11,.10),2.69+rng.uniform(-.07,.06),-.713))
    side=Vector((math.cos(a),math.sin(a),0))*size
    up=Vector((0,0,size*1.7));idx=len(verts)
    for q,uv in zip((-side-up,side-up,side+up,-side+up),((0,0),(1,0),(1,1),(0,1))):
        verts.append(center+q);uvs.append(uv);vel.append((math.cos(a)*speed,math.sin(a)*speed));col.append((phase,lift,.5,1))
    faces.append((idx,idx+1,idx+2,idx+3))
data=bpy.data.meshes.new('WaterImpactSpray_A');data.from_pydata(verts,[],faces);data.update()
obj=bpy.data.objects.new('WaterImpactSpray_A',data);bpy.context.collection.objects.link(obj)
mat=bpy.data.materials.new('ForestSpray');data.materials.append(mat)
for name,values in [('UVMap',uvs),('Velocity',vel)]:
    layer=data.uv_layers.new(name=name)
    for loop in data.loops:layer.data[loop.index].uv=values[loop.vertex_index]
color=data.color_attributes.new(name='Phase',type='FLOAT_COLOR',domain='CORNER')
for loop in data.loops:color.data[loop.index].color=col[loop.vertex_index]
objects.append(obj)
manifest=[]
for obj in objects:
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=str(OUT/(obj.name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,apply_scale_options='FBX_SCALE_ALL')
    manifest.append(dict(name=obj.name,triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),materials=[m.name for m in obj.data.materials]))
bpy.ops.wm.save_as_mainfile(filepath=str(DOC/'LivingForest_BetaDetails.blend'))
(DOC/'beta-detail-manifest.json').write_text(json.dumps(manifest,indent=2))
print('BETA_DETAILS',json.dumps(manifest))
