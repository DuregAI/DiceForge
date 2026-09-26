"""Build a separate sculpted T-pose goblin candidate from the supplied drawing.

Run with Blender 5.2 in background. The reference bitmap is deliberately not
used as a texture: its white sticker outline must not appear on the 3D model.
"""
import bpy
import math
import os
import struct
import zlib
from mathutils import Quaternion, Vector

ROOT = "C:/Backforge/Diceforge/DiceForge"
BLEND = ROOT + "/docs/Art/WoodlandDiorama/Goblin_Friend_Candidate.blend"
OUT = ROOT + "/Assets/_Project/07_Art/GoblinFriendCandidate"
CAPTURES = ROOT + "/docs/Art/WoodlandDiorama/Captures"
os.makedirs(OUT, exist_ok=True)
os.makedirs(CAPTURES, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

def material(name, rgb, roughness=0.72, metallic=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m

skin = material("GF_Skin_OliveGold", (.39, .385, .045))
skin_light = material("GF_Skin_Muzzle", (.49, .485, .085))
skin_shadow = material("GF_Skin_Folds", (.315, .345, .075))
ear_inner = material("GF_Ear_Coral", (.70, .225, .15))
ear_shade = material("GF_Ear_Shadow", (.60, .21, .18))
cheek = material("GF_Cheek_Blush", (.68, .29, .13))
eye_socket = material("GF_Eye_Socket", (.29, .22, .10))
eye_white = material("GF_Eye_Ivory", (.97, .90, .70), .31)
iris = material("GF_Iris_Amber", (.38, .20, .08), .27)
pupil = material("GF_Pupil", (.055, .029, .016), .14)
glint = material("GF_Eye_Glint", (1, .98, .82), .12)
hair = material("GF_Hair_Chestnut", (.115, .038, .011))
shirt = material("GF_Shirt_Cream", (.87, .75, .55))
shirt_shadow = material("GF_Shirt_Seams", (.58, .40, .25))
vest = material("GF_Vest_Leather", (.23, .078, .027))
vest_edge = material("GF_Vest_Edge", (.35, .135, .05))
scarf = material("GF_Scarf_Red", (.60, .035, .024))
scarf_fold = material("GF_Scarf_Fold", (.48, .045, .035))
shorts = material("GF_Shorts_Brown", (.29, .10, .035))
boots = material("GF_Boots_Chestnut", (.195, .06, .018))
boots_dark = material("GF_Boots_Sole", (.14, .065, .028))
gold = material("GF_Buckle_Brass", (.80, .48, .115), .38, .46)

def png_rgba(path, size, pixels):
    def chunk(kind, payload):
        data=kind+payload
        return struct.pack(">I",len(payload))+data+struct.pack(">I",zlib.crc32(data)&0xffffffff)
    scanlines=b"".join(b"\0"+pixels[y*size*4:(y+1)*size*4] for y in range(size))
    with open(path,"wb") as stream:
        stream.write(b"\x89PNG\r\n\x1a\n")
        stream.write(chunk(b"IHDR",struct.pack(">IIBBBBB",size,size,8,6,0,0,0)))
        stream.write(chunk(b"IDAT",zlib.compress(scanlines,8)))
        stream.write(chunk(b"IEND",b""))

def srgb(value):
    value=max(0,min(1,value))
    return value*12.92 if value<=.0031308 else 1.055*value**(1/2.4)-.055

def lattice_noise(u,v,frequency):
    u*=frequency
    v*=frequency
    ix=int(math.floor(u))
    iy=int(math.floor(v))
    fx=u-ix
    fy=v-iy
    fx=fx*fx*(3-2*fx)
    fy=fy*fy*(3-2*fy)
    def random_at(x,y):
        n=((x%frequency)*92837111+(y%frequency)*689287499+716327)&0xffffffff
        n=((n^(n>>13))*1274126177)&0xffffffff
        return ((n^(n>>16))&0xffff)/65535
    a=random_at(ix,iy)*(1-fx)+random_at(ix+1,iy)*fx
    b=random_at(ix,iy+1)*(1-fx)+random_at(ix+1,iy+1)*fx
    return a*(1-fy)+b*fy

def surface_height(u,v,style):
    broad=lattice_noise(u,v,6)
    fine=lattice_noise(u,v,32)
    micro=lattice_noise(u,v,64)
    if style=="cloth":
        weave=math.sin(2*math.pi*40*u)*math.sin(2*math.pi*40*v)
        return .57*broad+.22*fine+.10*micro+.11*(.5+.5*weave)
    if style=="leather":
        return .35*broad+.46*fine+.19*micro
    return .72*broad+.22*fine+.06*micro

def tactile_textures(mat,style):
    size=256
    color=bytearray(size*size*4)
    normal=bytearray(size*size*4)
    base=mat.diffuse_color
    contrast={"skin":.055,"cloth":.065,"leather":.075}[style]
    strength={"skin":.8,"cloth":1.2,"leather":1.5}[style]
    for y in range(size):
        for x in range(size):
            u=x/size
            v=y/size
            h=surface_height(u,v,style)
            shade=1+contrast*(h-.5)
            index=(y*size+x)*4
            for channel in range(3):
                warm=(.022*(h-.5) if channel==0 and style=="skin" else 0)
                color[index+channel]=round(255*srgb(base[channel]*shade+warm))
            color[index+3]=255
            dx=(surface_height(u-1/size,v,style)-surface_height(u+1/size,v,style))*strength
            dy=(surface_height(u,v-1/size,style)-surface_height(u,v+1/size,style))*strength
            length=math.sqrt(dx*dx+dy*dy+1)
            normal[index]=round(255*(dx/length*.5+.5))
            normal[index+1]=round(255*(dy/length*.5+.5))
            normal[index+2]=round(255*(1/length*.5+.5))
            normal[index+3]=255
    folder=OUT+"/Textures"
    os.makedirs(folder,exist_ok=True)
    color_path=folder+"/"+mat.name+"_Color.png"
    normal_path=folder+"/"+mat.name+"_Normal.png"
    png_rgba(color_path,size,color)
    png_rgba(normal_path,size,normal)
    nodes=mat.node_tree.nodes
    bsdf=nodes.get("Principled BSDF")
    color_node=nodes.new("ShaderNodeTexImage")
    color_node.image=bpy.data.images.load(color_path,check_existing=True)
    color_node.label="Game color texture"
    mat.node_tree.links.new(color_node.outputs["Color"],bsdf.inputs["Base Color"])
    normal_node=nodes.new("ShaderNodeTexImage")
    normal_node.image=bpy.data.images.load(normal_path,check_existing=True)
    normal_node.image.colorspace_settings.name="Non-Color"
    normal_node.label="Game normal texture"
    map_node=nodes.new("ShaderNodeNormalMap")
    map_node.inputs["Strength"].default_value={"skin":.13,"cloth":.18,"leather":.20}[style]
    mat.node_tree.links.new(normal_node.outputs["Color"],map_node.inputs["Color"])
    mat.node_tree.links.new(map_node.outputs["Normal"],bsdf.inputs["Normal"])

for textured_material,texture_style in (
    (skin,"skin"),(skin_light,"skin"),(ear_inner,"skin"),
    (shirt,"cloth"),(scarf,"cloth"),
    (vest,"leather"),(shorts,"leather"),(boots,"leather")):
    tactile_textures(textured_material,texture_style)

parts = []

def register(obj, name, mat, bone):
    obj.name = name
    if mat:
        obj.data.materials.append(mat)
    obj["rig_bone"] = bone
    for poly in obj.data.polygons:
        poly.use_smooth = True
    parts.append(obj)
    return obj

def sphere(name, center, scale, mat, bone="Torso", segments=32, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return register(obj, name, mat, bone)

def bevel_box(name, center, scale, mat, bone="Torso", bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=2, location=center)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = obj.modifiers.new("Hand softened edge", "BEVEL")
    mod.width = bevel
    mod.segments = 3
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return register(obj, name, mat, bone)

def tapered(name, a, b, radii, mat, bone, vertices=20):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radii[0],
                                   radius2=radii[1], depth=(b-a).length,
                                   location=(a+b)/2)
    obj = bpy.context.object
    obj.rotation_euler = (b-a).to_track_quat("Z", "Y").to_euler()
    return register(obj, name, mat, bone)

def curved_lock(name, path, widths, mat, bone="Head"):
    points=[Vector(point) for point in path]
    verts=[]
    sides=14
    for i,point in enumerate(points):
        tangent=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
        across=Vector((0,1,0)).cross(tangent).normalized()
        depth=tangent.cross(across).normalized()
        for j in range(sides):
            angle=2*math.pi*j/sides
            verts.append(point+across*(math.cos(angle)*widths[i])+depth*(math.sin(angle)*widths[i]*.70))
    faces=[tuple(range(sides-1,-1,-1))]
    for i in range(len(points)-1):
        for j in range(sides):
            nxt=(j+1)%sides
            faces.append((i*sides+j,i*sides+nxt,(i+1)*sides+nxt,(i+1)*sides+j))
    faces.append(tuple((len(points)-1)*sides+j for j in range(sides)))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    mod=obj.modifiers.new("Rounded hair lock","SUBSURF")
    mod.levels=2
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return register(obj,name,mat,bone)

def tube(name, points, radius, mat, bone, resolution=12):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 16
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points)-1)
    for handle, point in zip(spline.bezier_points, points):
        handle.co = point
        handle.handle_left_type = "AUTO"
        handle.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    return register(bpy.context.object, name, mat, bone)

