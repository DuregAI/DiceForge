import bpy, json, shutil, math
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/Backforge/Diceforge/DiceForge');RAW=Path('C:/Backforge/Tripo3D/work/forest_v01/tripo-oak')
OUT=ROOT/'Assets/_Project/07_Art/LivingForest';DOC=ROOT/'docs/Art/MapDesign/Production-v01'
(OUT/'Textures').mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(next(RAW.glob('*.fbx'))))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
scale=2.65/(hi.z-lo.z);center=Vector(((lo.x+hi.x)*.5,(lo.y+hi.y)*.5,lo.z))
images=[i for i in bpy.data.images if i.size[0]>1 and i.name!='Render Result']
image=max(images,key=lambda x:x.size[0]*x.size[1]);pixels=list(image.pixels);w,h=image.size
texture=next(RAW.rglob('*.png'),None) or next(RAW.rglob('*.jpg'))
texture_out=OUT/'Textures'/('OakColor'+texture.suffix);shutil.copy2(texture,texture_out)
image.filepath=str(texture_out)
mats=[]
for name in ('OakBark','OakLeaves'):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.82
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
    mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color']);mats.append(mat)
leaf_faces=0
for obj in objects:
    world=obj.matrix_world.copy()
    for v in obj.data.vertices:v.co=(world@v.co-center)*scale
    obj.matrix_world.identity();obj.data.materials.clear()
    for mat in mats:obj.data.materials.append(mat)
    colors=obj.data.color_attributes.new(name='Wind',type='BYTE_COLOR',domain='CORNER')
    uv=obj.data.uv_layers.active
    for p in obj.data.polygons:
        u=sum(uv.data[li].uv.x for li in p.loop_indices)/len(p.loop_indices)
        v=sum(uv.data[li].uv.y for li in p.loop_indices)/len(p.loop_indices)
        index=(min(h-1,int((v%1)*h))*w+min(w-1,int((u%1)*w)))*4
        r,g,b=pixels[index:index+3];leaf=g>r*1.08 and g>b*1.15
        p.material_index=1 if leaf else 0;p.use_smooth=True
        if leaf:leaf_faces+=1
        for li in p.loop_indices:
            co=obj.data.vertices[obj.data.loops[li].vertex_index].co
            weight=max(0,min(1,co.z/2.65))**1.8*(1 if leaf else .18)
            colors.data[li].color=(weight,.65,.5,1)
    obj.name='Oak_Tripo_A'
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
fbx=OUT/'Models/Oak_Tripo_A.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
    add_leaf_bones=False,bake_anim=False,apply_scale_options='FBX_SCALE_ALL',path_mode='COPY',embed_textures=False)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(DOC/'Oak_Tripo_A.blend'))
report={'provider':'Tripo Studio P2.0','modelId':'b38b23a6-f8a6-45d3-88a3-de580026abdc','privacy':'Private',
        'credits':{'geometry':100,'texture':30},'height':2.65,'leafFaces':leaf_faces,
        'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
        'texture':[w,h],'textureFile':str(texture_out),'split':'bark and leaves; height-weighted vertex wind mask',
        'sourceZip':'C:/Users/tehno/Downloads/Glimblehop_Oak_v01.zip'}
(DOC/'tripo-oak.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
