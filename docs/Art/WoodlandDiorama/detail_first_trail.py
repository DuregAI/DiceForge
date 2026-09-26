"""Art pass over the approved seven-cell composition. No change to route/camera."""
import bpy, math, random, contextlib, io
from mathutils import Vector
ROOT='C:/Backforge/Diceforge/DiceForge'
source=open(ROOT+'/docs/Art/WoodlandDiorama/build_first_trail.py',encoding='utf-8').read()
exec(compile(source.split('for o in scene.objects:o.select_set(True)')[0],'first_trail_base','exec'))
scene.name='First_Trail_Art'
random.seed(438)
for name,color in [('TrailGround',(.35,.38,.15)),('TrailCanvas',(.69,.46,.23)),('TrailCloth',(.60,.16,.065)),('TrailWater',(.07,.37,.34)),('TrailWood',(.51,.32,.14))]:
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
    m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.75
    materials[name]=m

def remove(o):bpy.data.objects.remove(o,do_unlink=True)
for o in list(scene.objects):
    if o.name.startswith(('Moss rim','Water creek','Flag')):remove(o)
    elif o.name.startswith('Small canvas tent'):
        o.data.materials.clear();o.data.materials.append(materials['TrailCanvas'])
        for vert in o.data.vertices:
            if vert.co.z>.1:vert.co.x+=.022*math.sin(vert.co.y*7)
    elif o.name.startswith(('Little bridge plank','Bridge post','Bridge rail','Tent ridge','Tent hem','Exit post','Exit lintel')):
        o.data.materials.clear();o.data.materials.append(materials['TrailWood'])
    elif o.name.startswith('TrailCell_'):
        for vert in o.data.vertices:
            vert.co.x+=.015*math.sin(vert.co.y*11+o.location.x*2)
            vert.co.y+=.014*math.cos(vert.co.x*9+o.location.y*3)
        o.data.update()

# A low relief ground surface painted in Unity from the same world coordinates.
n=96;rings=14;v=[(0,0,.015)];f=[]
for k in range(1,rings+1):
    r=k/rings
    for i in range(n):
        a=i*math.tau/n;edge=1+.028*math.sin(a*5)
        x=math.cos(a)*3.75*r*edge;y=math.sin(a)*2.25*r*edge
        z=.030+.010*math.sin(x*3.5)*math.cos(y*4.4)
        v.append((x,y,z))
for i in range(n):f.append((0,1+i,1+(i+1)%n))
for k in range(rings-1):
    a=1+k*n;b=a+n
    for i in range(n):f.append((a+i,b+i,b+(i+1)%n,a+(i+1)%n))
ground=mesh('Living forest floor',v,f,'TrailGround')
uv=ground.data.uv_layers.new(name='UVMap')
for p in ground.data.polygons:
    p.use_smooth=True
    for li in p.loop_indices:
        co=ground.data.vertices[ground.data.loops[li].vertex_index].co
        uv.data[li].uv=(co.x/7.5+.5,co.y/4.5+.5)

# Catmull-Rom creek, with continuous UVs for gentle flow and edge highlights.
control=[Vector((x,y)) for x,y in points];samples=[]
for i in range(len(control)-1):
    p0=control[max(0,i-1)];p1=control[i];p2=control[i+1];p3=control[min(len(control)-1,i+2)]
    for step in range(10):
        t=step/10
        p=.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)
        samples.append(p)
samples.append(control[-1]);verts=[];uvs=[]
for i,p in enumerate(samples):
    tangent=(samples[min(i+1,len(samples)-1)]-samples[max(0,i-1)]).normalized();normal=Vector((-tangent.y,tangent.x))
    width=.26+.028*math.sin(i*.35)
    for side in [-1,1]:
        q=p+normal*side*width;verts.append((q.x,q.y,.067));uvs.append(((side+1)/2,i/10))
creek=mesh('Water creek flowing',verts,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(samples)-1)],'TrailWater')
uv=creek.data.uv_layers.new(name='UVMap')
for p in creek.data.polygons:
    for li in p.loop_indices:uv.data[li].uv=uvs[creek.data.loops[li].vertex_index]

