"""Reproducible Blender source kit. Run inside Blender; exports game FBX assets."""
import bpy, math, random, os
from mathutils import Vector
ROOT = 'C:/Backforge/Diceforge/DiceForge'
OUT = ROOT + '/Assets/_Project/07_Art/WoodlandDiorama'
os.makedirs(OUT, exist_ok=True)
random.seed(72)
scene = bpy.data.scenes.new('Woodland_SourceKit')
bpy.context.window.scene = scene
palette = [('Soil',(0.24,.115,.048)),('Earth',(.40,.23,.10)),('Moss',(.34,.40,.105)),('Pine',(.12,.26,.125)),('PineTip',(.25,.38,.15)),('Stone',(.76,.68,.46)),('StoneLight',(.92,.84,.63)),('Rock',(.37,.37,.29)),('Bark',(.31,.14,.055)),('Wood',(.57,.32,.13)),('Skin',(.39,.60,.12)),('SkinLight',(.58,.73,.22)),('Ear',(.69,.36,.22)),('Red',(.65,.16,.075)),('Blue',(.09,.43,.47)),('Boot',(.17,.105,.055)),('Ivory',(.99,.91,.68)),('Eye',(.035,.055,.028)),('Gold',(.96,.63,.15)),('Water',(.12,.47,.46)),('Flower',(.88,.67,.20)),('Mushroom',(.72,.13,.055)),('Leaf',(.49,.52,.15)),('Foam',(.64,.86,.72))]
mats = []
for name,c in palette:
    m = bpy.data.materials.new('WD_'+name); m.diffuse_color=(*c,1); m.use_nodes=True
    m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*c,1)
    m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.78
    mats.append(m)
def mat(name): return mats[[p[0] for p in palette].index(name)]
parts=[]
def finish(o,name,loc,scale,material,bone=None):
    o.name=name; o.location=loc; o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat(material)); parts.append(o)
    if bone: o['bone']=bone
    for p in o.data.polygons: p.use_smooth=True
    return o
def ball(name,loc,scale,material,bone=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10)
    return finish(bpy.context.object,name,loc,scale,material,bone)
def box(name,loc,scale,material,bevel=.06,bone=None):
    bpy.ops.mesh.primitive_cube_add(size=2)
    o=finish(bpy.context.object,name,loc,scale,material,bone)
    mod=o.modifiers.new('Hand softened edges','BEVEL'); mod.width=bevel; mod.segments=3
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=mod.name)
    return o
def cone(name,loc,r1,r2,depth,material,verts=12):
    bpy.ops.mesh.primitive_cone_add(vertices=verts,radius1=r1,radius2=r2,depth=depth)
    return finish(bpy.context.object,name,loc,(1,1,1),material)
def rod(name,a,b,r,material,bone=None):
    a,b=Vector(a),Vector(b)
    o=cone(name,(a+b)/2,r,r*.85,(b-a).length,material)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    if bone:o['bone']=bone
    return o