def patch(name, outline, depth, mat, bone, bevel=.015):
    """A soft thin polygon in the X/Z plane; front faces negative Y."""
    n = len(outline)
    verts = [(x, y, z) for x,y,z in outline] + [(x, y+depth, z) for x,y,z in outline]
    faces = [tuple(range(n-1,-1,-1)), tuple(range(n,2*n))]
    for i in range(n):
        j=(i+1)%n
        faces.append((i,j,n+j,n+i))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    if bevel:
        mod=obj.modifiers.new("Soft handmade edge", "BEVEL")
        mod.width=bevel
        mod.segments=2
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return register(obj,name,mat,bone)

def cupped_ear(side):
    """Rounded three-dimensional ear shell, with its pink surface recessed."""
    contour=[(.385,1.706),(.515,1.805),(.716,1.865),(1.006,1.885),
             (1.071,1.860),(.960,1.768),(.786,1.604),(.608,1.527),(.475,1.580)]
    cx=sum(x for x,z in contour)/len(contour)
    cz=sum(z for x,z in contour)/len(contour)
    rings=[(1.0,-.084),(.80,-.091),(.63,-.051),(.31,-.013)]
    vertices=[]
    n=len(contour)
    for factor,y in rings:
        for x,z in contour:
            vertices.append((side*(cx+(x-cx)*factor),y,cz+(z-cz)*factor))
    vertices.append((side*cx,.006,cz))
    for x,z in contour:
        vertices.append((side*x,.078,z))
    faces=[]
    materials=[]
    for band in range(len(rings)-1):
        for j in range(n):
            k=(j+1)%n
            faces.append((band*n+j,band*n+k,(band+1)*n+k,(band+1)*n+j))
            materials.append(0 if band==0 else 1)
    center_index=len(rings)*n
    for j in range(n):
        faces.append(((len(rings)-1)*n+j,(len(rings)-1)*n+(j+1)%n,center_index))
        materials.append(2)
    back_start=center_index+1
    faces.append(tuple(back_start+j for j in range(n)))
    materials.append(0)
    for j in range(n):
        k=(j+1)%n
        faces.append((j,back_start+j,back_start+k,k))
        materials.append(0)
    mesh=bpy.data.meshes.new("Cupped pointed ear")
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    obj=bpy.data.objects.new("Cupped pointed ear",mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(skin)
    obj.data.materials.append(ear_inner)
    obj.data.materials.append(ear_shade)
    for polygon,index in zip(mesh.polygons,materials):
        polygon.material_index=index
        polygon.use_smooth=True
    obj["rig_bone"]="Head"
    parts.append(obj)
    return obj

# Boots, short legs, and the slightly uneven shorts silhouette.
for side, legbone in [(-1,"Leg.L"),(1,"Leg.R")]:
    x=side*.175
    sphere("Boot sole",(x,-.075,.085),(.185,.255,.072),boots_dark,legbone)
    sphere("Boot toe",(x,-.145,.155),(.176,.208,.112),boots,legbone)
    sphere("Boot ankle",(x,.015,.231),(.132,.145,.138),boots,legbone)
    sphere("Rolled boot cuff",(x,.012,.335),(.154,.155,.058),vest_edge,legbone)
    tapered("Exposed calf",(x,0,.338),(x,0,.575),(.087,.099),skin,legbone)
    tapered("Shorts leg",(x,0,.49),(x,0,.756),(.185,.164),shorts,legbone,28)
    sphere("Soft shorts hem",(x,0,.502),(.186,.169,.033),vest_edge,legbone)
    tube("Shorts seam",[(x,-.184,.681),(x-.008,-.190,.608),
         (x,-.184,.524)],.006,vest_edge,legbone)
    patch("Shorts repair patch",[(x+side*.045,-.177,.604),(x+side*.144,-.117,.616),
          (x+side*.142,-.118,.532),(x+side*.064,-.164,.530)],.008,vest_edge,legbone,.009)
    sphere("Boot side rivet",(x+side*.123,-.213,.218),(.015,.009,.015),gold,legbone)
    tube("Boot toe seam",[(x-.119,-.319,.154),(x,-.351,.172),
         (x+.119,-.319,.154)],.009,vest_edge,legbone)
    tube("Boot ankle leather seam",[(x-.099,-.113,.290),(x,-.146,.300),
         (x+.099,-.113,.290)],.008,vest_edge,legbone)

sphere("Shorts waist",(0,0,.735),(.355,.214,.177),shorts,"Hips")
sphere("Soft shirt torso",(0,0,1.02),(.316,.231,.327),shirt,"Torso")
sphere("Leather vest back",(0,.134,1.035),(.301,.137,.290),vest,"Torso")
sphere("Belt leather",(0,-.02,.758),(.347,.211,.056),vest,"Hips")
bevel_box("Brass buckle outer",(0,-.230,.766),(.094,.025,.065),gold,"Hips",.025)
bevel_box("Buckle dark center",(0,-.257,.766),(.059,.010,.037),vest,"Hips",.013)
tube("Buckle tongue",[(-.002,-.270,.770),(.042,-.270,.768)],.008,gold,"Hips")

# Vest front leaves the central shirt visible, like the supplied costume.
for side in (-1,1):
    s=side
    sphere("Vest shoulder strip",(s*.232,.048,1.251),(.091,.145,.095),vest,"Torso")
    patch("Rear shorts pocket",[(s*.057,.205,.665),(s*.213,.143,.652),
          (s*.211,.142,.557),(s*.073,.203,.561)],.008,vest_edge,"Hips",.01)
    patch("Leather vest panel",[(s*.09,-.247,1.235),(s*.310,-.124,1.25),
          (s*.325,-.153,.813),(s*.130,-.224,.815),(s*.125,-.259,.928)],
          .025,vest,"Torso",.022)
    tube("Vest piping",[(s*.107,-.260,1.208),(s*.132,-.270,1.00),
         (s*.129,-.245,.825)],.011,vest_edge,"Torso")
    sphere("Vest bottom fold",(s*.220,-.136,.822),(.111,.08,.035),vest_edge,"Torso")
    tube("Vest edge stitches",[(s*.153,-.266,1.105),(s*.165,-.267,.992),
         (s*.155,-.247,.886)],.004,shirt_shadow,"Torso")
    tapered("Folded shirt sleeve",(s*.323,0,1.205),(s*.501,0,1.205),
            (.153,.116),shirt,"Arm."+("L" if s<0 else "R"),24)
    sphere("Rolled sleeve cuff",(s*.493,0,1.205),(.041,.124,.123),shirt_shadow,
           "Arm."+("L" if s<0 else "R"))
    sphere("Cuff face",(s*.508,0,1.205),(.031,.119,.117),shirt,
           "Arm."+("L" if s<0 else "R"))
    tapered("Straight forearm",(s*.479,0,1.205),(s*.931,0,1.205),
            (.092,.071),skin,"Arm."+("L" if s<0 else "R"))
    sphere("Hand palm",(s*1.012,-.004,1.205),(.123,.073,.060),skin,"Arm."+("L" if s<0 else "R"))
    for j,zoff in enumerate((.038,.012,-.014,-.038)):
        tapered("Open hand finger",(s*1.08,-.011,1.205+zoff),
                (s*(1.183-.011*j),-.012,1.205+zoff*.85),
                (.023,.016),skin,"Arm."+("L" if s<0 else "R"),12)
    tapered("Open thumb",(s*.988,-.035,1.166),(s*1.066,-.086,1.100),
            (.034,.022),skin,"Arm."+("L" if s<0 else "R"),12)

# Shirt cord and signature neckerchief.
tube("Shirt lacing A",[(-.044,-.240,1.052),(.043,-.244,.995)],.013,vest_edge,"Torso")
tube("Shirt lacing B",[(.044,-.240,1.052),(-.043,-.244,.995)],.013,vest_edge,"Torso")
sphere("Neck",(0,0,1.319),(.170,.166,.110),skin,"Head")
sphere("Scarf collar",(0,-.028,1.288),(.291,.219,.075),scarf,"Torso")
sphere("Scarf knot behind neck",(0,.225,1.271),(.09,.064,.055),scarf_fold,"Torso")
patch("Scarf hanging point",[(-.270,-.201,1.294),(-.013,-.246,1.038),
      (.269,-.200,1.295),(.102,-.233,1.242),(0,-.264,1.212),(-.123,-.229,1.247)],
      .032,scarf,"Torso",.012)
tube("Scarf lower fold",[(-.19,-.240,1.239),(-.011,-.281,1.149),
     (.187,-.234,1.240)],.008,scarf_fold,"Torso")

# Oversized rounded cranium, projecting cheek/muzzle volume and pointed ears.
sphere("Head main",(0,-.012,1.666),(.477,.331,.415),skin,"Head",48,32)
sphere("Lower rounded jaw",(0,-.094,1.455),(.370,.291,.180),skin,"Head",40,24)
sphere("Muzzle",(0,-.300,1.470),(.275,.087,.117),skin_light,"Head")
for side in (-1,1):
    s=side
    cupped_ear(s)
    sphere("Cheek roundness",(s*.270,-.247,1.484),(.146,.102,.114),skin_light,"Head")
    sphere("Cheek blush",(s*.298,-.335,1.505),(.082,.017,.046),cheek,"Head")
    sphere("Deep eye socket",(s*.197,-.286,1.713),(.122,.043,.111),skin_shadow,"Head")
    sphere("Large ivory eye",(s*.195,-.322,1.718),(.103,.050,.098),eye_white,"Head")
    sphere("Amber iris",(s*.195,-.366,1.706),(.067,.020,.067),iris,"Head")
    sphere("Dark pupil",(s*.185,-.387,1.706),(.047,.010,.048),pupil,"Head")
    sphere("Top eye glint",(s*.166,-.397,1.736),(.017,.006,.018),glint,"Head")
    sphere("Lower eye glint",(s*.218,-.397,1.681),(.007,.005,.009),glint,"Head")
    tube("Soft upper eyelid",[(s*.095,-.333,1.759),(s*.191,-.356,1.809),
         (s*.293,-.328,1.765)],.014,skin_shadow,"Head")
    tube("Expressive eyebrow",[(s*.083,-.337,1.916),(s*.195,-.321,1.970),
         (s*.299,-.283,1.927)],.026,hair,"Head")
    sphere("Smile corner",(s*.206,-.395,1.422),(.015,.011,.015),pupil,"Head")

sphere("Warm peach nose",(0,-.376,1.542),(.088,.081,.064),cheek,"Head")
for side in (-1,1):
    sphere("Nostril",(side*.043,-.448,1.523),(.012,.006,.009),vest,"Head")
tube("Wide happy smile",[(-.205,-.393,1.421),(-.103,-.397,1.397),
     (0,-.399,1.385),(.103,-.397,1.397),(.205,-.393,1.421)],.010,pupil,"Head")
tongue=material("GF_Smile_Tongue", (.58, .16, .14))
sphere("Open smile shadow",(0,-.396,1.402),(.128,.010,.035),eye_socket,"Head")
sphere("Warm tongue",(0,-.407,1.385),(.074,.006,.012),tongue,"Head")
tube("Soft lower lip",[(-.132,-.390,1.375),(0,-.396,1.363),
     (.132,-.390,1.375)],.009,skin_shadow,"Head")

# Three curved locks. No external white/transparent image plane is used.
curved_lock("Hair central sweep",[(-.046,-.020,1.990),(-.065,-.024,2.085),
            (-.025,-.016,2.191),(.062,.007,2.272)],[.111,.086,.050,.006],hair)
curved_lock("Hair side sweep",[(.016,-.015,2.009),(.087,-.028,2.081),
            (.181,-.020,2.141),(.262,.007,2.152)],[.103,.084,.042,.006],hair)
curved_lock("Hair front flick",[(-.088,-.111,2.003),(-.034,-.119,2.082),
            (.070,-.109,2.117),(.119,-.074,2.100)],[.075,.059,.029,.004],hair)

# Rigid part groups are bound to a simple armature, suitable for later cleanup.
arm_data=bpy.data.armatures.new("GoblinFriend_Rig")
arm=bpy.data.objects.new("GoblinFriend_Rig",arm_data)
bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm
arm.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bones=[
    ("Root",(0,0,.03),(0,0,.18),None),
    ("Hips",(0,0,.48),(0,0,.83),"Root"),
    ("Torso",(0,0,.83),(0,0,1.31),"Hips"),
    ("Head",(0,0,1.31),(0,0,1.80),"Torso"),
    ("Arm.L",(-.32,0,1.22),(-.94,0,1.22),"Torso"),
    ("Arm.R",(.32,0,1.22),(.94,0,1.22),"Torso"),
    ("Leg.L",(-.17,0,.67),(-.17,0,.15),"Hips"),
    ("Leg.R",(.17,0,.67),(.17,0,.15),"Hips"),
]
for name,head,tail,parent in bones:
    bone=arm_data.edit_bones.new(name)
    bone.head=head
    bone.tail=tail
    if parent: bone.parent=arm_data.edit_bones[parent]
bpy.ops.object.mode_set(mode="OBJECT")
arm.select_set(False)

for obj in parts:
    group=obj.vertex_groups.new(name=obj["rig_bone"])
    group.add(list(range(len(obj.data.vertices))),1,"REPLACE")
    obj.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
mesh=bpy.context.object
mesh.name="GoblinFriend_LOD0"
mesh.parent=arm
rig_modifier=mesh.modifiers.new("Skin to simple rig","ARMATURE")
rig_modifier.object=arm

# Preserve the T-pose as the bind pose while exporting a relaxed, looping idle.
# Each arm remains a separate rigid skinned group; this is a preview animation,
# and the shoulder/hand deformation needs an authored production pass.
arm.animation_data_create()
idle=bpy.data.actions.new("GF_Idle")
arm.animation_data.action=idle

def rotate_about_world_y(bone_name, angle):
    pose_bone=arm.pose.bones[bone_name]
    pose_bone.rotation_mode="QUATERNION"
    local_axis=arm_data.bones[bone_name].matrix_local.to_3x3().inverted() @ Vector((0,1,0))
    pose_bone.rotation_quaternion=Quaternion(local_axis, angle)

for frame, sway in ((1,0),(16,1),(31,0),(46,-1),(61,0)):
    bpy.context.scene.frame_set(frame)
    for bone_name, angle in (("Arm.L",-.42+sway*.025),
                             ("Arm.R",.42+sway*.025),
                             ("Head",sway*.026),
                             ("Torso",sway*.012)):
        rotate_about_world_y(bone_name,angle)
        arm.pose.bones[bone_name].keyframe_insert(data_path="rotation_quaternion",frame=frame,group=bone_name)

# The board mover supplies the actual jump arc. This loop adds anticipation and
# an arm/head reaction without changing the logical position or adding root motion.
walk=bpy.data.actions.new("GF_Walk")
walk.use_fake_user=True
arm.animation_data.action=walk
for frame, beat in ((1,0),(7,1),(13,0),(19,-1),(25,0)):
    bpy.context.scene.frame_set(frame)
    for bone_name, angle in (("Arm.L",-.48-beat*.09),
                             ("Arm.R",.48-beat*.09),
                             ("Head",-beat*.055),
                             ("Torso",beat*.045)):
        rotate_about_world_y(bone_name,angle)
        arm.pose.bones[bone_name].keyframe_insert(data_path="rotation_quaternion",frame=frame,group=bone_name)
idle.use_fake_user=True
arm.animation_data.action=idle
bpy.context.scene.frame_end=61
bpy.context.scene.render.fps=30
bpy.context.scene.frame_set(1)

# Production export contains only the model and rig; studio objects remain in .blend.
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
mesh.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=OUT+"/GoblinFriend_Candidate.fbx",use_selection=True,
    add_leaf_bones=False,axis_forward="-Z",axis_up="Y",bake_anim=True,
    bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,
    path_mode="AUTO",apply_unit_scale=True)
