"""Bake an alternative outfit into Luma's existing atlas, with no FBX changes."""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector

ROOT=Path('C:/Backforge/Tripo3D/work/luma_outfit_v01')
bpy.ops.wm.open_mainfile(filepath='C:/Backforge/Tripo3D/work/luma_v03/Luma_v03.blend')
s=bpy.context.scene
m=next(o for o in s.objects if o.type=='MESH')
rig=next(o for o in s.objects if o.type=='ARMATURE')
rig.data.pose_position='REST'
source=next(n.image for n in m.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE')
mask=json.loads((ROOT/'face-mask.json').read_text())
atlas=bpy.data.images.new('Luma Forest Outfit BaseColor',width=2048,height=2048,alpha=False)
atlas.colorspace_settings.name='sRGB'
atlas.pixels[:]=source.pixels[:]
linen=bpy.data.images.load(str(ROOT/'Linen_Ivory_Source.png'))

def material(name,color):
    mat=bpy.data.materials.new(name);mat.use_nodes=True;mat.use_fake_user=True
    n=mat.node_tree.nodes;l=mat.node_tree.links;n.clear()
    def node(kind):return n.new(kind)
    def math_node(op,a,b):
        q=node('ShaderNodeMath');q.operation=op
        for i,v in enumerate((a,b)):
            if isinstance(v,(int,float)):q.inputs[i].default_value=v
            else:l.new(v,q.inputs[i])
        return q.outputs[0]
    original=node('ShaderNodeTexImage');original.image=source
    gray=node('ShaderNodeRGBToBW');l.new(original.outputs['Color'],gray.inputs[0])
    shade=math_node('ADD',math_node('MULTIPLY',math_node('SQRT',gray.outputs[0],0),.85),.45)
    weave=node('ShaderNodeTexImage');weave.image=linen;weave.projection='BOX';weave.projection_blend=.2
    coords=node('ShaderNodeTexCoord');scale=node('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs['Scale'].default_value=5
    l.new(coords.outputs['Object'],scale.inputs[0]);l.new(scale.outputs[0],weave.inputs['Vector'])
    bw=node('ShaderNodeRGBToBW');l.new(weave.outputs[0],bw.inputs[0])
    weaveShade=math_node('ADD',math_node('MULTIPLY',bw.outputs[0],.30),.83)
    shade=math_node('MULTIPLY',shade,weaveShade)
    base=node('ShaderNodeRGB');base.outputs[0].default_value=(*color,1)
    if name=='Teal linen':
        # Narrow warm linen hem. Geometry coordinates keep it continuous over UV islands.
        geo=node('ShaderNodeNewGeometry');sep=node('ShaderNodeSeparateXYZ');l.new(geo.outputs['Position'],sep.inputs[0])
        hem=math_node('MULTIPLY',math_node('GREATER_THAN',sep.outputs['Z'],.333),math_node('LESS_THAN',sep.outputs['Z'],.345))
        mix=node('ShaderNodeMixRGB');l.new(hem,mix.inputs[0]);l.new(base.outputs[0],mix.inputs[1]);mix.inputs[2].default_value=(.55,.45,.29,1)
        baseSocket=mix.outputs[0]
    else:baseSocket=base.outputs[0]
    multiply=node('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1
    l.new(baseSocket,multiply.inputs[1]);l.new(shade,multiply.inputs[2])
    # Preserve original skin, gold, bone and stray hair within boundary polygons.
    hsv=node('ShaderNodeSeparateColor');hsv.mode='HSV';l.new(original.outputs['Color'],hsv.inputs[0])
    hueLimit=.047 if name=='Berry scarf' else .105
    paint=math_node('MULTIPLY',math_node('LESS_THAN',hsv.outputs[0],hueLimit),math_node('GREATER_THAN',hsv.outputs[1],.38))
    if name=='Teal linen':paint=math_node('MULTIPLY',paint,math_node('LESS_THAN',hsv.outputs[2],.61))
    safe=node('ShaderNodeMixRGB');l.new(paint,safe.inputs[0]);l.new(original.outputs[0],safe.inputs[1]);l.new(multiply.outputs[0],safe.inputs[2])
    emission=node('ShaderNodeEmission');l.new(safe.outputs[0],emission.inputs[0])
    output=node('ShaderNodeOutputMaterial');l.new(emission.outputs[0],output.inputs['Surface'])
    target=node('ShaderNodeTexImage');target.image=atlas;n.active=target
    return mat

cloth=[material('Teal linen',(.022,.195,.175)),material('Berry scarf',(.24,.025,.066))]
# Bake only selected faces. Keeping the source image avoids rebaking face/hair/leather.
data=m.data.copy();bake=bpy.data.objects.new('Outfit bake surface',data);s.collection.objects.link(bake)
bake.matrix_world=m.matrix_world.copy()
data.materials.clear()
for mat in cloth:data.materials.append(mat)
bm=bmesh.new();bm.from_mesh(data);bm.faces.ensure_lookup_table()
for f in bm.faces:f.material_index=max(0,mask[f.index]-1)
bmesh.ops.delete(bm,geom=[f for f in bm.faces if mask[f.index]==0],context='FACES')
bm.to_mesh(data);bm.free()
bpy.ops.object.select_all(action='DESELECT');bake.select_set(True);bpy.context.view_layer.objects.active=bake
s.render.engine='CYCLES';s.cycles.samples=1;s.render.bake.use_clear=False;s.render.bake.margin=1;s.render.bake.use_selected_to_active=False
bpy.ops.object.bake(type='EMIT')
atlas.filepath_raw=str(ROOT/'Luma_ForestOutfit_BaseColor_2K.png');atlas.file_format='PNG';atlas.save()
bpy.data.objects.remove(bake,do_unlink=True)
# The native source includes the full editable rig, original map and alternative map.
body=m.data.materials[0].copy();body.name='Luma Forest Outfit'
for node in body.node_tree.nodes:
    if node.type=='TEX_IMAGE' and node.image==source:node.image=atlas
m.data.materials[0]=body
rig.data.pose_position='POSE'
for o in list(s.objects):
    if o.type in ('CAMERA','LIGHT'):bpy.data.objects.remove(o,do_unlink=True)
s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True
s.render.resolution_x=720;s.render.resolution_y=880;s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG';s.view_settings.view_transform='AgX'
s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.17,.19,.18,1);s.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.4
target=Vector((0,0,.52))
def aim(o):o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power in [('Key',(-2,-3,3),180),('Fill',(2,-2,2),90),('Rim',(0,3,2),120)]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.size=3;o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.location=loc;aim(o)
d=bpy.data.cameras.new('Outfit review');d.type='ORTHO';d.ortho_scale=1.2;cam=bpy.data.objects.new('Outfit review',d);s.collection.objects.link(cam);s.camera=cam
idle=bpy.data.actions['Idle_Scene'];rig.animation_data_create();rig.animation_data.action=idle;rig.animation_data.action_slot=idle.slots[0];s.frame_set(1)
for name,loc in [('front',(0,-3,.60)),('back',(0,3,.60)),('side',(-3,-.3,.60))]:
    cam.location=loc;aim(cam);s.render.filepath=str(ROOT/('outfit-'+name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Luma_ForestOutfit_v01.blend'))
(ROOT/'outfit-report.json').write_text(json.dumps(dict(source='Luma_v03.blend',texture=atlas.name,selectedFaces=sum(k>0 for k in mask),vertices=len(m.data.vertices),shapeKeys=len(m.data.shape_keys.key_blocks)-1,actions=[a.name for a in bpy.data.actions],fbxReexported=False),indent=2))
print('OUTFIT_BAKED')