roots=[]
def export(name,rig=False):
    global parts
    bpy.ops.object.select_all(action='DESELECT')
    if rig:
        arm=bpy.data.armatures.new(name+'_Rig'); root=bpy.data.objects.new(name,arm); scene.collection.objects.link(root)
        bpy.context.view_layer.objects.active=root; root.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
        bones=[('Root',(0,0,.08),(0,0,.38),None),('Body',(0,0,.38),(0,0,.80),'Root'),('Head',(0,0,.80),(0,0,1.3),'Body'),('ArmL',(-.21,0,.75),(-.43,0,.49),'Body'),('ArmR',(.21,0,.75),(.43,0,.49),'Body')]
        for bn,head,tail,parent in bones:
            b=arm.edit_bones.new(bn); b.head=head;b.tail=tail
            if parent:b.parent=arm.edit_bones[parent]
        bpy.ops.object.mode_set(mode='OBJECT');root.select_set(False)
        for o in parts:
            g=o.vertex_groups.new(name=o.get('bone','Root'));g.add(list(range(len(o.data.vertices))),1,'REPLACE')
        for o in parts:o.select_set(True)
        bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();mesh=bpy.context.object;mesh.name='Goblin_LOD0'
        mesh.parent=root; mod=mesh.modifiers.new('Rig','ARMATURE');mod.object=root
        parts=[mesh]
        lod=mesh.copy();lod.data=mesh.data.copy();scene.collection.objects.link(lod);lod.name='Goblin_LOD1'
        dec=lod.modifiers.new('Mobile detail','DECIMATE');dec.ratio=.46
        bpy.context.view_layer.objects.active=lod
        bpy.ops.object.modifier_apply(modifier=dec.name);lod.parent=root;parts.append(lod)
        root.animation_data_create()
        for clip,length,amount in [('Idle',80,.025),('IdleReact',64,.06),('Selected',22,.09),('Anticipate',14,-.08),('Walk',24,.11),('Land',14,-.055),('Hit',24,-.15),('Return',32,.09),('Victory',55,.13)]:
            action=bpy.data.actions.new(clip);root.animation_data.action=action
            for f in [1,length//2,length]:
                body=root.pose.bones['Body'];head=root.pose.bones['Head'];body.location=(0,0,amount if f==length//2 else 0)
                body.keyframe_insert('location',frame=f)
                head.rotation_mode='XYZ';head.rotation_euler=(0, .12 if f==length//2 else 0, .06 if f==length//2 else 0);head.keyframe_insert('rotation_euler',frame=f)
                for bn,side in [('ArmL',-1),('ArmR',1)]:
                    b=root.pose.bones[bn];b.rotation_mode='XYZ';b.rotation_euler=(0,side*(.7 if clip=='Victory' else .12) if f==length//2 else 0,0);b.keyframe_insert('rotation_euler',frame=f)
            track=root.animation_data.nla_tracks.new();track.name=clip;track.strips.new(clip,1,action);track.mute=True
        root.animation_data.action=None
        for pb in root.pose.bones:pb.location=(0,0,0);pb.rotation_euler=(0,0,0)
    else:
        for o in parts:o.select_set(True)
        bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();root=bpy.context.object;root.name=name
        scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');parts=[root]
    bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
    for o in parts:o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=rig,bake_anim_use_all_actions=rig,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='AUTO')
    roots.append(root)
    for o in parts:o.hide_set(True)
    root.hide_set(True);parts=[]
for i in range(4):
    box('Carved sandstone',(0,0,.105),(.49,.45,.105),'StoneLight' if i%2 else 'Stone',.075)
    for k in range(3):
        ball('Edge weathering',(random.uniform(-.4,.4),random.uniform(-.36,.36),.21),(.032,.021,.005),'Stone')
    export('Tile_'+str(i))
for i in range(3):
    h=1.65+i*.27;cone('Trunk',(0,0,.45),.11,.08,.9,'Bark')
    for j in range(4):
        z=.60+j*h*.19;r=(.68-j*.135)*(1+i*.08)
        cone('Evergreen crown',(0,0,z),r,.035,h*.55,'Pine' if j%2==0 else 'PineTip',10)
        for k in range(5):
            a=k*math.tau/5+j*.5
            ball('Soft bough',(math.cos(a)*r*.53,math.sin(a)*r*.53,z-.12),(r*.39,r*.29,.22),'PineTip' if j%2 else 'Pine')
    export('Pine_'+str(i))
for i in range(5):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2)
    o=finish(bpy.context.object,'Weathered granite',(0,0,.25),(.35+i*.035,.28,.32+i*.025),'Rock')
    for v in o.data.vertices:v.co*=random.uniform(.89,1.11)
    ball('Moss cushion',(.06,0,.48),(.23,.2,.06),'Moss');export('Rock_'+str(i))
for i in range(6):
    for j in range(5):
        x,y=random.uniform(-.21,.21),random.uniform(-.19,.19)
        if i<3:
            o=ball('Leaf',(x,y,.11),(.045,.09,.17),'Leaf' if j%2 else 'Moss');o.rotation_euler=(random.uniform(-.5,.5),random.uniform(-.5,.5),j)
        elif i==3:
            rod('Stem',(x,y,0),(x,y,.21),.012,'Moss')
            for k in range(5):
                a=k*math.tau/5;ball('Petal',(x+.045*math.cos(a),y+.045*math.sin(a),.24),(.04,.03,.015),'Ivory')
            ball('Pollen',(x,y,.25),(.022,.022,.02),'Flower')
        elif i==4:
            rod('Stalk',(x,y,0),(x,y,.16),.025,'Ivory');ball('Cap',(x,y,.18),(.09,.09,.055),'Mushroom')
            ball('Spot',(x+.025,y,.225),(.018,.018,.009),'Ivory')
        else:ball('Bush',(x,y,.18),(.14,.12,.15),'PineTip')
    export('Foliage_'+str(i))
for i in range(7):box('Plank',(0,(i-3)*.17,.15),(.50,.075,.07),'Wood',.025)
for x in [-.43,.43]:rod('Stringer',(x,-.63,.035),(x,.63,.035),.065,'Bark')
export('Bridge')
for x in [-.45,.45]:box('Post',(x,0,.35),(.065,.065,.35),'Bark')
for z in [.23,.49]:box('Rail',(0,0,z),(.52,.035,.055),'Wood',.025)
export('Fence')
box('Crate',(0,0,.28),(.28,.25,.28),'Bark')
for x in [-.22,0,.22]:box('Board',(x,-.26,.28),(.095,.025,.25),'Wood',.015)
for z in [.07,.47]:box('Brace',(0,-.29,z),(.29,.026,.035),'Stone',.015)
export('Crate')
cone('Stump',(0,0,.20),.28,.22,.4,'Bark');cone('Cut wood',(0,0,.407),.215,.215,.018,'Wood');export('Stump')
box('Post',(0,0,.42),(.045,.05,.42),'Bark');box('Sign',(0,-.035,.69),(.35,.06,.19),'Wood');export('Sign')
rod('Flagpole',(0,0,0),(0,0,1.2),.035,'Wood');ball('Finial',(0,0,1.22),(.06,.06,.065),'Gold')
box('Flag',(.23,0,.98),(.23,.015,.14),'Red',.025);export('Flag')
for team in ['Red','Blue']:
    cone('Ivory base',(0,0,.045),.32,.32,.09,'StoneLight',32)
    for x in [-.13,.13]:
        ball('Boot',(x,-.07,.15),(.105,.17,.11),'Boot');rod('Leg',(x,0,.18),(x,0,.41),.07,'Skin')
    ball('Tunic',(0,0,.51),(.24,.18,.29),team,'Body')
    box('Belt',(0,-.155,.43),(.225,.032,.037),'Boot',.02,'Body');box('Buckle',(0,-.197,.43),(.052,.018,.047),'Gold',.015,'Body')
    for side,bn in [(-1,'ArmL'),(1,'ArmR')]:
        ball('Sleeve',(side*.25,0,.67),(.12,.13,.14),team,bn);rod('Forearm',(side*.29,0,.62),(side*.37,-.06,.47),.069,'Skin',bn)
        ball('Hand',(side*.38,-.07,.46),(.088,.08,.09),'SkinLight',bn)
    ball('Head',(0,-.025,.99),(.30,.22,.285),'Skin','Head')
    ball('Muzzle',(0,-.19,.88),(.22,.11,.105),'SkinLight','Head')
    ball('Smile',(0,-.276,.876),(.14,.015,.033),'Eye','Head')
    for side in [-1,1]:
        o=ball('Ear',(side*.34,0,1.05),(.23,.055,.12),'Skin','Head');o.rotation_euler[1]=-side*.32
        o=ball('Inner ear',(side*.35,-.047,1.055),(.16,.017,.072),'Ear','Head');o.rotation_euler[1]=-side*.32
        ball('Cheek',(side*.20,-.175,.955),(.09,.055,.07),'SkinLight','Head')
        ball('Eye white',(side*.112,-.211,1.065),(.088,.035,.095),'Ivory','Head')
        ball('Pupil',(side*.10,-.242,1.055),(.033,.016,.047),'Eye','Head');ball('Eye glint',(side*.10-.009,-.256,1.075),(.010,.008,.014),'Ivory','Head')
        brow=ball('Brow',(side*.115,-.205,1.16),(.106,.043,.035),'Skin','Head');brow.rotation_euler[1]=side*.12
        box('Tooth',(side*.082,-.29,.883),(.027,.014,.027),'Ivory',.008,'Head')
    ball('Nose',(0,-.264,1.006),(.073,.067,.060),'SkinLight','Head')
    box('Scarf',(0,-.14,.755),(.19,.075,.055),team,.035,'Body')
    if team=='Blue':
        ball('Cap',(0,.025,1.20),(.28,.21,.09),team,'Head')
        o=ball('Cap tail',(.18,.02,1.28),(.20,.13,.08),team,'Head');o.rotation_euler[1]=.3
    else:
        for j in range(3):
            o=ball('Hair',((j-1)*.085,0,1.26),(.053,.1,.11),'Moss','Head');o.rotation_euler[1]=-.3
    export('Goblin_'+team,True)
for i,r in enumerate(roots):
    r.hide_set(False);r.location.x=(i%7)*3;r.location.y=(i//7)*3
    for child in r.children:child.hide_set(False)
scene.render.engine='CYCLES'
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/docs/Art/WoodlandDiorama/Woodland_SourceKit.blend')
print('Exported',len(roots),'assets to',OUT)
