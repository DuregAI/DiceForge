import bpy, math, random
from mathutils import Vector
ROOT='C:/Backforge/Diceforge/DiceForge'
OUT=ROOT+'/Assets/_Project/07_Art/WoodlandDiorama'
scene=bpy.context.scene
materials=[m for m in bpy.data.materials if m.name.startswith('WD_')]
materials.sort(key=lambda m:m.name)
image=bpy.data.images.new('Woodland painted palette',width=512,height=512)
pixels=[0.0]*(512*512*4)
random.seed(3)
for y in range(512):
    for x in range(512):
        index=(y//64)*8+x//64
        col=materials[index].diffuse_color if index<len(materials) else (.3,.3,.3,1)
        u,v=x%64/64,y%64/64
        shade=.94+.06*math.sin(u*13+math.sin(v*12))+.035*random.random()+.04*v
        p=(y*512+x)*4;pixels[p:p+4]=[min(1,col[0]*shade),min(1,col[1]*shade),min(1,col[2]*shade),1]
image.pixels.foreach_set(pixels);image.filepath_raw=OUT+'/WoodlandPalette.png';image.file_format='PNG';image.save()
atlas=bpy.data.materials.new('WoodlandAtlas');atlas.use_nodes=True
node=atlas.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
atlas.node_tree.links.new(node.outputs['Color'],atlas.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
atlas.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.8
for o in scene.objects:
    if o.type!='MESH':continue
    uv=o.data.uv_layers.active or o.data.uv_layers.new(name='UVMap')
    for poly in o.data.polygons:
        m=o.data.materials[poly.material_index]
        if m not in materials:continue
        idx=materials.index(m)
        for li in poly.loop_indices:
            co=o.data.vertices[o.data.loops[li].vertex_index].co
            uv.data[li].uv=((idx%8+.15+(co.x*.73%1)*.70)/8,(idx//8+.15+(co.z*.63%1)*.70)/8)
        poly.material_index=0
    o.data.materials.clear();o.data.materials.append(atlas)
for root in list(scene.objects):
    if root.parent is not None or root.type not in ('MESH','ARMATURE'):continue
    saved=root.location.copy();root.location=(0,0,0)
    bpy.ops.object.select_all(action='DESELECT');root.hide_set(False);root.select_set(True)
    for o in root.children_recursive:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+root.name+'.fbx',use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=root.type=='ARMATURE',bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='AUTO')
    root.location=saved
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/docs/Art/WoodlandDiorama/Woodland_SourceKit.blend')
print('Palette atlas baked and FBX assets updated')
