# Bake clips onto the exact rig of Model/GunnerV2.fbx so Unity can play them as Generic (no humanoid retarget).
# Per bone: world rotation delta from rest is copied from the source rig; bone offsets come from the model's rest;
# hips translation = source hips offset from rest, scaled by hip-height ratio.
# args: MODEL_FBX OUT_DIR name=path [name=path ...]
import bpy, sys, os, math
from mathutils import Matrix, Vector, Quaternion
A=sys.argv[sys.argv.index("--")+1:]; MODEL,OUT=A[0],A[1]; JOBS=[a.split("=",1) for a in A[2:]]
os.makedirs(OUT,exist_ok=True)
def load_model():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps=30
    bpy.ops.import_scene.fbx(filepath=MODEL)
    return [o for o in bpy.data.objects if o.type=='ARMATURE'][0]
def world_rest(arm):
    return {b.name:(arm.matrix_world@b.matrix_local) for b in arm.data.bones}
for name,path in JOBS:
    # stance width kept: Dead/Hit/casts untouched, side-steps a little, everything else half
    NARROW=1.0 if (name in ("Dead","Hit") or name.startswith("Cast")) else (0.75 if name.startswith("Jog") else 0.5)
    # hip sway/twist kept: locomotion 35% (steady torso), everything else untouched
    HIPDAMP=0.7 if (name.startswith(("FRun","Jog","RunStd","C_CarryRun","C_AimRun","C_CarryIdle","FIdle"))) else 1.0
    T=load_model(); TW=T.matrix_world; TWi=TW.inverted()
    trest=world_rest(T)
    n0=set(bpy.data.objects); a0=set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=path)
    S=[o for o in bpy.data.objects if o.type=='ARMATURE' and o not in n0][0]
    sact=S.animation_data.action if S.animation_data else [a for a in bpy.data.actions if a not in a0][0]
    srest=world_rest(S)
    f0,f1=[int(round(x)) for x in sact.frame_range]
    bones=[b for b in T.data.bones]                       # parent-first order
    hipsT=trest["mixamorig:Hips"].translation; hipsS=srest["mixamorig:Hips"].translation
    hscale=hipsT.z/hipsS.z if abs(hipsS.z)>1e-6 else 1.0
    act=bpy.data.actions.new("GunnerV2_G_"+name); T.animation_data_create(); T.animation_data.action=None; T.animation_data.action=act
    for i,fr in enumerate(range(f0,f1+1)):
        bpy.context.scene.frame_set(fr)
        Pw={}                                             # target posed world matrices
        for b in bones:
            n=b.name
            Rt_rest=trest[n].to_quaternion()
            if n in S.pose.bones:
                Sw=S.matrix_world@S.pose.bones[n].matrix
                delta=Sw.to_quaternion()@srest[n].to_quaternion().inverted()
            else:
                delta=None
            if b.parent is None:
                if n=="mixamorig:Hips" and n in S.pose.bones:
                    loc=hipsT+(Sw.translation-hipsS)*hscale
                    loc.x=hipsT.x+(loc.x-hipsT.x)*HIPDAMP          # less side-to-side sway
                    if delta is not None: delta=Quaternion().slerp(delta,HIPDAMP)   # less hip twist/roll -> steady upper body
                else: loc=trest[n].translation
                rot=(delta@Rt_rest) if delta is not None else Rt_rest
            else:
                pw=Pw[b.parent.name]; off=trest[b.parent.name].inverted()@trest[n]
                loc=(pw@off).translation
                rot=(delta@Rt_rest) if delta is not None else (pw.to_quaternion()@off.to_quaternion())
            Pw[n]=Matrix.LocRotScale(loc,rot,trest[n].to_scale())
        # narrow the stance: pull knees/feet toward the hip line sideways (character's side axis = world X)
        if NARROW<0.999:
            for side in ("Left","Right"):
                chain=[f"mixamorig:{side}{p}" for p in ("UpLeg","Leg","Foot","ToeBase","Toe_End") if f"mixamorig:{side}{p}" in Pw]
                for j in range(2):                     # thigh, then shin
                    a,bn=chain[j],chain[j+1]
                    p0=Pw[a].translation; v=Pw[bn].translation-p0
                    v2=Vector((v.x*NARROW,v.y,v.z)).normalized()*v.length
                    q=v.rotation_difference(v2)
                    for c in chain[j:]:
                        M=Pw[c]; loc=p0+q@(M.translation-p0)
                        Pw[c]=Matrix.LocRotScale(loc,q@M.to_quaternion(),M.to_scale())
        for b in bones:
            pb=T.pose.bones[b.name]
            Parm=TWi@Pw[b.name]
            if b.parent is None: basis=b.matrix_local.inverted()@Parm
            else:
                Pp=TWi@Pw[b.parent.name]
                basis=(b.parent.matrix_local.inverted()@b.matrix_local).inverted()@(Pp.inverted()@Parm)
            l,q,s=basis.decompose()
            pb.rotation_mode='QUATERNION'; pb.rotation_quaternion=q
            pb.keyframe_insert("rotation_quaternion",frame=i)
            if b.parent is None: pb.location=l; pb.keyframe_insert("location",frame=i)
    # drop the source rig, export model rig + meshes + gun + AimOrigin with this action
    for o in [o for o in bpy.data.objects if o not in n0 and o!=T and o.parent!=T]: bpy.data.objects.remove(o)
    for o in [o for o in bpy.data.objects if o not in n0]:
        if o.name in bpy.data.objects: bpy.data.objects.remove(o)
    T.animation_data.action=None; T.animation_data.action=act
    if len(act.slots): T.animation_data.action_slot=act.slots[0]
    sc=bpy.context.scene; sc.frame_start=0; sc.frame_end=f1-f0
    for o in bpy.context.view_layer.objects: o.select_set(o.type in('ARMATURE','MESH','EMPTY'))
    bpy.context.view_layer.objects.active=T
    out=os.path.join(OUT,f"GunnerV2_G_{name}.fbx")
    bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0.0,mesh_smooth_type='FACE',axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',path_mode='STRIP')
    print("BAKED",name,f1-f0+1,"frames hscale %.3f"%hscale)
print("done")
