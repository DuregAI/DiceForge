"""Sculpted island base and stream assets in the source Blender scene."""
import bpy, math, random
ROOT='C:/Backforge/Diceforge/DiceForge'
OUT=ROOT+'/Assets/_Project/07_Art/WoodlandDiorama'
scene=bpy.context.scene
atlas=bpy.data.materials['WoodlandAtlas']
names=sorted(m.name for m in bpy.data.materials if m.name.startswith('WD_'))
random.seed(301)
def export(name,verts,faces,colors):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.materials.append(atlas)
    uv=mesh.uv_layers.new(name='UVMap')
    for i,p in enumerate(mesh.polygons):
        idx=names.index('WD_'+colors[i])
        for li in p.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv=((idx%8+.15+(co.x*.53%1)*.70)/8,(idx//8+.15+(co.z*.73%1)*.70)/8)
    o=bpy.data.objects.new(name,mesh);scene.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bevel=o.modifiers.new('Weathered edges','BEVEL');bevel.width=.025;bevel.segments=2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False)
    o.location=(24,0,0)
v=[];f=[];c=[];n=64
for j,(radius,z) in enumerate([(1,0), (1.015,-.08),(.99,-.28),(.91,-.65)]):
    for i in range(n):
        a=i*math.tau/n;r=radius*(1+.012*math.sin(a*9)+.009*math.cos(a*13))
        v.append((math.cos(a)*r,math.sin(a)*r,z+(.012*math.sin(a*7) if j else 0)))
for j in range(3):
    for i in range(n):f.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i));c.append('Earth' if i%3 else 'Soil')
v.append((0,0,0));center=len(v)-1
for i in range(n):f.append((center,i,(i+1)%n));c.append('Moss')
export('Island',v,f,c)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/docs/Art/WoodlandDiorama/Woodland_SourceKit.blend')
print('Island exported')
