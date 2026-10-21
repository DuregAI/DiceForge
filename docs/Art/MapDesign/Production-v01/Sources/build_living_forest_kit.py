"""Author a reusable forest kit; no gameplay meshes are edited."""
import bpy, bmesh, math, random, json
from pathlib import Path
from mathutils import Vector
from mathutils.noise import noise_vector

ROOT=Path('C:/Backforge/Diceforge/DiceForge')
OUT=ROOT/'Assets/_Project/07_Art/LivingForest/Models'
DOC=ROOT/'docs/Art/MapDesign/Production-v01'
OUT.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name!='Collection' and c.users==0:bpy.data.collections.remove(c)
rng=random.Random(20261021)
colors={'ForestBark':(.29,.14,.055),'ForestLeaf':(.25,.37,.065),'ForestNeedle':(.12,.26,.095),
        'ForestReed':(.29,.40,.105),'ForestSeed':(.23,.105,.035),'ForestStone':(.68,.64,.52),
        'ForestWood':(.40,.21,.075),'ForestMetal':(.28,.16,.065),'ForestGlass':(1,.53,.12),
        'ForestFlame':(1,.68,.21),'ForestCloth':(.62,.16,.045),'ForestPetal':(.93,.88,.65),
        'ForestPollen':(.92,.54,.08),'ForestWetStone':(.40,.36,.26),'ForestSand':(.52,.48,.33)}
mats={}
for name,color in colors.items():
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Roughness'].default_value=.82 if name not in ('ForestMetal','ForestGlass') else .27
    mats[name]=m

class Mesh:
    def __init__(self,name):self.name=name;self.v=[];self.f=[];self.mi=[];self.col=[];self.slots=[]
    def part(self,verts,faces,mat,flex=None):
        offset=len(self.v);self.v.extend(verts)
        if mat not in self.slots:self.slots.append(mat)
        mi=self.slots.index(mat)
        self.f.extend(tuple(offset+i for i in f) for f in faces);self.mi.extend([mi]*len(faces))
        self.col.extend([(max(0,min(1,flex(p) if flex else 0)),rng.uniform(.25,.9),rng.random(),1) for p in verts])
    def tube(self,a,b,r0,r1,mat,n=10,rings=3,flex=None):
        a,b=Vector(a),Vector(b);axis=(b-a).normalized();u=axis.cross(Vector((0,0,1)))
        if u.length<.01:u=axis.cross(Vector((0,1,0)))
        u.normalize();v=axis.cross(u);verts=[];faces=[]
        for k in range(rings+1):
            t=k/rings;radius=r0*(1-t)+r1*t
            for j in range(n):
                angle=j*math.tau/n;groove=1+.09*math.sin(j*4.7)+.035*math.sin(t*17+j)
                p=a+(b-a)*t+(u*math.cos(angle)+v*math.sin(angle))*radius*groove
                verts.append(tuple(p))
        for k in range(rings):
            for j in range(n):faces.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
        faces+=[tuple(reversed(range(n))),tuple(range(rings*n,(rings+1)*n))]
        self.part(verts,faces,mat,flex)
    def leaf(self,a,b,width,mat,flex=None):
        a,b=Vector(a),Vector(b);axis=(b-a).normalized();side=axis.cross(Vector((0,.3,1)))
        if side.length<.01:side=Vector((1,0,0))
        side.normalize();mid=a+(b-a)*.48
        verts=[a,mid+side*width*.5,mid+Vector((0,0,width*.13)),mid-side*width*.5,b]
        self.part([tuple(p) for p in verts],[(0,1,2),(0,2,3),(1,4,2),(3,2,4)],mat,flex)
    def finish(self):
        data=bpy.data.meshes.new(self.name);data.from_pydata(self.v,[],self.f);data.update()
        obj=bpy.data.objects.new(self.name,data);bpy.context.collection.objects.link(obj)
        for name in self.slots:data.materials.append(mats[name])
        uv=data.uv_layers.new(name='UVMap');color=data.color_attributes.new(name='Wind',type='BYTE_COLOR',domain='CORNER')
        for p in data.polygons:
            p.material_index=self.mi[p.index];p.use_smooth=True
            for li in p.loop_indices:
                idx=data.loops[li].vertex_index;co=data.vertices[idx].co
                uv.data[li].uv=(co.x*2+co.y*.7,co.z*2+co.y*.3)
                color.data[li].color=self.col[idx]
        return obj

