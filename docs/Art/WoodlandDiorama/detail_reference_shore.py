"""Authored foreground shoreline fragment. Executed by reference_first_trail.py."""
random.seed(815)
for name,color in [('ShoreStone',(.53,.44,.32)),('ShoreStoneLight',(.64,.56,.43)),('ShoreStoneCool',(.43,.46,.43)),('ShoreMoss',(.36,.43,.09)),('ShoreLeaf',(.27,.37,.10)),('ShoreFoam',(.46,.72,.66))]:
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
    materials[name]=m

def shore_stone(name,x,y,rx,ry,bottom,top,phase):
    n=7;v=[];f=[]
    outline=[1+random.uniform(-.15,.13) for j in range(n)]
    for ring,(scale,z) in enumerate([(.90,bottom),(1.0,(bottom+top)*.5),(.88,top)]):
        for j in range(n):
            a=j*math.tau/n+phase
            v.append((x+math.cos(a)*rx*outline[j]*scale+ring*.018,y+math.sin(a)*ry*outline[j]*scale,z+random.uniform(-.055,.055)))
    for ring in range(2):
        for j in range(n):f.append((ring*n+j,ring*n+(j+1)%n,(ring+1)*n+(j+1)%n,(ring+1)*n+j))
    f.append(tuple(range(2*n,3*n)));f.append(tuple(reversed(range(n))))
    o=mesh(name,v,f,'ShoreStone');o.data.materials.append(materials['ShoreStoneLight']);o.data.materials.append(materials['ShoreStoneCool'])
    for face in o.data.polygons:face.material_index=random.choices([0,1,2],[6,2,1])[0]
    bevel(o,.035)
    return o

def shore_plant(x,y,z,scale):
    for j in range(6):
        a=j*math.tau/6+.3
        leaf=ball('Shore fleshy leaf',(x+math.cos(a)*.08*scale,y+math.sin(a)*.08*scale,z+.07*scale),(.12*scale,.044*scale,.035*scale),'ShoreLeaf')
        leaf.rotation_euler=(0,-.35,a)
    ball('Shore leaf heart',(x,y,z+.07*scale),(.035*scale,)*3,'ShoreMoss')

# Only the near bank is replaced: a comparison fragment, with the route untouched.
for o in list(scene.objects):
    if o.name.startswith('Rocky edge') and o.location.y<-.65:remove(o)
for i in range(12):
    a=-2.84+i*.23;x=3.62*math.cos(a);y=2.16*math.sin(a)
    top=random.uniform(-.05,.09);rx=random.uniform(.49,.58);ry=random.uniform(.39,.47)
    shore_stone('Carved shoreline stone',x,y,rx,ry,-.91,top,a+.2)
    if i in [2,5,8]:
        shore_stone('Low shoreline shelf',x*1.06,y*1.07,rx*.65,ry*.7,-.90,-.46,a-.2)
    if i%2==0:
        for j in range(14):
            t=j/13
            mx=x+math.cos(a)*rx*.6+random.uniform(-.07,.07)
            my=y+math.sin(a)*ry*.55+random.uniform(-.09,.09)
            mz=top+.028-t*.32
            ball('Cascading shore moss',(mx,my,mz),(random.uniform(.035,.072),random.uniform(.025,.065),.028),'ShoreMoss')
        shore_plant(x*.94,y*.92,max(.045,top+.02),random.uniform(.9,1.25))
    # Broken shallow arcs, following the foot of the rocks rather than a continuous outline.
    verts=[];faces=[]
    for j in range(19):
        angle=a-.95+j*1.9/18
        for scale in [1.0,1.04]:verts.append((x+math.cos(angle)*rx*.94*scale,y+math.sin(angle)*ry*.94*scale,-.715))
    for j in range(18):
        if j%7!=0:faces.append((j*2,j*2+1,j*2+3,j*2+2))
    foam=mesh('Shore contact ripples',verts,faces,'ShoreFoam');uv_project(foam)

# A focal stone and plant group on the near-right shoulder, below the playable route.
shore_stone('Mossy shoulder outcrop',.15,-1.62,.38,.30,.025,.42,.3)
shore_stone('Mossy shoulder companion',.48,-1.57,.22,.21,.02,.22,1.0)
for j in range(20):
    a=random.random()*math.tau;r=random.uniform(.04,.24)
    ball('Shoulder moss cushion',(.15+math.cos(a)*r,-1.62+math.sin(a)*r,.405),(.045,.038,.026),'ShoreMoss')
