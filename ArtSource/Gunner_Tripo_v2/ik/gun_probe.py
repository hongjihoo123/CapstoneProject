import bpy, sys, numpy as np
from mathutils import Vector
A=sys.argv[sys.argv.index("--")+1:]; FBX=A[0]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
gun=[o for o in bpy.data.objects if o.type=='MESH' and 'Gun' in o.name and 'Body' not in o.name][0]
print("GUN",gun.name,"parent",gun.parent.name if gun.parent else None,gun.parent_type,gun.parent_bone,"vgroups",[v.name for v in gun.vertex_groups][:6])
bpy.context.view_layer.update()
def B(n):
    for b in arm.pose.bones:
        if b.name.endswith(n): return b
rh=B("RightHand"); lh=B("LeftHand")
W=arm.matrix_world
rhp=W@rh.head; lhp=W@lh.head
co=np.array([(gun.matrix_world@v.co)[:] for v in gun.data.vertices])
print("rightHand",rhp[:],"leftHand",lhp[:])
c=co.mean(0); _,_,vt=np.linalg.svd(co-c); ax=Vector(vt[0])
# barrel axis toward away from right hand
if (Vector(co.max(0))-Vector(co.min(0))).length>0:
    ext=(co-np.array(rhp))@np.array(ax)
    if -ext.min()>ext.max(): ax=-ax
print("barrel axis",ax[:]); 
ext=(co-np.array(rhp))@np.array(ax); print("along-barrel from rightHand: min %.3f max %.3f"%(ext.min(),ext.max()))
up=Vector((0,0,1)); up=(up-up.dot(ax)*ax).normalized(); side=ax.cross(up)
rel=co-np.array(rhp)
u=rel@np.array(up); s=rel@np.array(side)
print("up range %.3f..%.3f  side range %.3f..%.3f"%(u.min(),u.max(),s.min(),s.max()))
# left hand in gun frame
d=Vector(lhp)-Vector(rhp); print("leftHand in gun frame (along,up,side): %.3f %.3f %.3f"%(d.dot(ax),d.dot(up),d.dot(side)))
# profile: for each 4cm slice along barrel, lowest 'up' extent -> reveals pistol grip / foregrip / mag
bins=np.arange(ext.min(),ext.max()+0.04,0.04)
for i in range(len(bins)-1):
    m=(ext>=bins[i])&(ext<bins[i+1])
    if m.sum(): print("along %.2f..%.2f  up %.3f..%.3f  side %.3f..%.3f"%(bins[i],bins[i+1],u[m].min(),u[m].max(),s[m].min(),s[m].max()))
# render side view ortho
import math
bpy.ops.object.camera_add(); cam=bpy.context.object; bpy.context.scene.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=1.4
ctr=Vector(c.tolist())
cam.location=ctr+side*2.0; 
direction=ctr-cam.location; cam.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()
bpy.ops.object.light_add(type='SUN'); bpy.context.object.rotation_euler=(0.6,0.2,0.4)
for o in bpy.data.objects:
    if o.type=='MESH' and o!=gun: o.hide_render=True
# marks
def mark(p,col,n):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.012,location=p); o=bpy.context.object; o.name=n
    m=bpy.data.materials.new(n); m.diffuse_color=col; o.data.materials.append(m)
mark(rhp,(1,0,0,1),"RH"); mark(lhp,(0,1,0,1),"LH")
sc=bpy.context.scene; sc.render.resolution_x=1400; sc.render.resolution_y=700; sc.render.engine='BLENDER_WORKBENCH'
sc.render.filepath=A[1]; bpy.ops.render.render(write_still=True)
