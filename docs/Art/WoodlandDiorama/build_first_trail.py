"""Composition study: two goblins, seven spaces, camp to bridge. No gameplay changes."""
import bpy, math, os, contextlib, io
ROOT='C:/Backforge/Diceforge/DiceForge'
# Reuse authored modeling helpers, not the previous ring composition.
with open(ROOT+'/docs/Art/WoodlandDiorama/build_hero.py',encoding='utf-8') as source:
    exec(source.read().split('# Raised, irregular island:')[0])
scene.name='First_Trail_Composition'
OUT=ROOT+'/Assets/_Project/07_Art/FirstTrail'
os.makedirs(OUT,exist_ok=True)

# Compact elongated island, broad enough for the route but with an obvious beginning/end.
n=64;v=[];f=[]
for radius,z in [(1,0),(1.015,-.16),(.93,-.67),(.85,-.92)]:
    for i in range(n):
        a=i*math.tau/n;r=radius*(1+.028*math.sin(a*5))
        v.append((math.cos(a)*3.75*r,math.sin(a)*2.25*r,z))
for k in range(3):
    for i in range(n):f.append((k*n+i,k*n+(i+1)%n,(k+1)*n+(i+1)%n,(k+1)*n+i))
v.append((0,0,0));center=len(v)-1
for i in range(n):f.append((center,i,(i+1)%n))
o=mesh('Trail island',v,f,'HeroEarth');bevel(o,.045)
for i in range(26):
    a=i*math.tau/26
    rock('Rocky edge',(3.66*math.cos(a),2.18*math.sin(a),-.42),(.35,.25,.33))
    if i%2==0:ball('Moss rim',(3.58*math.cos(a),2.12*math.sin(a),.012),(.38,.22,.055),'HeroMoss')

poses=[(-2.85,-1.05),(-1.96,-1.02),(-1.08,-.80),(-.29,-.36),(.37,.24),(.88,.98),(1.80,1.36),(2.84,1.38)]
for i,(x,y) in enumerate(poses):
    p0=poses[max(0,i-1)];p1=poses[min(len(poses)-1,i+1)];angle=math.atan2(p1[1]-p0[1],p1[0]-p0[0])
    box('TrailCell_%02d'%i,(x,y,.12 if i!=6 else .24),(.425,.40,.12),'HeroStone',.065,angle)

# Creek crosses the last stretch exactly once. The bridge carries cell 5.
points=[(1.92,2.17),(1.84,1.38),(1.53,.50),(1.35,-.28),(1.65,-1.14),(1.96,-1.91)]
verts=[]
for x,y in points:verts.extend([(x-.27,y,.064),(x+.27,y,.064)])
water=mesh('Water creek',verts,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(points)-1)],'HeroWater');uv_project(water)
for j,(x,y) in enumerate(points):
    for side in [-1,1]:rock('Creek bank',(x+side*.36,y,.07),(.15,.13,.10))
for i in range(9):
    x=1.80+(i-4)*.14;box('Little bridge plank',(x,1.36,.17),(.060,.56,.055),'HeroWood',.025)
for x in [1.24,2.36]:
    for y in [.84,1.88]:box('Bridge post',(x,y,.36),(.044,.044,.36),'HeroBark',.02)
for y in [.84,1.88]:rod('Bridge rail',(1.23,y,.62),(2.37,y,.62),.028,'HeroWood')

# Camp marks the start; an arch frames the destination.
tent=mesh('Small canvas tent',[(-3.25,-.12,0),(-2.22,-.12,0),(-2.74,-.12,.76),(-3.25,.73,0),(-2.22,.73,0),(-2.74,.73,.76)],[(2,5,3,0),(1,4,5,2)],'HeroWood');uv_project(tent)
rod('Tent ridge',(-2.74,-.18,.77),(-2.74,.80,.77),.026,'HeroBark')
for x in [-3.25,-2.22]:rod('Tent hem',(x,-.12,.02),(x,.73,.02),.024,'HeroBark')
prop('Crate',(-3.18,-.48,0),.48,.12)
prop('Stump',(-1.85,.12,0),.48)
for i in range(8):
    a=i*math.tau/8;rock('Campfire stones',(-2.14+math.cos(a)*.18,-.28+math.sin(a)*.18,.035),(.054,.05,.035))
rod('Campfire log',(-2.27,-.30,.05),(-2.0,-.24,.05),.035,'HeroBark')
for x in [2.66,3.18]:
    rod('Exit post',(x,1.85,0),(x,1.85,.90),.042,'HeroWood')
rod('Exit lintel',(2.62,1.85,.90),(3.22,1.85,.90),.045,'HeroWood')
prop('Flag',(3.23,1.79,0),.65)

# A few clear groups, kept away from the route. Detailed dressing comes after approval.
for x,y,h in [(-3.15,1.16,1.65),(-1.58,1.58,1.65),(-.66,1.79,1.25),(2.89,-.26,1.0)]:pine(x,y,h,.3)
for x,y,s in [(-.92,.49,.56),(.0,1.51,.48),(2.55,-.76,.54),(-2.0,-1.76,.38)]:
    rock('Feature rock',(x,y,.18),(.43*s,.32*s,.32*s));ball('Moss on rock',(x,y,.34*s),(.34*s,.24*s,.045),'HeroMoss')
for i in range(90):
    x=random.uniform(-3.4,3.4);y=random.uniform(-1.93,1.93)
    if (x/3.5)**2+(y/2.05)**2>1:continue
    if min(math.hypot(x-p[0],y-p[1]) for p in poses)<.65:continue
    if 1.0<x<2.3:continue
    if -3.4<x<-1.75 and -.6<y<.85:continue
    prop('Foliage_'+str(random.choice([0,1,3,5])),(x,y,.018),random.uniform(.48,.73),random.random()*math.tau)
for o in scene.objects:o.select_set(True);o.hide_set(False)
with contextlib.redirect_stdout(io.StringIO()):
    bpy.ops.export_scene.fbx(filepath=OUT+'/FirstTrail.fbx',use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False)
    bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/docs/Art/WoodlandDiorama/First_Trail.blend')
print('First trail composition exported:',len(scene.objects),'objects, seven cells')
