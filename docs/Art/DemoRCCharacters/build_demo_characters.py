"""Build the five Demo RC field characters in an isolated Blender process.

Run: blender --background --factory-startup --python this_file.py -- [tish luma bum ryzh bark]
The Woodland Goblin Friend sculpt is the shared construction vocabulary, not a
texture or a recolour: the variants change anatomy, costumes, hair and props.
Only DemoRCCharacters output folders are written. The user's open scene is never
loaded. Eight bone names, hierarchy and local axes stay compatible with GF clips.
"""
import argparse
import hashlib
import json
import math
import os
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Quaternion, Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "docs/Art/WoodlandDiorama/build_goblin_candidate.py"
ART = ROOT / "docs/Art/DemoRCCharacters"
MODELS = ROOT / "Assets/_Project/07_Art/DemoRCCharacters/Models"
RENDERS = ART / "Renders"
SOURCES = ART / "Sources"
for folder in (MODELS, RENDERS, SOURCES):
    folder.mkdir(parents=True, exist_ok=True)

PROPORTIONS = {
    "tish": dict(height=1.00, torso=1.00, head=1.00, limbs=1.00),
    "luma": dict(height=1.03, torso=.94, head=.96, limbs=.95),
    "bum": dict(height=1.36, torso=1.58, head=1.10, limbs=1.34),
    "ryzh": dict(height=1.01, torso=.87, head=.97, limbs=.91),
}
COLOURS = {
    "tish": {"skin":"97A953", "hair":"58402B", "shirt":"DDD0AC", "vest":"704B31", "scarf":"D66F5A"},
    "luma": {"skin":"85A56E", "hair":"405B34", "shirt":"E7DBB9", "vest":"785C40", "scarf":"63AFA4"},
    "bum": {"skin":"849A62", "hair":"536337", "shirt":"E5D7AF", "vest":"C49A4B", "scarf":"C49A4B"},
    "ryzh": {"skin":"97AC60", "hair":"BA643C", "shirt":"EFE1C5", "vest":"A34F3E", "scarf":"A34F3E"},
}

