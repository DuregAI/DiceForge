"""Normalize a Tripo prop and author material-separated, pinned wind weights."""
import bpy, json, shutil, sys
from pathlib import Path
from mathutils import Vector

kind=sys.argv[sys.argv.index('--')+1]
config={
    'fir':('Fir_Tripo_A','Fir',3.1,'3858d44e-11dd-4e48-955a-c52e470745ef','Glimblehop_Fir_v01.zip'),
    'rockgarden':('RockGarden_Tripo_A','Garden',.92,'06c6d5c6-137c-4e9e-bd99-6b795818da0b','Glimblehop_RockGarden_v01.zip')
}[kind]
name,prefix,height,model_id,zip_name=config
root=Path('C:/Backforge/Diceforge/DiceForge');raw=Path('C:/Backforge/Tripo3D/work/forest_v01')/('tripo-'+kind)
out=root/'Assets/_Project/07_Art/LivingForest';doc=root/'docs/Art/MapDesign/Production-v01'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(next(raw.glob('*.fbx'))))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
scale=height/(hi.z-lo.z);center=Vector(((lo.x+hi.x)*.5,(lo.y+hi.y)*.5,lo.z))
image=max([i for i in bpy.data.images if i.size[0]>1 and i.name!='Render Result'],key=lambda x:x.size[0]*x.size[1])
pixels=list(image.pixels);w,h=image.size
texture=next(raw.rglob('*.jpg'),None) or next(raw.rglob('*.png'))
texture_out=out/'Textures'/(prefix+'Color'+texture.suffix);shutil.copy2(texture,texture_out);image.filepath=str(texture_out)
mats=[]
for suffix in ('Body','Leaves'):
    mat=bpy.data.materials.new(prefix+suffix);mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.84
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
    mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color']);mats.append(mat)
foliage=0
for obj in objects:
    matrix=obj.matrix_world.copy()
    for v in obj.data.vertices:v.co=(matrix@v.co-center)*scale
    obj.matrix_world.identity();obj.data.materials.clear()
    for mat in mats:obj.data.materials.append(mat)
    colors=obj.data.color_attributes.new(name='Wind',type='BYTE_COLOR',domain='CORNER');uv=obj.data.uv_layers.active
    for polygon in obj.data.polygons:
        u=sum(uv.data[li].uv.x for li in polygon.loop_indices)/len(polygon.loop_indices)
        v=sum(uv.data[li].uv.y for li in polygon.loop_indices)/len(polygon.loop_indices)
        offset=(min(h-1,int((v%1)*h))*w+min(w-1,int((u%1)*w)))*4
        r,g,b=pixels[offset:offset+3];leaf=g>r*1.12 and g>b*1.18
        polygon.material_index=1 if leaf else 0;polygon.use_smooth=True
        if leaf:foliage+=1
        for li in polygon.loop_indices:
            co=obj.data.vertices[obj.data.loops[li].vertex_index].co
            # Rock moss stays attached. Only a tree's crown is allowed to flex.
            weight=max(0,min(1,co.z/height))**1.8*(1 if leaf else .14) if kind=='fir' else 0
            colors.data[li].color=(weight,.62,.5,1)
    obj.name=name
bpy.ops.object.select_all(action='DESELECT')
for obj in objects:obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(out/'Models'/(name+'.fbx')),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,apply_scale_options='FBX_SCALE_ALL',path_mode='COPY',embed_textures=False)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(doc/(name+'.blend')))
report={'provider':'Tripo Studio P2.0','modelId':model_id,'privacy':'Private','height':height,
    'credits':{'geometry':100,'texture':30},'leafFaces':foliage,
    'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
    'texture':[w,h],'sourceZip':str(Path('C:/Users/tehno/Downloads')/zip_name),
    'materials':[m.name for m in mats],'model':str(out/'Models'/(name+'.fbx'))}
(doc/('tripo-'+kind+'.json')).write_text(json.dumps(report,indent=2))
print(json.dumps(report))
