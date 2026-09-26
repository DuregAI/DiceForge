"""One authored landscape composition. Run in Blender, then Build hero level in Unity."""
import bpy, math, random, os, json
from mathutils import Vector
ROOT='C:/Backforge/Diceforge/DiceForge'
OUT=ROOT+'/Assets/_Project/07_Art/WoodlandHero'
os.makedirs(OUT,exist_ok=True)
random.seed(814)
scene=bpy.data.scenes.new('Woodland_Hero_15')
bpy.context.window.scene=scene
names=['Bridge','Fence','Crate','Stump','Sign','Flag']+['Foliage_'+str(i) for i in range(6)]
with bpy.data.libraries.load(ROOT+'/docs/Art/WoodlandDiorama/Woodland_SourceKit.blend',link=False) as (src,dst):
    dst.objects=[n for n in names if n in src.objects]
templates={n:o for n,o in zip(names,dst.objects)}
materials={}
colors={'HeroEarth':(.28,.18,.085),'HeroMoss':(.25,.34,.075),'HeroRock':(.36,.37,.30),'HeroStone':(.78,.71,.53),'HeroPine':(.105,.23,.115),'HeroTips':(.24,.35,.15),'HeroBark':(.22,.11,.045),'HeroWood':(.47,.26,.095),'HeroWater':(.035,.28,.26),'HeroFoam':(.55,.83,.73),'HeroGold':(.84,.56,.16)}
for name,color in colors.items():
    mat=bpy.data.materials.new(name);mat.diffuse_color=(*color,1);mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=.83
    materials[name]=mat
def mesh(name,v,f,mat):
    data=bpy.data.meshes.new(name);data.from_pydata(v,[],f);data.update()
    o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);data.materials.append(materials[mat])
    return o
def uv_project(o,scale=1):
    layer=o.data.uv_layers.active or o.data.uv_layers.new(name='UVMap')
    for p in o.data.polygons:
        axis=max(range(3),key=lambda i:abs(p.normal[i]));a,b=((1,2),(0,2),(0,1))[axis]
        for li in p.loop_indices:
            co=o.data.vertices[o.data.loops[li].vertex_index].co
            layer.data[li].uv=(co[a]*scale,co[b]*scale)
def bevel(o,width=.04):
    bpy.context.view_layer.objects.active=o
    mod=o.modifiers.new('Soft hand carved edges','BEVEL');mod.width=width;mod.segments=3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Keep broad faces planar and smooth the bevels with weighted normals.
    for p in o.data.polygons:p.use_smooth=True
    mod=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
    bpy.ops.object.modifier_apply(modifier=mod.name)
    uv_project(o)
def box(name,pos,size,mat,edge=.04,angle=0):
    bpy.ops.mesh.primitive_cube_add(size=2,location=pos)
    o=bpy.context.object;o.name=name;o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(materials[mat]);bevel(o,edge);o.rotation_euler.z=angle
    return o
def ball(name,pos,size,mat):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=pos)
    o=bpy.context.object;o.name=name;o.scale=size;o.data.materials.append(materials[mat])
    for p in o.data.polygons:p.use_smooth=True
    uv_project(o,2);return o
def rod(name,a,b,r,mat):
    a,b=Vector(a),Vector(b)
    bpy.ops.mesh.primitive_cone_add(vertices=10,radius1=r,radius2=r*.8,depth=(b-a).length,location=(a+b)/2)
    o=bpy.context.object;o.name=name;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();o.data.materials.append(materials[mat]);bevel(o,.01);return o
def prop(name,pos,scale=1,angle=0):
    o=templates[name].copy();o.data=templates[name].data.copy();scene.collection.objects.link(o)
    o.name=name;o.location=pos;o.scale=(scale,)*3;o.rotation_euler=(0,0,angle);o.hide_set(False);return o
def rock(name,pos,size,mat='HeroRock'):
    o=box(name,pos,size,mat,.10,random.uniform(-.5,.5))
    for v in o.data.vertices:v.co+=Vector((random.uniform(-.025,.025),random.uniform(-.025,.025),random.uniform(-.02,.02)))
    return o
