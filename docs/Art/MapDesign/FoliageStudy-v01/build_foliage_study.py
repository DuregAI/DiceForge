"""Rounded, curved leaves arranged on authored branch fans; deterministic study asset."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT=Path('C:/Backforge/Diceforge/DiceForge')
OUT=ROOT/'Assets/_Project/07_Art/FoliageStudy'
DOC=ROOT/'docs/Art/MapDesign/FoliageStudy-v01'
OUT.mkdir(parents=True,exist_ok=True); DOC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
rng=random.Random(731)
verts=[]; faces=[]; uvs=[]; masks=[]

def leaf(base, direction, length, width, roll):
    axis=direction.normalized()
    side=axis.cross(Vector((0,0,1))).normalized()
    normal=side.cross(axis).normalized()
    # A shallow bowl and drooping tip catch broad highlights, not triangular flashes.
    side2=side*math.cos(roll)+normal*math.sin(roll)
    normal2=side2.cross(axis).normalized()
    start=len(verts); variation=rng.uniform(.15,1)
    for j in range(9):
        t=j/8
        radius=width*.5*max(.025,math.sin(math.pi*t)**.68)
        for k in range(5):
            s=k/2-1
            bend=length*(.16*math.sin(math.pi*t)-.13*t*t)-abs(s)**1.5*width*.13
            p=base+axis*(length*t)+side2*(s*radius)+normal2*bend
            verts.append(tuple(p)); uvs.append((k/4,t)); masks.append((min(1,max(0,p.z/1.05))**1.6,variation,.5,1))
    for j in range(8):
        for k in range(4):
            a=start+j*5+k
            # Upward-facing normals for the Blender Z-up leaf surface.
            faces.append((a,a+1,a+6,a+5))

wood=bpy.data.materials.new('ForestBark'); wood.diffuse_color=(.23,.12,.045,1)
leafmat=bpy.data.materials.new('StudyLeaves'); leafmat.diffuse_color=(.33,.48,.12,1)
branches=[]
def stem(points,radius):
    curve=bpy.data.curves.new('Branch','CURVE'); curve.dimensions='3D'; curve.bevel_depth=radius; curve.bevel_resolution=1; curve.resolution_u=2
    spline=curve.splines.new('POLY'); spline.points.add(len(points)-1)
    for p,co in zip(spline.points,points): p.co=(*co,1)
    ob=bpy.data.objects.new('Branch',curve); bpy.context.collection.objects.link(ob); ob.data.materials.append(wood)
    bpy.context.view_layer.objects.active=ob; ob.select_set(True); bpy.ops.object.convert(target='MESH'); ob.select_set(False); branches.append(ob)

for branch in range(9):
    angle=branch*2.39996
    reach=rng.uniform(.35,.68); height=rng.uniform(.48,.88)
    radial=Vector((math.cos(angle),math.sin(angle),0)); cross=Vector((-math.sin(angle),math.cos(angle),0))
    def point(t): return radial*(reach*t**1.3)+Vector((0,0,height*(1-(1-t)**1.5)))
    stem([point(i/12) for i in range(13)],.012)
    for node in range(2,10):
        t=node/10; base=point(t)
        for sign in (-1,1):
            direction=(radial*.35+cross*sign*.83+Vector((0,0,rng.uniform(.18,.55)))).normalized()
            length=rng.uniform(.20,.29)*(1-.25*t)
            tip=base+direction*.07
            stem([base,tip],.0035)
            leaf(tip,direction,length,length*rng.uniform(.57,.72),rng.uniform(-.28,.28))
    leaf(point(.97),radial*.7+Vector((0,0,.65)),.19,.115,0)

mesh=bpy.data.meshes.new('Rounded leaf blades'); mesh.from_pydata(verts,[],faces); mesh.update()
ob=bpy.data.objects.new('Rounded leaves',mesh); bpy.context.collection.objects.link(ob); mesh.materials.append(leafmat)
uv=mesh.uv_layers.new(name='UVMap'); color=mesh.color_attributes.new(name='Wind',type='FLOAT_COLOR',domain='CORNER')
for poly in mesh.polygons:
    poly.use_smooth=True
    for li in poly.loop_indices:
        vi=mesh.loops[li].vertex_index; uv.data[li].uv=uvs[vi]; color.data[li].color=masks[vi]
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active=ob; bpy.ops.object.join(); ob.name='RoundedShrub_A'
bpy.ops.wm.save_as_mainfile(filepath=str(DOC/'RoundedShrub_A.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'RoundedShrub_A.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,apply_scale_options='FBX_SCALE_ALL')
ob.data.calc_loop_triangles()
(DOC/'mesh-manifest.json').write_text(json.dumps({'leaves':153,'vertices':len(ob.data.vertices),'triangles':len(ob.data.loop_triangles),'materials':len(ob.data.materials),'seed':731},indent=2))
