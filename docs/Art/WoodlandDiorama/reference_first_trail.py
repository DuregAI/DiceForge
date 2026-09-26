"""Eight-space reference foundation: rounded conifers, water surround and distant groves.
Run inside Blender. Rebuild with FirstTrailCompositionBuilder in Unity afterwards.
"""
import bpy, math, random, contextlib, io
from mathutils import Vector
ROOT='C:/Backforge/Diceforge/DiceForge'
source=open(ROOT+'/docs/Art/WoodlandDiorama/detail_first_trail.py',encoding='utf-8').read()
exec(compile(source.rsplit('\nfor o in scene.objects:o.select_set(True)',1)[0],'trail_detail','exec'))
scene.name='First_Trail_Reference_08'
random.seed(20260920)

for name,color in [('TrailLake',(.12,.44,.50)),('TrailNeedles',(.20,.29,.105))]:
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
    materials[name]=m

def rounded_pine(x,y,h,base=0):
    rod('Reference pine trunk',(x,y,base),(x,y,base+h*.9),h*.067,'HeroBark')
    for j in range(5):
        a=j*math.tau/5
        rod('Pine root',(x,y,base+.15*h),(x+math.cos(a)*h*.15,y+math.sin(a)*h*.15,base+.015),h*.035,'HeroBark')
    for tier in range(3):
        radius=h*(.34-tier*.086);bottom=base+h*(.23+tier*.235)
        for petal in range(11):
            a=petal*math.tau/11+tier*.24;v=[];f=[];rings=12;sides=10
            for ring in range(rings+1):
                t=ring/rings;rad=radius*(.08+.92*t)
                z=bottom+h*.40*(1-t)**1.7
                width=radius*.26*(math.sin(math.pi*t)**.38+.025)
                for j in range(sides):
                    angle=j*math.tau/sides;side=math.cos(angle)*width
                    v.append((x+math.cos(a)*rad-math.sin(a)*side,y+math.sin(a)*rad+math.cos(a)*side,z+math.sin(angle)*width*.48))
            for ring in range(rings):
                for j in range(sides):
                    k=ring*sides+j;q=ring*sides+(j+1)%sides
                    f.append((k,q,q+sides,k+sides))
            f.append(tuple(reversed(range(sides))));f.append(tuple(range(rings*sides,(rings+1)*sides)))
            o=mesh('Rounded pine bough',v,f,'TrailNeedles');uv_project(o)
            for face in o.data.polygons:face.use_smooth=True

# Replace the old faceted crowns while retaining the established silhouette groups.
for o in list(scene.objects):
    if o.name.startswith(('Cedar trunk','Cedar layered crown')):remove(o)
for x,y,h in [(-3.15,1.16,1.65),(-1.58,1.58,1.65),(-.66,1.79,1.25),(2.89,-.26,1.0)]:
    rounded_pine(x,y,h)

# A lower water plane makes the island read as a shore instead of a floating board.
lake=mesh('Lake surround',[(-35,-35,-.73),(35,-35,-.73),(35,35,-.73),(-35,35,-.73)],[(0,1,2,3)],'TrailLake');uv_project(lake)
for i in range(26):
    a=i*math.tau/26
    if i%3==0:
        prop('Foliage_1',(3.57*math.cos(a),2.10*math.sin(a),.015),.36,a)
    if i%5==0:
        x=4.05*math.cos(a);y=2.65*math.sin(a)
        # Lily pad: missing wedge and a shallow bevel keep the silhouette readable.
        verts=[(x,y,-.705)]+[(x+.19*math.cos(.28+j*(math.tau-.56)/24),y+.19*math.sin(.28+j*(math.tau-.56)/24),-.705) for j in range(25)]
        leaf=mesh('Lake lily pad',verts,[(0,j,j+1) for j in range(1,25)],'HeroTips')
        solid=leaf.modifiers.new('Leaf thickness','SOLIDIFY');solid.thickness=.018
        bpy.context.view_layer.objects.active=leaf;bpy.ops.object.modifier_apply(modifier=solid.name)
        bevel(leaf,.01)

# Background groups are physically farther from the lens; Gaussian focus leaves the route sharp.
for x,y,h in [(-5.2,4.0,2.7),(-3.5,4.6,2.6),(-1.6,5.2,2.5),(1.8,5.1,2.7),(4.1,4.5,2.8),(5.8,3.8,2.5),(-6,7,3.5),(0,8.2,3.6),(6,7,3.8)]:
    ball('Distant moss bank',(x,y,-1.35),(1.45,1.2,.9),'HeroMoss')
    rounded_pine(x,y,h,-.7)
    for j in range(4):
        a=j*1.7
        rock('Distant shoreline rock',(x+math.cos(a)*1.25,y+math.sin(a)*1.05,-.6),(.34,.30,.35))

exec(compile(open(ROOT+'/docs/Art/WoodlandDiorama/detail_reference_shore.py',encoding='utf-8').read(),'detail_reference_shore.py','exec'))
exec(compile(open(ROOT+'/docs/Art/WoodlandDiorama/detail_reference_water.py',encoding='utf-8').read(),'detail_reference_water.py','exec'))
for name,color in [('TrialStone0',(.78,.75,.68)),('TrialStone1',(.82,.79,.70))]:
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;materials[name]=m
exec(compile(open(ROOT+'/docs/Art/WoodlandDiorama/detail_reference_tiles.py',encoding='utf-8').read(),'detail_reference_tiles.py','exec'))
for o in scene.objects:o.select_set(True);o.hide_set(False)
with contextlib.redirect_stdout(io.StringIO()):
    bpy.ops.export_scene.fbx(filepath=OUT+'/FirstTrail.fbx',use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False)
    bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/docs/Art/WoodlandDiorama/First_Trail_Reference.blend')
print('Reference foundation exported:',len(scene.objects),'objects; eight cells')