def pine(name,h,seed):
    local=random.Random(seed);m=Mesh(name);flex=lambda p:max(0,p[2]/h)**1.7*.7
    m.tube((0,0,0),(.035,0,h),h*.045,.012,'ForestBark',12,8,flex)
    for i in range(6):
        a=i*math.tau/6;m.tube((0,0,.12),(.19*math.cos(a),.19*math.sin(a),.015),.055,.008,'ForestBark')
    for tier in range(8):
        z=h*(.23+tier*.09);radius=h*(.29-tier*.030)
        for j in range(8 if tier<5 else 6):
            a=j*math.tau/(8 if tier<5 else 6)+tier*.72
            tip=Vector((math.cos(a)*radius,math.sin(a)*radius,z-.1*h))
            stem=Vector((0,0,z));m.tube(stem,tip,.009,.002,'ForestBark',6,2,flex)
            for k in range(11):
                t=.12+k*.078;center=stem.lerp(tip,t)+Vector((0,0,.07*math.sin(t*math.pi)))
                w=radius*(.25*(1-t)+.025)
                for side in (-1,1):
                    end=center+Vector((math.cos(a+side*1.08)*w,math.sin(a+side*1.08)*w,-.025))
                    m.leaf(center,end,w*.88,'ForestNeedle',flex)
    return m.finish()

def shrub(name,h,seed):
    local=random.Random(seed);m=Mesh(name);flex=lambda p:max(0,p[2]/h)**1.8
    for branch in range(9):
        a=branch*2.3999;end=Vector((math.cos(a)*h*.38,math.sin(a)*h*.38,h*local.uniform(.58,1)))
        m.tube((0,0,.01),end,.018,.003,'ForestBark',6,3,flex)
        for i in range(18):
            t=local.uniform(.3,1);p=end*t;az=local.random()*math.tau
            q=p+Vector((math.cos(az)*h*.18,math.sin(az)*h*.18,h*.04))
            m.leaf(p,q,h*.12,'ForestLeaf',flex)
    return m.finish()

def fern():
    m=Mesh('Fern_A');flex=lambda p:max(0,p[2]/.42)**1.4
    for j in range(9):
        a=j*2.3999;length=rng.uniform(.25,.42)
        for k in range(8):
            t=k/8;p=Vector((math.cos(a)*length*t,math.sin(a)*length*t,math.sin(t*math.pi)*length*.55+.03))
            width=length*.25*(1-t)
            for side in (-1,1):m.leaf(p,p+Vector((math.cos(a+side*1.25)*width,math.sin(a+side*1.25)*width,.025)),width*.39,'ForestLeaf',flex)
    return m.finish()

def reeds():
    m=Mesh('Reeds_A');flex=lambda p:max(0,p[2]/.85)**1.65
    for i in range(9):
        x,y=rng.uniform(-.16,.16),rng.uniform(-.14,.14);h=rng.uniform(.46,.83)
        m.tube((x,y,0),(x+.04,y,h),.007,.004,'ForestReed',6,5,flex)
        if i%2==0:m.tube((x+.04,y,h*.78),(x+.04,y,h*.99),.025,.019,'ForestSeed',8,4,flex)
        for j in range(3):
            a=i*2.4+j*2;start=Vector((x,y,h*.17));tip=start+Vector((math.cos(a)*.16,math.sin(a)*.16,h*.55))
            m.leaf(start,tip,.028,'ForestReed',flex)
    return m.finish()

def boulder(name,scale,seed):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=1)
    obj=bpy.context.object;obj.name=name;local=random.Random(seed)
    for v in obj.data.vertices:
        p=v.co.copy();n=noise_vector(p*2.4+Vector((seed*.1,1,3)))
        v.co.x=(p.x+n.x*.08)*scale[0];v.co.y=(p.y+n.y*.08)*scale[1]
        v.co.z=max(0,(p.z*.55+.49+n.z*.04))*scale[2]
    obj.data.materials.append(mats['ForestStone'])
    for p in obj.data.polygons:p.use_smooth=True
    return obj

def cube(m,center,size,mat):
    x,y,z=center;dx,dy,dz=[v*.5 for v in size]
    v=[(x+sx*dx,y+sy*dy,z+sz*dz) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)]
    m.part(v,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],mat)