def pine(x,y,h,phase=0):
    rod('Cedar trunk',(x,y,0),(x,y,h*.83),h*.055,'HeroBark')
    for tier in range(5):
        z=h*(.20+tier*.155);radius=h*(.29-tier*.046);n=24;v=[];f=[]
        for ring,(r,dz) in enumerate([(1,-.045),(.94,.03),(.58,.24),(.10,.43)]):
            for j in range(n):
                a=j*math.tau/n+phase+tier*.3
                scallop=1+.085*math.cos(j*math.pi)+.035*math.sin(j*3.7+tier)
                v.append((x+math.cos(a)*radius*r*scallop,y+math.sin(a)*radius*r*scallop,z+dz*h/2+(.035*h*math.cos(j*math.pi) if ring==0 else 0)))
        for ring in range(3):
            for j in range(n):f.append((ring*n+j,ring*n+(j+1)%n,(ring+1)*n+(j+1)%n,(ring+1)*n+j))
        f.append(tuple(range(3*n,4*n)))
        o=mesh('Cedar layered crown',v,f,'HeroPine' if tier%2==0 else 'HeroTips');bevel(o,.024)

# Raised, irregular island: visible rock faces and an uneven moss-covered crown.
n=96;v=[];f=[]
for radius,z in [(1,.015),(1.025,-.14),(.97,-.72),(.85,-1.15)]:
    for i in range(n):
        a=i*math.tau/n;r=radius*(1+.026*math.sin(a*7)+.017*math.cos(a*11))
        v.append((math.cos(a)*4.17*r,math.sin(a)*2.94*r,z+.025*math.sin(a*8)))
for k in range(3):
    for i in range(n):f.append((k*n+i,k*n+(i+1)%n,(k+1)*n+(i+1)%n,(k+1)*n+i))
v.append((0,0,.035));center=len(v)-1
for i in range(n):f.append((center,i,(i+1)%n))
island=mesh('Ancient earth island',v,f,'HeroEarth');bevel(island,.035)
for i in range(44):
    a=i*math.tau/44;r=1+.024*math.sin(a*7)
    rock('Cliff block',(4.05*r*math.cos(a),2.85*r*math.sin(a),-.47),(.32,.28,random.uniform(.30,.49)))
    if i%2==0:ball('Overhanging moss',(4.02*math.cos(a),2.82*math.sin(a),-.02),(.38,.25,.095),'HeroMoss')
    if i%5==0:rod('Ancient root',(3.96*math.cos(a),2.77*math.sin(a),.02),(3.98*math.cos(a+.035),2.84*math.sin(a+.035),-.80),.045,'HeroBark')

# Equal arc length path, with restrained handmade offsets in tile geometry.
samples=[Vector((-math.sin(i*math.tau/900)*3.27,-math.cos(i*math.tau/900)*2.08,.27)) for i in range(901)]
length=[0]
for i in range(1,len(samples)):length.append(length[-1]+(samples[i]-samples[i-1]).length)
poses=[]
for i in range(15):
    d=length[-1]*i/15;j=1
    while length[j]<d:j+=1
    p=samples[j-1].lerp(samples[j],(d-length[j-1])/(length[j]-length[j-1]));poses.append(p)
for i,p in enumerate(poses):
    delta=poses[(i+1)%15]-poses[(i-1)%15];angle=math.atan2(delta.y,delta.x)
    o=box('Tile_%02d'%i,(p.x,p.y,.12),(.56,.49,.14),'HeroStone',.085,angle+random.uniform(-.035,.035))
    for vert in o.data.vertices:vert.co.z+=.008*math.sin(vert.co.x*8+i)
    # Fine stone chips around the outer edges, leaving the landing surface clean.
    for j in range(3):
        a=random.uniform(0,math.tau);rock('Path pebble',(p.x+math.cos(a)*.67,p.y+math.sin(a)*.59,.055),(.055,.042,.04))
with open(OUT+'/HeroLayout.json','w') as fp:json.dump({'positions':[{'x':p.x,'y':p.z,'z':-p.y} for p in poses]},fp)

# Pond and winding brook, opening into a waterfall on the right front edge.
ball('Pond bed',(-.55,-.10,.015),(1.16,.79,.055),'HeroRock')
ball('Water pond',(-.55,-.10,.073),(1.10,.73,.032),'HeroWater')
points=[(-.40,-.08),(.28,-.13),(.85,-.44),(1.44,-.54),(2.1,-.37),(2.70,-.27),(3.23,-.50),(3.93,-.78)]
verts=[]
for i,(x,y) in enumerate(points):
    width=.24 if i>1 else .35
    verts.extend([(x,y-width,.076),(x,y+width,.076)])
