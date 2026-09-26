"""Submerged shelf and a short cascade for the free water material."""
random.seed(924)
mat=bpy.data.materials.new('ShoreSand');mat.diffuse_color=(.55,.52,.38,1);mat.use_nodes=True
materials['ShoreSand']=mat
# Broad shallow shelf slopes into the lake; it supplies actual depth beneath transparent water.
v=[];f=[];n=128
for k,(radius,z) in enumerate([(.85,-.88),(1.04,-.96),(1.23,-1.12),(1.55,-1.65),(2.3,-3.6),(12,-5)]):
    for j in range(n):
        a=j*math.tau/n;r=radius*(1+.026*math.sin(a*7))
        v.append((3.75*r*math.cos(a),2.25*r*math.sin(a),z+.025*math.sin(a*11)))
for k in range(5):
    for j in range(n):f.append((k*n+j,(k+1)*n+j,(k+1)*n+(j+1)%n,k*n+(j+1)%n))
bed=mesh('Submerged sandy lake shelf',v,f,'ShoreSand');uv_project(bed,.35)
for face in bed.data.polygons:face.use_smooth=True
for j in range(22):
    a=-2.9+j*.13;r=random.uniform(1.10,1.30)
    ball('Underwater pebble',(3.75*r*math.cos(a),2.25*r*math.sin(a),-1.04),(.12,.09,.055),'ShoreStoneCool')

# Sculpt the creek into the ground, preserving interpolated ground UVs.
import bmesh
from mathutils.kdtree import KDTree
tree=KDTree(len(samples))
for index,p in enumerate(samples):tree.insert(Vector((p.x,p.y,0)),index)
tree.balance()
for o in scene.objects:
    if o.name.startswith('Trail island'):
        bm=bmesh.new();bm.from_mesh(o.data)
        bmesh.ops.delete(bm,geom=[face for face in bm.faces if face.normal.z>.7],context='FACES')
        bm.to_mesh(o.data);bm.free()
    if o.name.startswith('Living forest floor'):
        bm=bmesh.new();bm.from_mesh(o.data)
        bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=2,use_grid_fill=True)
        for vert in bm.verts:
            _,_,distance=tree.find(Vector((vert.co.x,vert.co.y,0)))
            t=max(0,min(1,(distance-.13)/.25));blend=1-t*t*(3-2*t)
            vert.co.z-=.24*blend
        bm.to_mesh(o.data);bm.free();o.data.update()

for o in scene.objects:
    if o.name.startswith('Water creek flowing'):
        for vert in o.data.vertices:vert.co.z=.045

# Curved sheet follows the outlet over the edge to the lake (continuous river UVs).
v=[];f=[];uvs=[]
for j in range(25):
    t=j/24;x=1.96+.38*t;y=-1.91-.65*t
    z=.045-.805*(t*t*(3-2*t))
    width=.245*(1-.22*t)
    for side in [-1,1]:
        v.append((x+side*width,y,z));uvs.append(((side+1)*.5,5+t*1.7))
for j in range(24):f.append((j*2,j*2+1,j*2+3,j*2+2))
fall=mesh('Creek outlet cascade',v,f,'TrailWater')
uv=fall.data.uv_layers.new(name='UVMap')
for face in fall.data.polygons:
    face.use_smooth=True
    for li in face.loop_indices:uv.data[li].uv=uvs[fall.data.loops[li].vertex_index]
# Remove the rigid rings from the previous shoreline: the material now draws contact foam.
for o in list(scene.objects):
    if o.name.startswith('Shore contact ripples'):
        remove(o);continue
    if o.name.startswith('Carved shoreline stone'):
        center=sum((vert.co for vert in o.data.vertices),Vector())/len(o.data.vertices)
        if math.hypot(center.x-1.97,center.y+1.95)<.48:remove(o)

# Wet rocks frame the short fall, with a separate animated impact foam patch below it.
shore_stone('Waterfall left lip',1.64,-2.06,.19,.21,-.45,.08,.6)
shore_stone('Waterfall right lip',2.38,-1.92,.22,.20,-.50,.10,.2)
foamMat=bpy.data.materials.new('TrailSplash');foamMat.diffuse_color=(.7,.9,.84,1);foamMat.use_nodes=True;materials['TrailSplash']=foamMat
o=mesh('Waterfall landing foam',[(1.75,-3.05,-.704),(2.95,-3.05,-.704),(2.95,-2.05,-.704),(1.75,-2.05,-.704)],[(0,1,2,3)],'TrailSplash')
layer=o.data.uv_layers.new(name='UVMap')
for face in o.data.polygons:
    for li in face.loop_indices:layer.data[li].uv=[(0,0),(1,0),(1,1),(0,1)][o.data.loops[li].vertex_index]

# Foreground foliage sits close to the fixed camera, outside the playable silhouette.
for side in [-1,1]:
    for j in range(3):
        x=side*(3.10+j*.25);y=-8.0+j*.16;z=5.90+j*.12
        rod('Foreground soft branch',(x+side*.7,y,z-.45),(x-side*.35,y+.12,z+.25),.045,'HeroBark')
        for k in range(6):
            px=x+side*(k*.16-.30);py=y+(k%2)*.23;pz=z+.12*math.sin(k)
            leaf=ball('Foreground soft leaf',(px,py,pz),(.28,.13,.075),'ShoreLeaf')
            leaf.rotation_euler=(.2,-.25,side*.6+(k%2)*.7)