def lantern():
    m=Mesh('Lantern_A')
    m.tube((0,0,-.03),(0,0,-.075),.025,.03,'ForestMetal',10)
    m.tube((0,0,-.075),(0,0,-.12),.025,.105,'ForestMetal',8)
    m.tube((0,0,-.12),(0,0,-.30),.083,.083,'ForestGlass',8)
    m.tube((0,0,-.30),(0,0,-.33),.105,.075,'ForestMetal',8)
    for j in range(4):
        a=j*math.pi/2+math.pi*.25
        p=(math.cos(a)*.084,math.sin(a)*.084)
        m.tube((*p,-.12),(*p,-.31),.009,.009,'ForestMetal',6)
    m.tube((0,0,-.28),(0,0,-.16),.024,.004,'ForestFlame',8,4)
    # A genuine suspension ring gives the lamp a readable pivot.
    for j in range(12):
        a=j*math.tau/12;b=(j+1)*math.tau/12
        m.tube((math.cos(a)*.028,0,math.sin(a)*.028),(math.cos(b)*.028,0,math.sin(b)*.028),.006,.006,'ForestMetal',5,1)
    return m.finish()

def flag():
    m=Mesh('Flag_A');verts=[];faces=[]
    for j in range(9):
        for i in range(13):
            u=i/12;v=j/8
            verts.append((u*.46,math.sin(u*6.5+v*2)*u*.025,-v*.37+.015*math.sin(u*4)))
    for j in range(8):
        for i in range(12):
            k=j*13+i;faces.append((k,k+1,k+14,k+13))
    m.part(verts,faces,'ForestCloth',lambda p:p[0]/.46)
    obj=m.finish()
    # Horizontal UV supplies a stable pole-side pin mask in Unity.
    for p in obj.data.polygons:
        for li in p.loop_indices:
            co=obj.data.vertices[obj.data.loops[li].vertex_index].co
            obj.data.uv_layers.active.data[li].uv=(co.x/.46,-co.z/.37)
    return obj

def signpost():
    m=Mesh('ExitPost_A');m.tube((0,0,0),(0,0,1.46),.038,.027,'ForestWood',12,9)
    m.tube((-.07,0,1.34),(.58,0,1.34),.025,.018,'ForestWood',10,5)
    for height in (1.30,1.35,1.39):m.tube((-.045,0,height),(.045,0,height+.008),.012,.012,'ForestReed',8,2)
    return m.finish()

def grass():
    m=Mesh('GrassTuft_A');flex=lambda p:max(0,p[2]/.21)**1.8
    for j in range(21):
        a=j*2.3999;h=rng.uniform(.07,.21);r=rng.uniform(.01,.07)
        start=Vector((math.cos(a)*r,math.sin(a)*r,0))
        mid=start+Vector((math.cos(a)*h*.23,math.sin(a)*h*.23,h*.65))
        end=start+Vector((math.cos(a)*h*.7,math.sin(a)*h*.7,h))
        m.leaf(start,mid,.018,'ForestReed',flex);m.leaf(mid,end,.012,'ForestReed',flex)
    return m.finish()

def ivy():
    m=Mesh('Ivy_A');points=[Vector((math.sin(i*.6)*.05,0,-i*.048)) for i in range(13)]
    for j in range(12):
        m.tube(points[j],points[j+1],.004,.003,'ForestReed',5,1)
        for side in (-1,1):
            a=points[j];b=a+Vector((side*rng.uniform(.055,.09),-.015,.025))
            m.leaf(a,b,.047,'ForestLeaf',lambda p:max(0,-p[2]/.6)*.35)
    return m.finish()

objects=[pine('Pine_A',1.75,11),pine('Pine_B',2.2,52),shrub('Shrub_A',.55,19),shrub('Shrub_B',.38,76),fern(),reeds(),
         boulder('Boulder_A',(.46,.38,.60),13),boulder('Boulder_B',(.31,.25,.31),31),boulder('Boulder_C',(.62,.43,.43),67),
         lantern(),flag(),signpost(),grass(),ivy()]
# Exit support has its top exactly at the imported pivot, matching the unchanged logical exit.
exitstone=boulder('ExitStone_A',(.54,.48,.38),92)
for v in exitstone.data.vertices:
    v.co.z=min(0,v.co.z-.38)
