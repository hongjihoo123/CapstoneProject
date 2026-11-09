# Hand-authored short clips for Gunner v2 (one-hand pistol, body facing forward), timed to the skill data.
# Blender world: character faces -Y, up +Z, character's right = -X.
import bpy, sys, os, json, math
from mathutils import Vector, Matrix, Quaternion
A=sys.argv[sys.argv.index("--")+1:]; MIX,OUT=A[0],A[1]; P=json.loads(A[2]) if len(A)>2 else {}
FPS=30
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene; sc.render.fps=FPS
bpy.ops.import_scene.fbx(filepath=os.path.join(MIX,"GunnerV2_TPose.fbx"))
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
W=arm.matrix_world
def load_action(name):
    before=set(bpy.data.actions); n0=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MIX,f"GunnerV2_{name}.fbx"))
    act=[a for a in bpy.data.actions if a not in before][0]
    for o in [o for o in bpy.data.objects if o not in n0]: bpy.data.objects.remove(o)
    return act
def use(act):
    arm.animation_data_create(); arm.animation_data.action=act
    if hasattr(act,"slots") and len(act.slots): arm.animation_data.action_slot=act.slots[0]
SRC={k:load_action(k) for k in ("IdleBreath","RunStd_F","RifleAimIdle","RifleFire2","RifleRunAim","RifleIdleLow","RifleRunDown","FIdle","FRun_F")}
PB=arm.pose.bones
def B(n): return PB["mixamorig:"+n]
_cache={}
def snapshot(act,frame):
    k=(act.name,int(frame))
    if k not in _cache:
        prev=arm.animation_data.action if arm.animation_data else None
        prev_slot=arm.animation_data.action_slot if prev is not None and hasattr(arm.animation_data,"action_slot") else None
        use(act); sc.frame_set(int(frame)); bpy.context.view_layer.update()
        _cache[k]={pb.name:(pb.location.copy(),pb.rotation_quaternion.copy()) for pb in PB}
        if prev is not None:
            arm.animation_data.action=prev
            if prev_slot is not None: arm.animation_data.action_slot=prev_slot
    return _cache[k]
def apply(snap):
    for pb in PB:
        l,q=snap[pb.name]; pb.location=l; pb.rotation_quaternion=q
    bpy.context.view_layer.update()
# --- pose helpers (world space) ---
def point(name,d,roll=0.0):
    """aim bone `name` along world direction d, then roll it about its own axis (deg)"""
    pb=B(name); bpy.context.view_layer.update()
    Mw=W@pb.matrix; loc=Mw.translation; R=Mw.to_3x3().normalized()
    y=(R@Vector((0,1,0))).normalized(); d=Vector(d).normalized()
    q=y.rotation_difference(d)
    if roll: q=Quaternion(d,math.radians(roll))@q
    newR=(q.to_matrix()@R)
    pb.matrix=W.inverted()@(Matrix.Translation(loc)@newR.to_4x4()@Matrix.Diagonal(Mw.to_scale().to_4d()))
    bpy.context.view_layer.update()
def turn(name,axis,deg):
    """rotate bone `name` about a WORLD axis by deg (keeps children following)"""
    pb=B(name); bpy.context.view_layer.update()
    Mw=W@pb.matrix; loc=Mw.translation
    q=Quaternion(Vector(axis).normalized(),math.radians(deg))
    pb.matrix=W.inverted()@(Matrix.Translation(loc)@(q.to_matrix()@Mw.to_3x3().normalized()).to_4x4()@Matrix.Diagonal(Mw.to_scale().to_4d()))
    bpy.context.view_layer.update()
def curl(side,deg):
    for f in ("Thumb","Index","Middle","Ring","Pinky"):
        for i in (1,2,3):
            n=f"{side}Hand{f}{i}"
            if "mixamorig:"+n not in PB: continue
            amt=deg*(0.15 if f=="Index" else (0.5 if f=="Thumb" else 1.0))
            pb=B(n); pb.rotation_quaternion=pb.rotation_quaternion@Quaternion((1,0,0),math.radians(amt*P.get("curlsign",1)))
    bpy.context.view_layer.update()
FWD=Vector((0,-1,0)); UP=Vector((0,0,1)); RIGHT=Vector((-1,0,0)); LEFT=Vector((1,0,0))
