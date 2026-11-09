import bpy,os,numpy as np
from mathutils import Vector
M="C:/Users/hongj/CapstoneProject/Assets/Members/HJH/05.Data/Character/GunnerV2"
bpy.ops.wm.open_mainfile(filepath=os.path.abspath("rifle/GunnerV2_work.blend") if os.path.exists("rifle/GunnerV2_work.blend") else os.path.abspath("rifle/GunnerV2_new_work.blend"))
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
a0=set(bpy.data.actions); n1=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=M+"/Custom/GunnerV2_C_AimIdle.fbx")
act=[a for a in bpy.data.actions if a not in a0][0]
for o in [o for o in bpy.data.objects if o not in n1]: bpy.data.objects.remove(o)
arm.animation_data_create(); arm.animation_data.action=act
if hasattr(act,"slots") and len(act.slots): arm.animation_data.action_slot=act.slots[0]
bpy.context.scene.frame_set(int(act.frame_range[0])); bpy.context.view_layer.update()
gun=bpy.data.objects["Gunner_Gun"]; body=[o for o in bpy.data.objects if o.type=='MESH' and o.name!="Gunner_Gun"][0]
dg=bpy.context.evaluated_depsgraph_get()
head=arm.matrix_world@arm.pose.bones["mixamorig:Head"].head; neck=arm.matrix_world@arm.pose.bones["mixamorig:Neck"].head
rh=arm.matrix_world@arm.pose.bones["mixamorig:RightHand"].head
print("HEAD",tuple(head),"NECK",tuple(neck),"RH",tuple(rh))
be=body.evaluated_get(dg); bv=np.array([(be.matrix_world@v.co)[:] for v in be.data.vertices])
hv=bv[(bv[:,2]>head.z-0.02)]; c=np.array(head[:])+np.array([0,0,0.05]); cc=hv.mean(0); r=np.percentile(np.linalg.norm(hv-cc,axis=1),90)
print("head centre",cc,"r",r,"n",len(hv))
ge=gun.evaluated_get(dg); gv=np.array([(ge.matrix_world@v.co)[:] for v in ge.data.vertices])
palm=np.array((gun.matrix_world.translation)[:]) # object origin ~ at hand; we scale about right hand head
rhp=np.array(rh[:])
for k in (1.0,0.85,0.75,0.65):
    p=rhp+(gv-rhp)*k
    # distance from head-vertex cloud: min distance gun vertex to head verts
    from numpy.linalg import norm
    d=np.min(norm(p[:,None,:]-hv[None,::20,:],axis=2),axis=1)
    print("k",k,"min dist to head verts %.4f"%d.min(),"gun verts inside head sphere",int((norm(p-cc,axis=1)<r*0.8).sum()))
print("----grid")
lh=arm.matrix_world@arm.pose.bones["mixamorig:LeftHand"].head
f=np.array((lh-rh).normalized()[:]); up=np.array([0,0,1.0]); rt=np.cross(f,up); rt/=np.linalg.norm(rt); up2=np.cross(rt,f)
rel=cc-rhp; print("head centre in hand frame fwd/right/up",rel@f,rel@rt,rel@up2)
gl=(gv-rhp); print("gun extent fwd",(gl@f).min(),(gl@f).max(),"right",(gl@rt).min(),(gl@rt).max(),"up",(gl@up2).min(),(gl@up2).max())
from numpy.linalg import norm
sub=hv[::6]
best=[]
for k in (1.0,0.9,0.8):
  for r_ in (0,0.01,0.02,0.03,0.04):
    for u_ in (0,-0.01,-0.02,-0.03):
      for f_ in (0,0.02,0.04,0.06):
        p=rhp+(gl*k)+rt*r_+up2*u_+f*f_
        # penetration proxy: gun verts within 0.015 of head verts
        dm=np.min(norm(p[::4,None,:]-sub[None],axis=2),axis=1)
        best.append(((dm<0.012).sum(),k,r_,u_,f_))
best.sort(); print(best[:12])
print([b for b in best if b[1]==1.0 and b[2]==0 and b[3]==0 and b[4]==0])
print("----grid2")
best=[]
for k in (0.8,0.7,0.6):
  for r_ in (0.03,0.05,0.07):
    for u_ in (0,-0.02,-0.04):
      for f_ in (0.04,0.08,0.12):
        p=rhp+(gl*k)+rt*r_+up2*u_+f*f_
        dm=np.min(norm(p[::4,None,:]-sub[None],axis=2),axis=1)
        best.append(((dm<0.012).sum(),k,r_,u_,f_))
best.sort(); print("G2",best[:10])