objects.append(exitstone)

flower=Mesh('Flowers_A')
for j in range(7):
    x,y=rng.uniform(-.19,.19),rng.uniform(-.16,.16);h=rng.uniform(.12,.25)
    flower.tube((x,y,0),(x,y,h),.004,.002,'ForestReed',5,2,lambda p:p[2]/.3)
    for k in range(6):
        a=k*math.tau/6;flower.leaf((x,y,h),(x+math.cos(a)*.045,y+math.sin(a)*.045,h+.007),.027,'ForestPetal',lambda p:p[2]/.3)
    flower.tube((x,y,h),(x,y,h+.01),.012,.011,'ForestPollen',8,1)
objects.append(flower.finish())

bank=Mesh('BackdropBank_A');vertices=[];faces=[]
for j in range(17):
    for i in range(41):
        x=-9+i*18/40;y=-3+j*6/16
        y+=(.35*math.sin(x*.8)+.18*math.cos(x*2))*(j/16)**4
        z=.25+.30*math.sin(x*.4+y*.3)+.13*math.cos(x*.8-y*.6)
        z+=.8*math.exp(-((x+4)**2+(y-1)**2)/7)+.65*math.exp(-((x-4)**2+y*y)/5)
        vertices.append((x,y,z))
for j in range(16):
    for i in range(40):
        k=j*41+i;faces.append((k,k+1,k+42,k+41))
bank.part(vertices,faces,'ForestLeaf')
for i in range(40):
    a=vertices[16*41+i];b=vertices[16*41+i+1]
    bank.part([a,b,(b[0],b[1],-1.0),(a[0],a[1],-1.0)],[(0,1,2,3)],'ForestStone')
objects.append(bank.finish())

# A real shallow bed and irregular pebbles are visible through the creek.
control=[Vector(p) for p in [(1.92,2.17),(1.84,1.38),(1.53,.50),(1.35,-.28),(1.65,-1.14),(1.96,-1.91)]]
samples=[]
for j in range(len(control)-1):
    p0=control[max(0,j-1)];p1=control[j];p2=control[j+1];p3=control[min(len(control)-1,j+2)]
    for k in range(12):
        t=k/12;samples.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
samples.append(control[-1]);bed=Mesh('CreekBed_A');v=[]
for j,p in enumerate(samples):
    tangent=(samples[min(j+1,len(samples)-1)]-samples[max(j-1,0)]).normalized();side=Vector((-tangent.y,tangent.x))
    for direction in (-1,1):
        q=p+side*.255*direction;v.append((-q.x,-q.y,-.155+.012*math.sin(j*.7)))
bed.part(v,[(j*2,j*2+2,j*2+3,j*2+1) for j in range(len(samples)-1)],'ForestSand')
objects.append(bed.finish())

def small_stones(name,placements):
    m=Mesh(name)
    # One joined reusable model avoids a draw call for every underwater pebble.
    for x,y,z,sx,sy,sz in placements:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1)
        o=bpy.context.object
        v=[(-x+q.co.x*sx,-y+q.co.y*sy,z+q.co.z*sz) for q in o.data.vertices]
        m.part(v,[tuple(f.vertices) for f in o.data.polygons],'ForestWetStone')
        bpy.data.objects.remove(o,do_unlink=True)
    return m.finish()
placements=[]
for j in range(155):
    p=samples[rng.randrange(len(samples))];a=rng.uniform(-.18,.18);r=rng.uniform(.018,.055)
    placements.append((p.x+a,p.y+rng.uniform(-.04,.04),-.135,r,r*.75,r*.48))
objects.append(small_stones('CreekPebbles_A',placements))
placements=[]
for patch in range(9):
    a=-2.8+patch*.46
    for j in range(13):
        angle=a+rng.uniform(-.14,.14);radius=rng.uniform(1.08,1.37);s=rng.uniform(.025,.095)
        z=-1.02-(radius-1.08)*.55
        placements.append((3.75*radius*math.cos(angle),2.25*radius*math.sin(angle),z,s,s*.73,s*.48))
objects.append(small_stones('ShorePebbles_A',placements))

