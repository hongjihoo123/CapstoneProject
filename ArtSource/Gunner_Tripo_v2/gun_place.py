# Place a gun mesh so that, in the rifle AimIdle pose, its grip sits in the right hand and its barrel runs
# toward the left hand. The placement is stored relative to mixamorig:RightHand and the model is exported in rest pose.
# args: TPOSE_FBX AIMIDLE_FBX OUT_FBX  GUN_SOURCE  [json]
#   GUN_SOURCE = KM fbx (gun = faces with material M_Gun)  or  any mesh file (glb/fbx/obj) holding only the gun
import bpy, bmesh, sys, os, json, math, numpy as np
from mathutils import Vector, Matrix
A=sys.argv[sys.argv.index("--")+1:]; TPOSE,AIM,OUT,GUN=A[0],A[1],A[2],A[3]; P=json.loads(A[4]) if len(A)>4 else {}
bpy.ops.wm.read_factory_settings(use_empty=True)
def frame_from(origin,x,up):
    x=Vector(x).normalized(); z=(Vector(up)-Vector(up).dot(x)*x).normalized(); y=z.cross(x)
    M=Matrix((x,y,z)).transposed().to_4x4(); M.translation=Vector(origin); return M
# ---- 1. gun mesh in its own frame (x = barrel toward muzzle, z = up, origin = grip) ----
n0=set(bpy.data.objects)
if GUN.lower().endswith(".glb") or GUN.lower().endswith(".gltf"): bpy.ops.import_scene.gltf(filepath=GUN)
elif GUN.lower().endswith(".obj"): bpy.ops.wm.obj_import(filepath=GUN)
else: bpy.ops.import_scene.fbx(filepath=GUN)
new=[o for o in bpy.data.objects if o not in n0]
meshes=[o for o in new if o.type=='MESH']
km=any(m.name.startswith("M_Gun") for o in meshes for m in o.data.materials if m)
bm=bmesh.new()
for o in meshes:
    t=bmesh.new(); t.from_mesh(o.data); t.transform(o.matrix_world)
    if km:
        gi=[i for i,m in enumerate(o.data.materials) if m and m.name.startswith("M_Gun")]
        bmesh.ops.delete(t,geom=[f for f in t.faces if f.material_index not in gi],context='FACES')
    me_tmp=bpy.data.meshes.new("tmp"); t.to_mesh(me_tmp); t.free(); bm.from_mesh(me_tmp)
gm=bpy.data.meshes.new("Gunner_Gun"); bm.to_mesh(gm); bm.free()
mats=[m for o in meshes for m in o.data.materials if m and (not km or m.name.startswith("M_Gun"))]
gm.materials.clear()
for m in mats[:1]: gm.materials.append(m)
co=np.array([v.co[:] for v in gm.vertices]); c=co.mean(0)
_,_,vt=np.linalg.svd(co-c); ax=Vector(vt[0])
# barrel direction / grip: KM -> muzzle far from the KM right hand; generic -> P["muzzle"] sign along the long axis
if km:
    arm_km=[o for o in new if o.type=='ARMATURE'][0]; grip=arm_km.matrix_world@arm_km.data.bones["Bone.007"].head_local
    # barrel = the side that reaches farther from the grip (the stock is the short side)
    ext=(co-np.array(grip))@np.array(ax)
    if -ext.min()>ext.max(): ax=-ax
    if P.get("flip"): ax=-ax
else:
    ax=ax*P.get("muzzle",1)
    proj=(co-c)@np.array(ax); low=co[:,2]<np.percentile(co[:,2],P.get("grip_pct",25))
    # grip = lowest part near the back third (pistol grip), unless given explicitly
    g=co[low&(proj<np.percentile(proj,55))]; grip=Vector(g.mean(0)) if len(g) else Vector(c)