stream=mesh('Water brook',verts,[(i*2+2,i*2+3,i*2+1,i*2) for i in range(len(points)-1)],'HeroWater');uv_project(stream)
for j in range(8):
    y=-.78+(j-3.5)*.071;verts=[]
    for k in range(13):
        t=k/12;x=3.89+.30*math.sin(t*math.pi/2);z=.08-t*1.85
        verts.extend([(x,y-.037,z),(x+.009,y+.037,z)])
    fall=mesh('Waterfall ribbon',verts,[(k*2,k*2+1,k*2+3,k*2+2) for k in range(12)],'HeroFoam' if j%3==0 else 'HeroWater');uv_project(fall)
for j in range(11):
    ball('Water foam',(4.18+random.uniform(-.15,.15),-.78+random.uniform(-.32,.32),-1.68+random.uniform(-.05,.07)),(.12,.10,.06),'HeroFoam')
for j in range(15):
    a=j*math.tau/15;rock('Stream bank',(-.55+math.cos(a)*1.2,-.1+math.sin(a)*.83,.13),(.17,.13,random.uniform(.11,.22)))

# Bespoke curved wooden footbridge with rails and cut end grain.
for i in range(9):
    y=(i-4)*.14;z=.17+.085*(1-(y/.65)**2)
    box('Bridge plank',(-.65,y,z),(.55,.062,.055),'HeroWood',.025,random.uniform(-.025,.025))
for x in [-1.14,-.16]:
    for y in [-.59,.59]:
        box('Bridge post',(x,y,.32),(.048,.05,.32),'HeroBark',.022)
        ball('Bridge post cap',(x,y,.64),(.07,.07,.05),'HeroWood')
    rod('Bridge handrail',(x,-.61,.53),(x,.61,.53),.029,'HeroWood')

# Three silhouette groups; keep the near side open for figures.
for x,y,h in [(.65,.82,2.0),(1.41,.67,1.40),(.13,1.05,1.16),(-3.10,1.89,2.40),(-2.47,2.18,1.78),(2.8,2.04,1.70)]:pine(x,y,h,random.random())
for x,y,s in [(1.35,.04,.7),(-1.55,.70,.55),(.45,1.36,.60),(-2.91,-.83,.65),(2.80,1.31,.53)]:
    rock('Feature boulder',(x,y,s*.34),(s*.55,s*.40,s*.43))
    ball('Boulder moss',(x-.03,y,s*.70),(s*.43,s*.32,s*.07),'HeroMoss')
prop('Crate',(-1.85,1.22,.02),.85,.2);prop('Stump',(1.60,-1.16,.02),.8)
prop('Sign',(-3.67,.1,0),1.1,-.13);prop('Flag',(2.87,1.32,0),1.05)
for x,y,a in [(-3.70,1.06,1.1),(-2.1,2.62,.3),(-.70,2.69,0),(1.0,2.66,0),(3.55,1.07,-1.0),(-3.38,-1.48,-.8),(2.84,-1.94,.6)]:prop('Fence',(x,y,0),.85,a)

# Authored clusters, deliberately avoiding all landing surfaces and the water.
for i in range(420):
    x=random.uniform(-4.0,4.0);y=random.uniform(-2.8,2.8)
    if (x/4.02)**2+(y/2.81)**2>1:continue
    if min((Vector((x,y,0))-Vector((p.x,p.y,0))).length for p in poses)<.67:continue
    if ((x+.55)/1.24)**2+((y+.10)/.87)**2<1:continue
    if -.78<y<.12 and x>.25:continue
    choice=random.choices([0,1,2,3,4,5],[3,3,2,2,1,2])[0]
    prop('Foliage_'+str(choice),(x,y,.02),random.uniform(.55,1.0),random.random()*math.tau)
    if i%3==0:ball('Soft ground moss',(x,y,.012),(random.uniform(.15,.35),random.uniform(.13,.27),.037),'HeroMoss')

# Export the authored environment only. Source scene stays separate from other Blender work.
for o in scene.objects:o.hide_set(False);o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT+'/WoodlandHero.fbx',use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='AUTO')
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/docs/Art/WoodlandDiorama/Woodland_Hero.blend')
print('Hero island exported:',len(scene.objects),'objects, 15 cells')