# Beta water: one continuous, varying-width stream, down the fall and into a
# spreading tail. Coordinates here are Unity world X/Z; FBX mirrors Blender X.
mats['ForestRiver']=mats['ForestGlass'].copy();mats['ForestRiver'].name='ForestRiver'
riverControl=[Vector(p) for p in [(1.92,2.20,.045),(1.84,1.38,.045),(1.53,.50,.045),
    (1.35,-.28,.045),(1.65,-1.14,.045),(1.96,-1.91,.045),(2.01,-2.13,.015),
    (2.16,-2.40,-.25),(2.19,-2.66,-.70),(2.40,-3.08,-.72),(2.82,-3.60,-.72)]]
riverSamples=[]
for j in range(len(riverControl)-1):
    a=riverControl[max(0,j-1)];b=riverControl[j];c=riverControl[j+1];d=riverControl[min(len(riverControl)-1,j+2)]
    for k in range(18):
        t=k/18;riverSamples.append(.5*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
riverSamples.append(riverControl[-1])
river=Mesh('RiverSurface_A');verts=[];uvs=[];colorsRiver=[];distance=0;cols=17
rockCenters=[(1.62,.54,.10),(1.42,-.15,.08),(1.69,-1.24,.11),(1.88,-1.83,.09)]
for j,p in enumerate(riverSamples):
    if j:distance+=(p-riverSamples[j-1]).length
    tangent=riverSamples[min(j+1,len(riverSamples)-1)]-riverSamples[max(0,j-1)]
    side=Vector((-tangent.y,tangent.x));side.normalize()
    tail=max(0,min(1,(-p.y-2.65)/.95))
    width=.29+.035*math.sin(distance*4.3)+.025*math.sin(distance*9+.7)+tail*.44
    rapid=.25+.65*math.exp(-((p.y+.65)/.55)**2)+.5*math.exp(-((p.y+1.70)/.32)**2)
    fall=max(0,min(1,(-p.y-2.05)/.35))*(1-tail*.55)
    for k in range(cols):
        u=k/(cols-1);offset=(u*2-1)*width*(1+.10*math.sin(distance*3+u*2))
        q=Vector((p.x,p.y))+side*offset
        wake=0
        for rx,rz,rr in rockCenters:
            downstream=rz-q.y
            if downstream>0:
                wake=max(wake,math.exp(-((q.x-rx)/(.05+downstream*.17))**2)*math.exp(-downstream*2.8))
        y=p.z+.006*math.sin(distance*17+u*4)*rapid*math.sin(math.pi*u)**2
        verts.append((-q.x,-q.y,y));uvs.append((u,distance));colorsRiver.append((min(1,rapid+fall),wake,fall,1))
river.part(verts,[(j*cols+k,(j+1)*cols+k,(j+1)*cols+k+1,j*cols+k+1) for j in range(len(riverSamples)-1) for k in range(cols-1)],'ForestRiver')
obj=river.finish()
for loop in obj.data.loops:
    obj.data.uv_layers.active.data[loop.index].uv=uvs[loop.vertex_index]
    obj.data.color_attributes.active_color.data[loop.index].color=colorsRiver[loop.vertex_index]
objects.append(obj)
objects.append(small_stones('RiverRocks_A',[(x,z,.035,r,r*.8,r*.85) for x,z,r in rockCenters]))

# An asymmetric submerged shore: coves, a shallow gravel bar and deeper pools.
shelf=Mesh('AsymmetricShelf_A');verts=[];rings=[(.86,-.85),(1.05,-.92),(1.30,-1.12),(1.70,-1.75),(3.0,-3.7),(14,-5)]
count=160
for ring,(radius,z) in enumerate(rings):
    for i in range(count):
        a=i*math.tau/count
        irregular=1+.11*math.sin(a*3+.7)+.055*math.sin(a*7+1.9)+.045*math.cos(a*5-.3)
        # The front right gravel bar is deliberately unequal to the left bank.
        bar=.22*math.exp(-((a-5.2)/.42)**2)
        x=3.75*radius*irregular*math.cos(a)+.25*math.sin(a*2)
        zWorld=2.25*radius*(1+.08*math.cos(a*4))*math.sin(a)+.18*math.cos(a*3)
        height=z+bar*(1 if ring<3 else .4)+(.035*math.sin(a*11) if ring<3 else 0)
        verts.append((-x,-zWorld,height))
for j in range(len(rings)-1):
    shelf.part(verts[j*count:(j+2)*count],[(i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count)],'ForestSand')
objects.append(shelf.finish())

def foreground_branch(name,mirror):
    m=Mesh(name);local=random.Random(402 if mirror==1 else 709)
    # Lies in the local horizontal plane; the runtime rig faces this plane to
    # the camera. Authored opposite X compensates for the FBX handedness change.
    points=[Vector((-mirror*i*.16,.035*i+.055*math.sin(i*.65),.018*math.sin(i))) for i in range(9)]
    flex=lambda p:min(1,abs(p[0])/1.3)**1.4*.48
    for i in range(8):m.tube(points[i],points[i+1],.045*(1-i/10),.04*(1-i/10),'ForestBark',10,2,flex)
    for i in range(2,9):
        for side in (-1,1):
            start=points[i];tip=start+Vector((-mirror*.10,side*(.15+local.random()*.13),.025))
            m.tube(start,tip,.009,.002,'ForestBark',6,2,flex)
            for k in range(3):
                p=start.lerp(tip,.32+k*.29)
                angle=side*(.7+k*.55)+local.uniform(-.3,.3)
                length=local.uniform(.14,.22);axis=Vector((-mirror*math.cos(angle),math.sin(angle),.12)).normalized()
                lateral=Vector((-axis.y,axis.x,0)).normalized();v=[];faces=[]
                for n in range(9):
                    t=n/8;rib=p+axis*(length*t)+Vector((0,0,.035*math.sin(t*math.pi)))
                    width=length*.33*math.sin(t*math.pi)**.8*(1+.15*math.sin(t*math.pi*7))
                    v.extend([tuple(rib-lateral*width-Vector((0,0,width*.25))),tuple(rib),tuple(rib+lateral*width-Vector((0,0,width*.25)))])
                for n in range(8):
                    a=n*3;faces.extend([(a,a+3,a+4,a+1),(a+1,a+4,a+5,a+2)])
                m.part(v,faces,'ForestLeaf',flex)
    return m.finish()
# Close foreground uses the approved textured oak instead of visibly flat leaf
# cards. Only lateral crown/branch surfaces are retained; the cut stays offscreen.
with bpy.data.libraries.load(str(DOC/'Oak_Tripo_A.blend'),link=False) as (src,dst):
    dst.objects=['Oak_Tripo_A']
oak=dst.objects[0]
for name,mirror in [('ForegroundBranch_A',1),('ForegroundBranch_B',-1)]:
    data=oak.data.copy();obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    bm=bmesh.new();bm.from_mesh(data)
    remove=[v for v in bm.verts if v.co.x>-.20 or v.co.z<1.10]
    bmesh.ops.delete(bm,geom=remove,context='VERTS')
    for v in bm.verts:
        p=v.co.copy();v.co=Vector(((p.x+.20)*mirror,p.z-1.45,-p.y))
    if mirror<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(data);bm.free();data.update()
    colors=data.color_attributes.active_color
    for loop in data.loops:
        p=data.vertices[loop.vertex_index].co
        colors.data[loop.index].color=(min(1,abs(p.x))*.45,.55,.5,1)
    objects.append(obj)
bpy.data.objects.remove(oak,do_unlink=True)

manifest=[]
for obj in objects:
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    if not obj.data.uv_layers:
        with bpy.context.temp_override(object=obj,active_object=obj,selected_objects=[obj],selected_editable_objects=[obj]):
            bpy.ops.object.mode_set(mode='EDIT');bpy.ops.uv.smart_project(island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.export_scene.fbx(filepath=str(OUT/(obj.name+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,apply_scale_options='FBX_SCALE_ALL')
    tri=sum(len(p.vertices)-2 for p in obj.data.polygons)
    manifest.append({'name':obj.name,'triangles':tri,'materials':[m.name for m in obj.data.materials],
                     'file':str(OUT/(obj.name+'.fbx')),'pivot':'ground; Lantern suspension ring; Flag pole-side upper corner'})
bpy.ops.wm.save_as_mainfile(filepath=str(DOC/'LivingForest_Kit.blend'))
(DOC/'kit-manifest.json').write_text(json.dumps(manifest,indent=2))
print('LIVING_FOREST_KIT',len(objects),'models',sum(x['triangles'] for x in manifest),'triangles')