up=Vector(P.get("gun_up",[0,0,1]))
G_src=frame_from(grip,ax,up)
loc_co=[G_src.inverted()@Vector(v) for v in co]
L=(co-c)@np.array(ax); length=L.max()-L.min()
for o in new: bpy.data.objects.remove(o)
# ---- 2. character in the AimIdle pose ----
n0=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=TPOSE)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE' and o not in n0][0]
if P.get("style"):   # feedback pass: clearer colours + bigger goggles (gunner_style.py); P["style"]=[TEX_IN, TEX_OUT]
    sys.path.insert(0,os.path.dirname(os.path.abspath(__file__))); import gunner_style
    gunner_style.apply([o for o in bpy.data.objects if o.type=='MESH' and o not in n0][0],P["style"][0],P["style"][1],P.get("goggle_scale",1.5))
a0=set(bpy.data.actions); n1=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=AIM)
act=[a for a in bpy.data.actions if a not in a0][0]
for o in [o for o in bpy.data.objects if o not in n1]: bpy.data.objects.remove(o)
arm.animation_data_create(); arm.animation_data.action=act
if hasattr(act,"slots") and len(act.slots): arm.animation_data.action_slot=act.slots[0]
bpy.context.scene.frame_set(int(act.frame_range[0])); bpy.context.view_layer.update()
W=arm.matrix_world; pb=arm.pose.bones
rh=W@pb["mixamorig:RightHand"].head; lh=W@pb["mixamorig:LeftHand"].head
mid=W@pb["mixamorig:RightHandMiddle1"].head
barrel=(lh-rh).normalized()
palm=rh+(mid-rh)*P.get("palm",0.6)          # grip sits in the palm, between wrist and knuckles
# keep the receiver/sight clear of the face: the hands sit on the face line in the aim pose, so slide the gun to the right and down (metres)
side=barrel.cross(Vector((0,0,1))).normalized()
shift=side*P.get("off_right",0)+Vector((0,0,-P.get("off_down",0)))
G_pose=frame_from(palm+Vector(P.get("off",[0,0,0]))+shift,barrel,Vector((0,0,1)))
Hp=W@pb["mixamorig:RightHand"].matrix; lp,rp,_=Hp.decompose(); Hp=Matrix.LocRotScale(lp,rp,Vector((1,1,1)))
local=Hp.inverted()@G_pose
# size: gun length as a multiple of the hand-to-hand gap
gap=(lh-rh).length; s=P.get("length_over_gap",2.6)*gap/length if length>0 else 1.0
print("hand gap %.3f gun length %.3f -> scale %.3f"%(gap,length,s))
# ---- 3. back to rest pose, bake the gun under the hand ----
arm.animation_data.action=None; arm.data.pose_position='REST'; bpy.context.view_layer.update()
for p in pb: p.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
Hr=W@arm.data.bones["mixamorig:RightHand"].matrix_local; lr,rr,_=Hr.decompose(); Hr=Matrix.LocRotScale(lr,rr,Vector((1,1,1)))
G_rest=Hr@local
for v,l in zip(gm.vertices,loc_co): v.co=G_rest@(l*s)
gun=bpy.data.objects.new("Gunner_Gun",gm); bpy.context.scene.collection.objects.link(gun)
mz=max(loc_co,key=lambda l:l.x)
emp=bpy.data.objects.new("AimOrigin",None); emp.empty_display_size=0.03; bpy.context.scene.collection.objects.link(emp)
emp.location=G_rest@Vector((mz.x*s,0,0))
for o in (gun,emp):
    mw=o.matrix_world.copy(); o.parent=arm; o.parent_type='BONE'; o.parent_bone="mixamorig:RightHand"
    bpy.context.view_layer.update(); o.matrix_world=mw
bpy.context.view_layer.update()
arm.data.pose_position='POSE'
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
body=[o for o in bpy.data.objects if o.type=='MESH' and o!=gun][0]; body.name="GunnerV2_Body"
for o in bpy.context.view_layer.objects: o.select_set(o.type in('ARMATURE','MESH','EMPTY'))
bpy.ops.export_scene.fbx(filepath=OUT,use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',path_mode='STRIP')
bpy.ops.wm.save_as_mainfile(filepath=OUT.replace(".fbx","_work.blend") if P.get("keep") else os.path.join(os.environ.get("TEMP","."),"gun_place.blend"))
print("exported",OUT)