def linear(hex_colour):
    values = [int(hex_colour[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in values)

def srgb(value):
    return value*12.92 if value<=.0031308 else 1.055*value**(1/2.4)-.055

def set_colour(material, rgb):
    material.diffuse_color = (*rgb, 1)
    material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)

def recolour(ns, key):
    colours = COLOURS[key]
    for name in ("skin", "hair", "shirt", "vest", "scarf"):
        set_colour(ns[name], linear(colours[name]))
    base = linear(colours["skin"])
    set_colour(ns["skin_light"], tuple(min(1, c*1.16+.02) for c in base))
    set_colour(ns["skin_shadow"], tuple(c*.68 for c in base))
    set_colour(ns["scarf_fold"], tuple(c*.69 for c in linear(colours["scarf"])))
    set_colour(ns["vest_edge"], tuple(min(1, c*1.25) for c in linear(colours["vest"])))
    set_colour(ns["ear_inner"], linear("C47B65"))
    set_colour(ns["ear_shade"], linear("9C604E"))
    set_colour(ns["cheek"], linear("C48C67"))
    set_colour(ns["shorts"], linear("67533B"))
    set_colour(ns["boots"], linear("634832"))
    set_colour(ns["boots_dark"], linear("473628"))
    set_colour(ns["eye_white"], linear("FFF1D4"))
    set_colour(ns["iris"], linear("AA6A29"))
    set_colour(ns["gold"], linear("C69D50"))

def woodland_base(key):
    source = SOURCE.read_text(encoding="utf-8").split("# Rigid part groups")[0]
    # Reduce primitive density; silhouette detail is retained at field scale.
    source = source.replace('OUT = ROOT + "/Assets/_Project/07_Art/GoblinFriendCandidate"',
                            'OUT = "' + str(MODELS).replace("\\", "/") + '"')
    source = source.replace('CAPTURES = ROOT + "/docs/Art/WoodlandDiorama/Captures"',
                            'CAPTURES = "' + str(RENDERS).replace("\\", "/") + '"')
    first = source.index("for textured_material,texture_style in (")
    last = source.index("parts = []", first)
    source = source[:first] + source[last:]
    source = source.replace("segments=32, rings=20", "segments=20, rings=12")
    source = source.replace(',48,32)', ',28,18)').replace(',40,24)', ',24,16)')
    source = source.replace('mod.levels=2', 'mod.levels=1')
    source = source.replace('curve.resolution_u = 16', 'curve.resolution_u = 5')
    source = source.replace('curve.bevel_resolution = 3', 'curve.bevel_resolution = 1')
    source = source.replace('sides=14', 'sides=10')
    ns = {"__name__":"__demo_character_base__"}
    exec(compile(source, str(SOURCE), "exec"), ns)
    recolour(ns, key)
    p = PROPORTIONS[key]
    for obj in list(ns["parts"]):
        name = obj.name
        bone = obj["rig_bone"]
        if key in ("luma", "bum", "ryzh") and name.startswith("Hair "):
            ns["parts"].remove(obj)
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        if key in ("bum", "ryzh") and name.startswith("Scarf "):
            ns["parts"].remove(obj)
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        # Transform in world space. The source vertices are object-local.
        for v in obj.data.vertices:
            pos = obj.matrix_world @ v.co
            if bone == "Head":
                pos.x *= p["head"]
                pos.y *= p["head"]
                if key == "bum":
                    pos.z = 1.68 + (pos.z - 1.68)*.94
                if key == "tish" and name.startswith("Cupped pointed ear") and pos.x < -.94 and pos.z > 1.78:
                    pos.z -= .055 + .025*max(0, 1-abs(pos.x+1.00)/.06)
            elif bone.startswith("Arm"):
                pos.x *= p["limbs"]
                pos.y *= (1.18 if key == "bum" else 1)
                if key == "bum":
                    pos.z = 1.205 + (pos.z-1.205)*1.18
            elif bone.startswith("Leg"):
                pos.x *= (1.32 if key == "bum" else p["torso"])
                pos.y *= (1.22 if key == "bum" else 1)
            else:
                pos.x *= p["torso"]
                pos.y *= (1.27 if key == "bum" else .96 if key == "ryzh" else 1)
            pos.z *= p["height"]
            v.co = obj.matrix_world.inverted() @ pos
    return ns

def accessories(ns, key):
    sphere, box, patch, tube, lock = (ns[n] for n in ("sphere", "bevel_box", "patch", "tube", "curved_lock"))
    material = ns["material"]
    skin, hair, cloth, leather, edge, gold = (ns[n] for n in ("skin", "hair", "shirt", "vest", "vest_edge", "gold"))
    if key == "tish":
        bag = material("Tish_Satchel_Cloth", linear("766048"))
        box("Tish shoulder satchel",(.388,.008,.76),(.156,.147,.175),bag,"Torso",.07)
        patch("Tish satchel flap",[(.25,-.150,.917),(.52,-.120,.917),(.512,-.148,.786),(.38,-.161,.750),(.25,-.165,.789)],.015,bag,"Torso",.022)
        tube("Tish cross-body strap",[(-.213,-.20,1.22),(-.06,-.262,1.05),(.18,-.26,.91),(.33,-.165,.83)],.035,bag,"Torso")
        box("Tish satchel latch",(.40,-.181,.804),(.035,.010,.024),gold,"Torso",.008)
        for z in (.81,.86):
            tube("Tish satchel stitch",[(.266,-.173,z),(.334,-.178,z)],.0035,cloth,"Torso")
    elif key == "luma":
        # Longer trousers and a wide triangular capelet; no chest plate.
        for side,bone in ((-1,"Leg.L"),(1,"Leg.R")):
            ns["tapered"]("Luma trail trouser calf",(side*.165,0,.338*1.03),(side*.165,0,.575*1.03),(.104,.120),ns["shorts"],bone,20)
        cape = ns["scarf"]
        patch("Luma triangular shoulder cape",[(-.37,.165,1.295),(-.35,.26,1.17),(-.05,.335,.965),(.29,.22,1.08),(.37,.154,1.295)],.02,cape,"Torso",.018)
        leaf = material("Luma_Leaf_Clasp",linear("B7C58A"))
        patch("Luma leaf clasp",[(-.044,-.274,1.283),(.043,-.279,1.324),(.072,-.272,1.248),(.027,-.290,1.187),(-.033,-.280,1.222)],.018,leaf,"Torso",.012)
        tube("Luma leaf vein",[(.016,-.302,1.208),(.025,-.305,1.254),(.031,-.296,1.295)],.004,edge,"Torso")
        lock("Luma sweeping fringe",[(.30,-.15,2.06),(.17,-.24,2.14),(-.11,-.22,2.14),(-.32,-.14,1.98)],[.14,.12,.075,.009],hair)
        lock("Luma crown sweep",[(.33,.04,1.99),(.23,.12,2.16),(-.07,.11,2.19),(-.35,.02,2.03)],[.16,.12,.105,.012],hair)
        # Short, side-visible segmented braid, laid over the shoulder.
        for j in range(4):
            z = 1.72 - j*.10
            x = -.502 + (.022 if j%2 else -.022)
            braid = sphere("Luma side braid",(x,-.10,z),(.082,.075,.084),hair,"Head",16,10)
            braid.rotation_euler.y = (-.33 if j%2 else .33)
        sphere("Luma braid tie",(-.502,-.103,1.357),(.052,.06,.027),cape,"Head",16,10)
        lock("Luma braid tail",[(-.502,-.10,1.35),(-.511,-.108,1.307),(-.547,-.11,1.278)],[.055,.038,.005],hair)
        paper = material("Luma_Map_Linen",linear("D2BD88"))
        box("Luma rolled route map",(.30,.20,.846),(.038,.051,.167),paper,"Torso",.03)
        tube("Luma map tie",[(.28,.151,.90),(.32,.150,.86),(.335,.171,.83)],.010,leather,"Torso")
    elif key == "bum":
        h=1.36
        bag=material("Bum_Backpack_SoftCloth",linear("8B7050"))
        repair=material("Bum_Backpack_Repair",linear("AE9368"))
        box("Bum large soft backpack",(0,.414*h,1.05*h),(.448*h,.238*h,.425*h),bag,"Torso",.135*h)
        box("Bum backpack rounded flap",(0,.47*h,1.40*h),(.445*h,.23*h,.105*h),bag,"Torso",.09*h)
        box("Bum backpack patch",(.105*h,.665*h,.99*h),(.110*h,.010,.100*h),repair,"Torso",.025*h)
        for side in (-1,1):
            tube("Bum backpack shoulder strap",[(side*.29*h,.29*h,1.39*h),(side*.31*h,-.18*h,1.26*h),(side*.32*h,-.28*h,.98*h)],.046*h,bag,"Torso")
            box("Bum backpack strap buckle",(side*.316*h,-.30*h,1.07*h),(.055*h,.018,.068*h),gold,"Torso",.012)
            tube("Bum bag seam",[(side*.35*h,.59*h,1.33*h),(side*.38*h,.63*h,1.10*h),(side*.34*h,.61*h,.75*h)],.012*h,repair,"Torso")
        lock("Bum leafy short tuft",[(0,-.025,2.025*h),(-.015,-.012,2.14*h),(.073,.024,2.21*h)],[.10*h,.078*h,.006],hair)
        lock("Bum quiet side tuft",[(.06,.01,2.025*h),(.13,.00,2.10*h),(.21,.015,2.11*h)],[.077*h,.044*h,.004],hair)
        # Two big fabric pockets emphasize the wide calm silhouette.
        for side in (-1,1):
            patch("Bum vest broad pocket",[(side*.19*h,-.289*h,.985*h),(side*.423*h,-.203*h,.985*h),(side*.42*h,-.205*h,.851*h),(side*.21*h,-.28*h,.851*h)],.017,leather,"Torso",.025)
    elif key == "ryzh":
        for i,(path,width) in enumerate((
            ([(.04,-.06,1.99),(-.02,-.06,2.18),(.08,-.015,2.32)],[.115,.089,.005]),
            ([(.03,-.06,2.03),(.17,-.10,2.19),(.37,-.03,2.20)],[.12,.08,.005]),
            ([(.11,.055,2.01),(.27,.06,2.16),(.43,.07,2.08)],[.09,.075,.004]),
            ([(-.11,-.08,2.00),(-.19,-.10,2.12),(-.15,-.04,2.23)],[.085,.05,.004]))):
            lock("Ryzh rust swept crest "+str(i),path,width,hair)
        badge=material("Ryzh_Acorn_Amber",linear("D5A553"))
        cap=material("Ryzh_Acorn_Cap",linear("72543A"))
        sphere("Ryzh acorn sign",(-.16,-.254,1.02),(.052,.014,.070),badge,"Torso",16,10)
        sphere("Ryzh acorn cap",(-.16,-.256,1.07),(.063,.016,.025),cap,"Torso",16,10)
        tube("Ryzh acorn stem",[(-.16,-.253,1.08),(-.159,-.249,1.11)],.008,cap,"Torso")

def bark_model():
    # The same helpers create a genuinely separate six-legged anatomy.
    source = SOURCE.read_text(encoding="utf-8").split("# Boots, short legs")[0]
    source = source.replace('OUT = ROOT + "/Assets/_Project/07_Art/GoblinFriendCandidate"','OUT = "'+str(MODELS).replace("\\","/")+'"')
    source = source.replace('CAPTURES = ROOT + "/docs/Art/WoodlandDiorama/Captures"','CAPTURES = "'+str(RENDERS).replace("\\","/")+'"')
    first=source.index("for textured_material,texture_style in (")
    last=source.index("parts = []",first)
    source=source[:first]+source[last:]
    source=source.replace("segments=32, rings=20","segments=20, rings=12").replace("curve.resolution_u = 16","curve.resolution_u = 5").replace("curve.bevel_resolution = 3","curve.bevel_resolution = 1")
    ns={"__name__":"__demo_bark__"}
    exec(compile(source,str(SOURCE),"exec"),ns)
    mat,sphere,tube,patch,taper=(ns[n] for n in ("material","sphere","tube","patch","tapered"))
    wood=mat("Bark_Shell_WarmWood",linear("9A7450"))
    ridge=mat("Bark_CrackedBark",linear("654735"))
    face=mat("Bark_Face_Wood",linear("B89969"))
    moss=mat("Bark_Sparse_Moss",linear("798343"))
    amber=mat("Bark_Amber_Eyes",linear("D9A34B"),.32)
    sphere("Bark underside",(0,.10,.37),(.47,.52,.23),ridge)
    sphere("Bark left shell",(-.175,.12,.72),(.39,.54,.43),wood)
    sphere("Bark right shell",(.175,.12,.72),(.39,.54,.43),wood)
    def shell_surface(x,y):
        heights=[]
        for center in (-.175,.175):
            inside=1-((x-center)/.39)**2-((y-.12)/.54)**2
            if inside>=0:heights.append(.72+.43*math.sqrt(inside))
        return max(heights) if heights else .72
    tube("Bark shell center seam",[(0,y,shell_surface(0,y)+.006) for y in (-.30,-.12,.12,.34,.55)],.013,ridge,"Torso")
    # Large directional strips, rather than geometric noise, read as fallen bark.
    for side in (-1,1):
        for j in range(4):
            y=-.22+j*.205
            tube("Bark shallow grain seam",[(side*x,y,shell_surface(side*x,y)+.005) for x in (.10,.20,.31,.43,.50)],.011,ridge,"Torso")
            mx=side*(.20+.028*j);my=y+.02
            sphere("Bark moss tuft",(mx,my,shell_surface(mx,my)+.015),(.084,.052,.023),moss,"Torso",14,8)
    sphere("Bark friendly head",(0,-.397,.55),(.368,.268,.303),face,"Head",28,18)
    for side in (-1,1):
        sphere("Bark eye socket",(side*.192,-.617,.625),(.127,.051,.147),ridge,"Head")
        sphere("Bark ivory eye",(side*.192,-.654,.631),(.110,.039,.129),ns["eye_white"],"Head")
        sphere("Bark amber iris",(side*.187,-.686,.631),(.075,.014,.088),amber,"Head")
        sphere("Bark dark pupil",(side*.182,-.699,.635),(.047,.010,.065),ns["pupil"],"Head")
        sphere("Bark eye glint",(side*.17,-.709,.674),(.018,.006,.022),ns["glint"],"Head",12,8)
        tube("Bark antenna",[(side*.16,-.39,.80),(side*.205,-.385,.979),(side*.255,-.39,1.10),(side*.32,-.42,1.113)],.020,ridge,"Head")
        sphere("Bark antenna tip",(side*.319,-.42,1.11),(.030,.027,.028),wood,"Head",12,8)
        for j,y in enumerate((-.24,.13,.44)):
            bone=("Arm." if j==0 else "Leg.")+('L' if side<0 else 'R')
            a=(side*.34,y,.34); b=(side*.56,y-.046,.20); c=(side*.64,y-.10,.054)
            taper("Bark leg upper",a,b,(.093,.083),wood,bone,16)
            sphere("Bark leg joint",b,(.087,.081,.083),ridge,bone,14,8)
            taper("Bark short foot",b,c,(.078,.066),wood,bone,16)
            sphere("Bark soft toe",c,(.076,.093,.048),ridge,bone,14,8)
    tube("Bark quiet smile",[(-.12,-.641,.424),(0,-.671,.406),(.12,-.641,.424)],.012,ridge,"Head")
    leaf=mat("Bark_NibbledLeaf",linear("9DA85A"))
    patch("Bark lunch leaf",[(.023,-.696,.435),(.18,-.739,.48),(.327,-.70,.42),(.205,-.733,.366),(.081,-.727,.394)],.013,leaf,"Head",.012)
    tube("Bark leaf vein",[(.03,-.716,.432),(.17,-.752,.426),(.30,-.72,.42)],.006,moss,"Head")
    return ns

def create_rig(key):
    p=PROPORTIONS.get(key,dict(height=1,torso=1,limbs=1))
    h,w,l=p["height"],p["torso"],p["limbs"]
    bones=[("Root",(0,0,.03),(0,0,.18),None),
           ("Hips",(0,0,.48),(0,0,.83),"Root"),
           ("Torso",(0,0,.83),(0,0,1.31),"Hips"),
           ("Head",(0,0,1.31),(0,0,1.80),"Torso"),
           ("Arm.L",(-.32,0,1.22),(-.94,0,1.22),"Torso"),
           ("Arm.R",(.32,0,1.22),(.94,0,1.22),"Torso"),
           ("Leg.L",(-.17,0,.67),(-.17,0,.15),"Hips"),
           ("Leg.R",(.17,0,.67),(.17,0,.15),"Hips")]
    if key=="bark":
        bones=[("Root",(0,0,.03),(0,0,.18),None),
               ("Hips",(0,.12,.26),(0,.12,.40),"Root"),
               ("Torso",(0,.12,.40),(0,.12,.65),"Hips"),
               ("Head",(0,-.31,.47),(0,-.31,.67),"Torso"),
               ("Arm.L",(-.34,-.24,.34),(-.64,-.24,.34),"Torso"),
               ("Arm.R",(.34,-.24,.34),(.64,-.24,.34),"Torso"),
               ("Leg.L",(-.34,.27,.34),(-.34,.27,.06),"Hips"),
               ("Leg.R",(.34,.27,.34),(.34,.27,.06),"Hips")]
    data=bpy.data.armatures.new("GoblinFriend_Rig")
    arm=bpy.data.objects.new("GoblinFriend_Rig",data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active=arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name,head,tail,parent in bones:
        b=data.edit_bones.new(name)
        factor=l if name.startswith("Arm") else (1.32 if key=="bum" else w) if name.startswith("Leg") else 1
        b.head=(head[0]*factor,head[1],head[2]*h)
        b.tail=(tail[0]*factor,tail[1],tail[2]*h)
        if parent:b.parent=data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.select_set(False)
    return arm

def palette_atlas(mesh,key):
    """One submesh, a padded 256px palette atlas and tactile normal swatches."""
    materials=list(mesh.data.materials)
    n=8; tile=32; size=n*tile
    colour=bpy.data.images.new(key+"_Palette",width=size,height=size,alpha=False)
    normal=bpy.data.images.new(key+"_Normal",width=size,height=size,alpha=False)
    normal.colorspace_settings.name="Non-Color"
    rgb_pixels=[0.]*(size*size*4); normal_pixels=[0.]*(size*size*4)
    for y in range(size):
        for x in range(size):
            index=(y//tile)*n+x//tile
            mat=materials[min(index,len(materials)-1)]
            rgb=mat.diffuse_color[:3]
            cloth=any(word in mat.name for word in ("Shirt","Scarf","Cloth","Repair"))
            wood=mat.name.startswith("Bark_") and any(word in mat.name for word in ("Shell","Cracked"))
            grain=(.92+.07*math.sin(x*.50+math.sin(y*.22)*2.1)) if wood else .975+.025*math.sin(x*1.67+y*2.29)
            offset=(y*size+x)*4
            rgb_pixels[offset:offset+4]=[srgb(rgb[0]*grain),srgb(rgb[1]*grain),srgb(rgb[2]*grain),1]
            amp=.08 if cloth else .035 if wood else .02
            normal_pixels[offset:offset+4]=[.5+amp*math.sin(x*(.5 if wood else math.pi/2)),.5+amp*math.sin(y*(.22 if wood else math.pi/2)),1,1]
    colour.pixels.foreach_set(rgb_pixels); normal.pixels.foreach_set(normal_pixels)
    colour.filepath_raw=str(MODELS/(key+"_Palette.png")); colour.file_format="PNG"; colour.save()
    normal.filepath_raw=str(MODELS/(key+"_Normal.png")); normal.file_format="PNG"; normal.save()
    uv=mesh.data.uv_layers.active or mesh.data.uv_layers.new(name="PaletteUV")
    for polygon in mesh.data.polygons:
        index=polygon.material_index
        tx,ty=index%n,index//n
        # Stay well inside each colour island to prevent mip bleeding on field.
        for loop_index in polygon.loop_indices:
            v=mesh.data.vertices[mesh.data.loops[loop_index].vertex_index].co
            u=(v.x*3.7+v.y*2.1)%1; vcoord=(v.z*3.1+v.y*1.4)%1
            uv.data[loop_index].uv=((tx+.25+.5*u)/n,(ty+.25+.5*vcoord)/n)
        polygon.material_index=0
    mat=bpy.data.materials.new(key+"_Atlas")
    mat.diffuse_color=(1,1,1,1);mat.use_nodes=True
    nodes=mat.node_tree.nodes; bsdf=nodes.get("Principled BSDF")
    tex=nodes.new("ShaderNodeTexImage");tex.image=colour
    mat.node_tree.links.new(tex.outputs["Color"],bsdf.inputs["Base Color"])
    norm=nodes.new("ShaderNodeTexImage");norm.image=normal
    bump=nodes.new("ShaderNodeNormalMap");bump.inputs["Strength"].default_value=.12 if key=="bark" else .3
    mat.node_tree.links.new(norm.outputs["Color"],bump.inputs["Color"])
    mat.node_tree.links.new(bump.outputs["Normal"],bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value=.68
    mesh.data.materials.clear();mesh.data.materials.append(mat)
    return mat

def bind(ns,key):
    arm=create_rig(key)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in ns["parts"]:
        obj.vertex_groups.new(name=obj["rig_bone"]).add(list(range(len(obj.data.vertices))),1,"REPLACE")
        obj.select_set(True)
    bpy.context.view_layer.objects.active=ns["parts"][0]
    bpy.ops.object.join()
    mesh=bpy.context.object;mesh.name=key+"_LOD0";mesh.parent=arm
    bm=bmesh.new();bm.from_mesh(mesh.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh.data);bm.free()
    mod=mesh.modifiers.new("Skin to common rig","ARMATURE");mod.object=arm
    palette_atlas(mesh,key)
    return arm,mesh

def pose(arm,bone,angle):
    pb=arm.pose.bones[bone];pb.rotation_mode="QUATERNION"
    axis=arm.data.bones[bone].matrix_local.to_3x3().inverted() @ Vector((0,1,0))
    pb.rotation_quaternion=Quaternion(axis,angle)

def animate(arm,key):
    action=bpy.data.actions.new("GF_Idle" if key!="bark" else "Bark_Idle")
    arm.animation_data_create();arm.animation_data.action=action
    for frame,sway in ((1,0),(16,1),(31,0),(46,-1),(61,0)):
        bpy.context.scene.frame_set(frame)
        values=(("Arm.L",-.42+sway*.025),("Arm.R",.42+sway*.025),("Head",sway*.026),("Torso",sway*.012))
        if key=="bark":values=(("Head",sway*.027),("Torso",sway*.012),("Arm.L",sway*.035),("Arm.R",sway*.035))
        for bone,angle in values:
            pose(arm,bone,angle)
            arm.pose.bones[bone].keyframe_insert(data_path="rotation_quaternion",frame=frame,group=bone)
    scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=61;scene.render.fps=30;scene.frame_set(1)

def studio(key,height):
    scene=bpy.context.scene
    world=scene.world;world.use_nodes=True
    world.node_tree.nodes["Background"].inputs["Color"].default_value=(.55,.57,.52,1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value=.5
    for name,position,power,size in (("Warm key",(-3,-4,5),550,4),("Soft fill",(3,-2,3),240,4),("Rim",(1.5,3,4),360,3)):
        light=bpy.data.lights.new(name,"AREA");light.energy=power;light.shape="DISK";light.size=size
        obj=bpy.data.objects.new(name,light);bpy.context.collection.objects.link(obj);obj.location=position
        obj.rotation_euler=(Vector((0,0,height*.5))-obj.location).to_track_quat("-Z","Y").to_euler()
    data=bpy.data.cameras.new("Character turnaround camera");camera=bpy.data.objects.new("Character turnaround camera",data)
    bpy.context.collection.objects.link(camera);scene.camera=camera;data.type="ORTHO";data.ortho_scale=height*1.32
    scene.render.engine="CYCLES";scene.cycles.samples=24
    scene.view_settings.view_transform="Standard";scene.view_settings.look="Medium High Contrast";scene.view_settings.exposure=-.2
    scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG";scene.render.film_transparent=True
    for angle,location in (("front",(0,-7.9,height*.5)),("three-quarter",(3.6,-6.6,height*.65)),("back",(2.7,6.8,height*.6))):
        camera.location=location
        camera.rotation_euler=(Vector((0,0,height*.5))-camera.location).to_track_quat("-Z","Y").to_euler()
        scene.render.filepath=str(RENDERS/(key+"-"+angle+".png"))
        bpy.ops.render.render(write_still=True)
    return camera

def build(key):
    # Prevent suffixes (.001) on rig/action names between characters. This only
    # resets the background process, never the interactive Blender instance.
    bpy.ops.wm.read_factory_settings(use_empty=False)
    ns=bark_model() if key=="bark" else woodland_base(key)
    if key!="bark":accessories(ns,key)
    arm,mesh=bind(ns,key)
    mesh.data.calc_loop_triangles()
    triangles=len(mesh.data.loop_triangles)
    assert triangles < 40000,(key,triangles)
    animate(arm,key)
    depsgraph=bpy.context.evaluated_depsgraph_get()
    evaluated=mesh.evaluated_get(depsgraph)
    posed=evaluated.to_mesh()
    posed_points=[evaluated.matrix_world@v.co for v in posed.vertices]
    posed_dimensions=[max(v[i] for v in posed_points)-min(v[i] for v in posed_points) for i in range(3)]
    evaluated.to_mesh_clear()
    bpy.ops.object.select_all(action="DESELECT");arm.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=arm
    fbx=MODELS/(key+".fbx")
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,add_leaf_bones=False,
        axis_forward="-Z",axis_up="Y",bake_anim=True,bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,path_mode="AUTO",apply_unit_scale=True)
    # Bind-pose dimensions are stable and useful for field scaling.
    points=[mesh.matrix_world@v.co for v in mesh.data.vertices]
    dimensions=[max(v[i] for v in points)-min(v[i] for v in points) for i in range(3)]
    studio(key,dimensions[2])
    bpy.context.preferences.filepaths.save_version=0
    if hasattr(bpy.context.preferences.filepaths,"save_preview_images"):
        bpy.context.preferences.filepaths.save_preview_images=False
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCES/(key+".blend")))
    record={"id":key,"fbx":str(fbx.relative_to(ROOT)).replace("\\","/"),
        "triangles":triangles,"vertices":len(mesh.data.vertices),"materials":len(mesh.data.materials),
        "bones":[b.name for b in arm.data.bones],"bind_dimensions_xyz":dimensions,"idle_dimensions_xyz":posed_dimensions,
        "source_height_relative":PROPORTIONS.get(key,{}).get("height"),
        "suggested_field_scale_relative_to_tish":(.76 if key=="bum" else 1),
        "rig_object":"GoblinFriend_Rig","animation":"Bark_Idle" if key=="bark" else "GF_Idle",
        "fbx_sha256":hashlib.sha256(fbx.read_bytes()).hexdigest()}
    print("DEMO_CHARACTER_RESULT "+json.dumps(record))
    return record

args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
parser=argparse.ArgumentParser();parser.add_argument("characters",nargs="*",metavar="CHARACTER")
keys=parser.parse_args(args).characters or ["tish","luma","bum","ryzh","bark"]
unknown=set(keys)-{"tish","luma","bum","ryzh","bark"}
if unknown:parser.error("Unknown character: "+", ".join(sorted(unknown)))
records=[]
manifest_path=ART/"models_manifest.json"
if manifest_path.exists() and len(keys)<5:
    records=[r for r in json.loads(manifest_path.read_text(encoding="utf-8"))["characters"] if r["id"] not in keys]
for key in keys:
    records.append(build(key))
manifest_path.write_text(json.dumps({"generator":"build_demo_characters.py",
    "source_sha256":hashlib.sha256(SOURCE.read_bytes()).hexdigest(),"blender":bpy.app.version_string,
    "characters":records},indent=2),encoding="utf-8")
