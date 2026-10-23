import bpy
import math
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).parent
PROJECT = HERE.parents[3]
ART = PROJECT / 'Assets/_Project/07_Art/Characters/Finish'
bpy.ops.wm.open_mainfile(filepath='C:/Backforge/Tripo3D/work/cast_v05/sources/Scratchy_polish.blend')
scene = bpy.context.scene
mesh = next(o for o in scene.objects if o.type == 'MESH')
rig = next(o for o in scene.objects if o.type == 'ARMATURE')
for obj in list(scene.objects):
    if obj.type in {'LIGHT', 'CAMERA'}:
        bpy.data.objects.remove(obj, do_unlink=True)
action = bpy.data.actions['Idle_Scene']
rig.animation_data_create()
rig.animation_data.action = action
rig.animation_data.action_slot = action.slots[0]
scene.frame_set(1)
for key in mesh.data.shape_keys.key_blocks:
    key.value = 0
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 640
scene.render.resolution_y = 760
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.19, .22, .19, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45
target = Vector((0, -.02, .54))
def aim(obj):
    obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()
for name, location, energy in [('Key', (-2, -3, 3), 180), ('Fill', (2, -2, 2), 90), ('Rim', (0, 3, 2), 120)]:
    light = bpy.data.lights.new(name, 'AREA')
    light.energy = energy
    light.size = 3
    obj = bpy.data.objects.new(name, light)
    scene.collection.objects.link(obj)
    obj.location = location
    aim(obj)
camera_data = bpy.data.cameras.new('Review Camera')
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 1.22
camera = bpy.data.objects.new('Review Camera', camera_data)
scene.collection.objects.link(camera)
camera.location = (0, -3, .60)
aim(camera)
scene.camera = camera

# First render the unmodified production texture under exactly the same light.
scene.render.filepath = str(HERE / 'scratchy-before.png')
bpy.ops.render.render(write_still=True)
for index, material in enumerate(mesh.data.materials):
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    texture = nodes.new('ShaderNodeTexImage')
    texture.image = bpy.data.images.load(str(ART / 'Scratchy_Flax_BaseColor.png'), check_existing=True)
    links.new(texture.outputs['Color'], shader.inputs['Base Color'])
    shader.inputs['Roughness'].default_value = .90 if index == 0 else .48
    if index == 0:
        packed = nodes.new('ShaderNodeTexImage')
        packed.image = bpy.data.images.load(str(ART / 'Scratchy_Surface.png'))
        packed.image.colorspace_settings.name = 'Non-Color'
        channels = nodes.new('ShaderNodeSeparateColor')
        links.new(packed.outputs['Color'], channels.inputs['Color'])
        links.new(channels.outputs['Red'], shader.inputs['Metallic'])
        invert = nodes.new('ShaderNodeMath')
        invert.operation = 'SUBTRACT'
        invert.inputs[0].default_value = 1
        scale = nodes.new('ShaderNodeMath')
        scale.operation = 'MULTIPLY'
        scale.inputs[1].default_value = .19
        links.new(packed.outputs['Alpha'], scale.inputs[0])
        links.new(scale.outputs[0], invert.inputs[1])
        links.new(invert.outputs[0], shader.inputs['Roughness'])
        normal = nodes.new('ShaderNodeTexImage')
        normal.image = bpy.data.images.load(str(ART / 'Scratchy_Weave_Normal.png'))
        normal.image.colorspace_settings.name = 'Non-Color'
        mapping = nodes.new('ShaderNodeNormalMap')
        mapping.inputs['Strength'].default_value = .40
        links.new(normal.outputs['Color'], mapping.inputs['Color'])
        links.new(mapping.outputs['Normal'], shader.inputs['Normal'])
scene.render.filepath = str(HERE / 'scratchy-after.png')
bpy.ops.render.render(write_still=True)
target = Vector((0, -.05, .64))
camera.data.ortho_scale = .72
camera.location = (0, -3, .65)
aim(camera)
scene.render.filepath = str(HERE / 'scratchy-face-after.png')
bpy.ops.render.render(write_still=True)
