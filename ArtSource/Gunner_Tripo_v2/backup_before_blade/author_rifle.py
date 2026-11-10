# Rifle (two-hand) clips for Gunner v2: legs/hips forward, upper body = Mixamo rifle mocap twisted so the gun points straight ahead.
import os, sys
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)),"rifle_base.py"),encoding="utf-8").read())
import math
LOWER=("Hips","LeftUpLeg","LeftLeg","LeftFoot","LeftToeBase","LeftToe_End","RightUpLeg","RightLeg","RightFoot","RightToeBase","RightToe_End")
def is_lower(n): return n.replace("mixamorig:","") in LOWER
def yaw(v): return math.degrees(math.atan2(v.x,-v.y))
def gun_dir():
    bpy.context.view_layer.update()
    return (W@B("LeftHand").head)-(W@B("RightHand").head)
def combine(legs,upper,aim=True):
    for pb in PB:
        l,q=(legs if is_lower(pb.name) else upper)[pb.name]; pb.location=l; pb.rotation_quaternion=q
    bpy.context.view_layer.update()
    if not aim: return    # carry poses (rifle held low across the body) keep the mocap upper body as is
    # twist the spine so the rifle (right hand -> left hand) points straight ahead, head turned back toward the front
    for _ in range(3):
        g=gun_dir(); err=yaw(Vector((g.x,g.y,0)).normalized())
        if abs(err)<0.5: break
        for n,w in (("Spine",0.4),("Spine1",0.3),("Spine2",0.3)): turn(n,UP,-err*w)   # +deg about +Z turns toward +X (left); err>0 = gun points left -> turn right
    # level the gun pitch a little (mocap aims slightly down)
    g=gun_dir(); pitch=math.degrees(math.atan2(g.z,math.hypot(g.x,g.y)))
    turn("Spine2",LEFT,(pitch-P.get("pitch",-3))*0.9)
    g=gun_dir(); _dbg.append((round(yaw(Vector((g.x,g.y,0)).normalized()),1),round(math.degrees(math.atan2(g.z,math.hypot(g.x,g.y))),1)))
    # face forward-ish
    h=W@B("Head").matrix; f=(h.to_3x3()@Vector((0,0,1))); hy=yaw(Vector((f.x,f.y,0)).normalized())
    turn("Neck",UP,-hy*0.35); turn("Head",UP,-hy*0.35)
_dbg=[]
def kick(back=0.0,lean=0.0):
    """+back = recoil (torso tips back, both hands stay on the rifle); +lean = torso forward"""
    a=lean-back
    if a: turn("Spine",LEFT,a*0.4); turn("Spine1",LEFT,a*0.3); turn("Spine2",LEFT,a*0.3)
def key(frame):
    for pb in PB:
        pb.keyframe_insert("rotation_quaternion",frame=frame)
        if pb.name=="mixamorig:Hips": pb.keyframe_insert("location",frame=frame)
def new_clip(name):
    act=bpy.data.actions.new("GunnerV2_"+name); act.use_fake_user=True
    arm.animation_data_create(); arm.animation_data.action=None   # drop the old slot so keys get a fresh "OBArmature" slot
    arm.animation_data.action=act
    return act
def S(src,f): return snapshot(SRC[src],f)
# pre-sample everything (no action switching while keying)
for f in range(1,95,1): S("RifleAimIdle",f)
for f in range(1,10): S("RifleFire2",f)
for f in range(1,24): S("RifleRunAim",f)
for f in (1,20): S("IdleBreath",f)
for f in range(1,23): S("RunStd_F",f)
for f in (1,60,120): S("RifleIdleLow",f)
for f in range(1,22): S("RifleRunDown",f)
# pre-sample every source frame BEFORE any clip is created (sampling while keying returns a frozen pose)
for _a in ("FIdle","FRun_F","RifleRunDown"):
    _f0,_f1=[int(x) for x in SRC[_a].frame_range]
    for _f in range(_f0,_f1+1): S(_a,_f)
clips={}
# CarryIdle 2.7 s loop: rifle held low across the body, feminine standing legs
FI0,FI1=[int(x) for x in SRC["FIdle"].frame_range]
for f in (FI0,FI0+40): S("FIdle",f)
act=new_clip("CarryIdle")
for fr,rf,lf in ((0,1,FI0),(40,60,FI0+40),(80,1,FI0)):
    combine(S("FIdle",lf),S("RifleIdleLow",rf),aim=False)
    if fr==40: turn("Spine1",LEFT,-2.0); turn("Spine2",LEFT,-1.5); turn("Spine2",UP,2.0); turn("Head",LEFT,1.5)   # breath in + small weight shift
    key(fr)
clips["CarryIdle"]=act
# CarryRun: female run legs; upper body = low-carry pose with ~35% of the rifle-run upper motion mixed back in
# (shoulder swing / torso counter-twist / gun bob) + a per-stride chest bounce, head kept steady
FR0,FR1=[int(x) for x in SRC["FRun_F"].frame_range]
RD0,RD1=[int(x) for x in SRC["RifleRunDown"].frame_range]
n=FR1-FR0
for f in range(RD0,RD1+1): S("RifleRunDown",f)
def mix(a,b,t,skip=()):
    out={}
    for k in a:
        if k in skip: out[k]=a[k]; continue
        la,qa=a[k]; lb,qb=b[k]
        out[k]=(la.lerp(lb,t),qa.slerp(qb,t))
    return out
