"""Render the FBX roundtrip at source scale and at suggested field scale."""
import bpy
from mathutils import Vector
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
ART=ROOT/"docs/Art/DemoRCCharacters"
MODELS=ROOT/"Assets/_Project/07_Art/DemoRCCharacters/Models"
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
roots={}
for key,x in (("tish",-4.95),("luma",-2.55),("bum",.15),("ryzh",2.95),("bark",5.05)):
    previous=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(MODELS/(key+".fbx")))
    added=set(bpy.data.objects)-previous
    parent=bpy.data.objects.new(key+" placement",None)
    bpy.context.collection.objects.link(parent)
    for obj in added:
        if obj.parent not in added:obj.parent=parent
    parent.location.x=x
    roots[key]=parent
scene.frame_set(1)
world=bpy.data.worlds.new("Warm inspection world");world.use_nodes=True;scene.world=world
world.node_tree.nodes["Background"].inputs["Color"].default_value=(.35,.34,.31,1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value=.6
for name,position,power,size in (("Large soft key",(-4,-6,8),1900,10),("Fill",(5,-3,6),1300,8),("Rim",(1,4,6),1400,8)):
    light=bpy.data.lights.new(name,"AREA");light.energy=power;light.shape="DISK";light.size=size
    obj=bpy.data.objects.new(name,light);bpy.context.collection.objects.link(obj);obj.location=position
    obj.rotation_euler=(Vector((0,0,1.3))-obj.location).to_track_quat("-Z","Y").to_euler()
camera_data=bpy.data.cameras.new("Lineup camera");camera=bpy.data.objects.new("Lineup camera",camera_data)
bpy.context.collection.objects.link(camera);scene.camera=camera
camera.location=(0,-16,4.4);camera.rotation_euler=(Vector((0,0,1.45))-camera.location).to_track_quat("-Z","Y").to_euler()
camera_data.type="ORTHO";camera_data.ortho_scale=13.5
scene.render.engine="CYCLES";scene.cycles.samples=32
scene.view_settings.view_transform="Standard";scene.view_settings.look="Medium High Contrast";scene.view_settings.exposure=-.1
scene.render.resolution_x=1920;scene.render.resolution_y=600;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.render.image_settings.file_format="PNG"
scene.render.filepath=str(ART/"Renders/lineup-source-scale.png")
bpy.ops.render.render(write_still=True)
roots["bum"].scale=(.76,.76,.76)
scene.render.filepath=str(ART/"Renders/lineup-field-scale.png")
bpy.ops.render.render(write_still=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ART/"Sources/lineup.blend"))