arm.animation_data.action=walk
bpy.context.scene.frame_end=25
bpy.context.scene.frame_set(1)
bpy.ops.export_scene.fbx(filepath=OUT+"/GoblinFriend_Walk.fbx",use_selection=True,
    add_leaf_bones=False,axis_forward="-Z",axis_up="Y",bake_anim=True,
    bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,
    path_mode="AUTO",apply_unit_scale=True)
arm.animation_data.action=idle
bpy.context.scene.frame_end=61
bpy.context.scene.frame_set(1)

# Studio lighting and three useful inspection angles in the source file.
world=bpy.context.scene.world
world.color=(.32,.36,.39)
world.use_nodes=True
world.node_tree.nodes["Background"].inputs["Color"].default_value=(.42,.48,.55,1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value=.45

def area(name,position,power,size):
    data=bpy.data.lights.new(name,"AREA")
    data.energy=power
    data.shape="DISK"
    data.size=size
    obj=bpy.data.objects.new(name,data)
    bpy.context.collection.objects.link(obj)
    obj.location=position
    obj.rotation_euler=(Vector((0,0,1.15))-obj.location).to_track_quat("-Z","Y").to_euler()

area("Warm key",(-3,-4,5),650,4)
area("Cool fill",(3,-2,3),260,4)
area("Rim",(1.5,3,4),400,3)

camera_data=bpy.data.cameras.new("Candidate front camera")
camera=bpy.data.objects.new("Candidate front camera",camera_data)
bpy.context.collection.objects.link(camera)
camera.location=(0,-7.9,1.17)
camera.rotation_euler=(Vector((0,0,1.17))-camera.location).to_track_quat("-Z","Y").to_euler()
camera_data.type="ORTHO"
camera_data.ortho_scale=2.8
bpy.context.scene.camera=camera

scene=bpy.context.scene
scene.render.engine="CYCLES"
scene.cycles.samples=24
scene.view_settings.view_transform="Standard"
scene.view_settings.look="Medium High Contrast"
scene.view_settings.exposure=-.35
scene.render.resolution_x=768
scene.render.resolution_y=768
scene.render.resolution_percentage=100
scene.render.image_settings.file_format="PNG"
scene.render.filepath=CAPTURES+"/goblin-candidate-front.png"
bpy.ops.render.render(write_still=True)

camera.location=(3.6,-6.6,2.0)
camera.rotation_euler=(Vector((0,0,1.12))-camera.location).to_track_quat("-Z","Y").to_euler()
scene.render.filepath=CAPTURES+"/goblin-candidate-three-quarter.png"
bpy.ops.render.render(write_still=True)

camera.location=(2.7,6.8,2.0)
camera.rotation_euler=(Vector((0,0,1.12))-camera.location).to_track_quat("-Z","Y").to_euler()
scene.render.filepath=CAPTURES+"/goblin-candidate-back.png"
bpy.ops.render.render(write_still=True)

# Save with the three-quarter camera as default.
camera.location=(3.6,-6.6,2.0)
camera.rotation_euler=(Vector((0,0,1.12))-camera.location).to_track_quat("-Z","Y").to_euler()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("Created candidate:",BLEND,OUT)