act=new_clip("CarryRun")
LIFE=P.get("life",0.38)
for i,f in enumerate(range(FR0,FR1+1)):
    rf=RD0+int(round((RD1-RD0)*i/max(1,n)))            # phase-match the rifle run to the leg cycle
    up=mix(S("RifleIdleLow",1),S("RifleRunDown",rf),LIFE,skip=("mixamorig:Head","mixamorig:Neck"))
    combine(S("FRun_F",f),up,aim=False)
    ph=2*math.pi*i/max(1,n)                            # two strides per loop -> 2x frequency
    turn("Spine1",LEFT,2.5*math.cos(2*ph))             # chest dips on each footfall
    turn("Spine2",UP,3.0*math.sin(ph))                 # shoulders counter the hips
    turn("Head",UP,-2.0*math.sin(ph)); turn("Head",LEFT,-1.5*math.cos(2*ph))   # keep the gaze steady
    key(i)
clips["CarryRun"]=act
# AimIdle 1.5 s loop
act=new_clip("AimIdle")
for fr,rf,lf in ((0,1,1),(22,45,20),(45,1,1)):
    combine(S("IdleBreath",lf),S("RifleAimIdle",rf)); key(fr)
clips["AimIdle"]=act
# AimRun: rifle-run upper body (bake every frame, 0.73 s loop), forward-run legs
act=new_clip("AimRun")
for i in range(0,23):
    combine(S("RunStd_F",min(22,i+1)),S("RifleRunAim",min(23,i+1))); key(i)
clips["AimRun"]=act
# Attack: Mixamo single rifle shot (0.27 s), Anim_Fire at frame 1
act=new_clip("Attack")
for i in range(0,9):
    combine(S("IdleBreath",1),S("RifleFire2",min(9,i+1))); key(i)
clips["Attack"]=act
# Skills: exaggerated so they read from the top-down camera
# PassiveShot 0.47 s: lean in 0.12 s, heavy kick at frame 4 (Anim_Fire), recover
act=new_clip("PassiveShot")
for fr,back,lean in ((0,0,0),(3,0,14),(4,26,0),(8,10,0),(14,0,0)):
    combine(S("IdleBreath",1),S("RifleAimIdle",1)); kick(back,lean); key(fr)
clips["PassiveShot"]=act
# Dash 0.23 s: sprint lunge, rifle carried low, torso way forward
act=new_clip("Dash")
for fr,rf,lean in ((0,4,18),(3,6,38),(7,8,24)):
    combine(S("RunStd_F",rf),S("RifleRunDown",8),aim=False); kick(0,lean); key(fr)
clips["Dash"]=act
# WeaponSkill = dash blast 0.37 s: low lunge 0.15 s, snap the rifle up, huge cone-blast kick, recover
act=new_clip("WeaponSkill")
for fr,rf,aimed,back,lean in ((0,4,False,0,20),(4,6,False,0,38),(5,None,True,0,10),(6,None,True,34,0),(9,None,True,18,0),(11,None,True,6,0)):
    combine(S("RunStd_F",rf) if rf else S("IdleBreath",1),S("RifleAimIdle",1) if aimed else S("RifleRunDown",8),aim=aimed); kick(back,lean); key(fr)
clips["WeaponSkill"]=act
# Ultimate = shock cannon 0.47 s: wind up (lean in hard) 0.3 s, massive kick at frame 10, stagger back
act=new_clip("Ultimate")
for fr,back,lean in ((0,0,0),(4,0,22),(9,0,30),(10,42,0),(12,30,0),(14,20,0)):
    combine(S("IdleBreath",1),S("RifleAimIdle",1)); kick(back,lean); key(fr)
clips["Ultimate"]=act
for name,act in clips.items(): print("CLIP",name,act.frame_range[:])
print("GUNDIR yaw,pitch samples",_dbg[:6],"...",_dbg[-4:])
os.makedirs(OUT,exist_ok=True)
# export WITH the skinned body so the file's bind pose stays the T-pose (armature-only export bakes frame 0 as rest)
for o in bpy.context.view_layer.objects: o.select_set(o==arm or (o.type=='MESH' and o.parent==arm))
bpy.context.view_layer.objects.active=arm
for name,act in clips.items():
    arm.animation_data.action=None; arm.animation_data.action=act
    if len(act.slots): arm.animation_data.action_slot=act.slots[0]
    sc.frame_start=int(act.frame_range[0]); sc.frame_end=int(act.frame_range[1])
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,f"GunnerV2_C_{name}.fbx"),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0.0,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL')
bpy.ops.wm.save_as_mainfile(filepath=A[3])
# verify: re-import each exported clip and make sure the right arm actually moves away from rest
import subprocess
for name in clips:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT,f"GunnerV2_C_{name}.fbx"))
    a=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; bpy.context.scene.frame_set(1)
    q=a.pose.bones["mixamorig:RightArm"].rotation_quaternion
    print("VERIFY",name,"rightarm w=%.3f"%q.w, "OK" if q.w<0.98 else "REST-POSE!")
print("done")