def grass(x,y,size):
    v=[];f=[]
    for j in range(7):
        a=random.random()*math.tau;r=random.uniform(0,.07)*size
        px=x+math.cos(a)*r;py=y+math.sin(a)*r;h=random.uniform(.08,.17)*size;w=.014*size
        start=len(v);v.extend([(px-w,py,.02),(px+w,py,.02),(px+w*.35+math.cos(a)*.035,py+math.sin(a)*.025,h*.60),(px-w*.35+math.cos(a)*.035,py+math.sin(a)*.025,h*.60),(px+math.cos(a)*.075,py+math.sin(a)*.05,h)])
        f.extend([(start,start+1,start+2,start+3),(start+3,start+2,start+4),(start+3,start+2,start+1,start),(start+4,start+2,start+3)])
    o=mesh('Soft grass tufts',v,f,'HeroTips');uv_project(o)

for i in range(210):
    x=random.uniform(-3.5,3.5);y=random.uniform(-2.05,2.05)
    if (x/3.55)**2+(y/2.12)**2>1:continue
    distance=min(math.hypot(x-p[0],y-p[1]) for p in poses)
    if distance<.60 or 1.07<x<2.26 or (-3.45<x<-1.75 and -.60<y<.83):continue
    if i%3==0:prop('Foliage_3',(x,y,.02),random.uniform(.30,.52),random.random()*6)
    else:grass(x,y,random.uniform(.65,1.25))
for i,(x,y) in enumerate(poses):
    for side in [-1,1]:
        for k in range(4):
            px=x+random.uniform(-.47,.47);py=y+side*random.uniform(.51,.65)
            ball('Scattered path gravel',(px,py,.026),(random.uniform(.025,.055),.035,.026),'HeroRock')
    # Restrained weathering, concentrated on the edges rather than a noisy landing surface.
    for j in range(3):
        a=j*2.2+i;px=x+math.cos(a)*.42;py=y+math.sin(a)*.37
        ball('Stone edge fleck',(px,py,.245 if i!=6 else .365),(.014,.025,.002),'HeroRock')

# Camp details: guy ropes, pegs, a rolled blanket, and a few split logs.
for x in [-3.25,-2.22]:
    rod('Tent guy rope',(x,-.12,.36),(x-.12,-.46,.02),.008,'HeroWood')
    rod('Tent peg',(x-.12,-.46,0),(x-.12,-.46,.09),.018,'HeroBark')
blanket=ball('Rolled travel blanket',(-3.0,-.40,.13),(.22,.105,.105),'HeroPine')
for x in [-3.10,-2.9]:rod('Bedroll strap',(x,-.49,.09),(x,-.40,.22),.009,'HeroWood')
for j in range(3):rod('Camp firewood',(-1.90+j*.08,.40,.05),(-1.98+j*.08,.71,.05),.04,'HeroBark')
for x in [1.27,2.32]:
    for y in [.85,1.87]:ball('Bridge iron pin',(x,y,.725),(.027,.027,.012),'HeroRock')

# Separate local-space cloth object can sway without moving its wooden support.
cloth=mesh('Exit pennant',[(0,0,0),(.35,0,-.035),(.30,0,-.25),(0,0,-.20)],[(0,1,2,3),(3,2,1,0)],'TrailCloth')
cloth.location=(2.82,1.85,.88);uv_project(cloth)
rod('Pennant pole',(2.80,1.85,.82),(2.80,1.85,1.10),.023,'HeroWood')
ball('Pole brass cap',(2.80,1.85,1.11),(.032,.032,.038),'HeroGold')

for o in scene.objects:o.select_set(True);o.hide_set(False)
with contextlib.redirect_stdout(io.StringIO()):
    bpy.ops.export_scene.fbx(filepath=OUT+'/FirstTrail.fbx',use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False)
    bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/docs/Art/WoodlandDiorama/First_Trail.blend')
print('First trail art pass exported:',len(scene.objects),'objects; approved route unchanged')