for x,y,s in [(-.30,-1.77,1.3),(.48,-1.94,1.1),(.71,-1.51,.85)]:shore_plant(x,y,.04,s)

# Uneven earthen lip bridges the paper-thin floor and the tops of the cliff stones.
v=[];f=[];segments=100
for j in range(segments+1):
    a=-2.94+j*2.58/segments
    wobble=1+.025*math.sin(a*17)+.012*math.sin(a*31)
    for k,(radius,z) in enumerate([(.86,.034),(.94,.062),(1.015,-.025),(1.035,-.17)]):
        r=radius*(wobble if k>0 else 1)
        v.append((3.75*math.cos(a)*r,2.25*math.sin(a)*r,z+.017*math.sin(a*23)))
for j in range(segments):
    a=-2.94+(j+.5)*2.58/segments
    if abs(a+1.05)<.13:continue # Keep the creek outlet open.
    for k in range(3):f.append((j*4+k,j*4+k+1,(j+1)*4+k+1,(j+1)*4+k))
lip=mesh('Uneven earthen shoreline lip',v,f,'TrailGround')
uv=lip.data.uv_layers.new(name='UVMap')
for face in lip.data.polygons:
    face.use_smooth=True
    for li in face.loop_indices:
        co=lip.data.vertices[lip.data.loops[li].vertex_index].co
        uv.data[li].uv=(co.x/7.5+.5,co.y/4.5+.5)

# Seat moss on the actual surfaces instead of leaving separate green beads floating above them.
surfaces=[o for o in scene.objects if o.name.startswith(('Carved shoreline stone','Mossy shoulder','Living forest floor','Uneven earthen shoreline lip'))]
bpy.context.view_layer.update()
def surface_height(x,y):
    top=-.15
    for o in surfaces:
        inverse=o.matrix_world.inverted()
        hit,p,normal,index=o.ray_cast(inverse@Vector((x,y,3)),inverse.to_3x3()@Vector((0,0,-1)))
        if hit:top=max(top,(o.matrix_world@p).z)
    return top

for o in list(scene.objects):
    if o.name.startswith(('Cascading shore moss','Shoulder moss cushion')):remove(o)

def moss_patch(x,y,sx,sy,seed):
    v=[];f=[];rings=6;n=32
    for ring in range(rings+1):
        r=max(.001,ring/rings)
        for j in range(n):
            a=j*math.tau/n;edge=1+.10*math.sin(a*5+seed)+.07*math.sin(a*9)
            px=x+math.cos(a)*sx*r*edge;py=y+math.sin(a)*sy*r*edge
            relief=(1-r*r)*(.036+.018*math.sin(px*44)*math.sin(py*39))
            v.append((px,py,surface_height(px,py)+.007+relief))
    for ring in range(rings):
        for j in range(n):f.append((ring*n+j,(ring+1)*n+j,(ring+1)*n+(j+1)%n,ring*n+(j+1)%n))
    o=mesh('Dense shoreline moss carpet',v,f,'ShoreMoss');uv_project(o,3)
    for face in o.data.polygons:face.use_smooth=True

for i,a in enumerate([-2.6,-2.22,-1.87,-1.56,-1.28,-.75,-.48]):
    x=3.66*math.cos(a);y=2.16*math.sin(a)
    if min(math.hypot(x-p[0],y-p[1]) for p in poses)>.57:
        moss_patch(x,y,.29,.19,i)
        moss_patch(x*.93,y*.93,.25,.17,i+.4)
        if i%2==0:
            px=x*.92;py=y*.92
            prop('Foliage_3',(px,py,surface_height(px,py)+.01),.38,a)
moss_patch(.12,-1.60,.24,.20,3)
moss_patch(.51,-1.84,.26,.17,2)

# A few roots descend through joints; tapered ends stop above the water.
for a in [-2.38,-1.72,-.65]:
    x=3.75*math.cos(a);y=2.25*math.sin(a)
    points=[(x*.93,y*.93,.07),(x,y,-.02),(x*1.035,y*1.035,-.17),(x*1.04+.06,y*1.04,-.36)]
    for j in range(3):rod('Shore exposed root',points[j],points[j+1],.028-j*.007,'TrailWood')
