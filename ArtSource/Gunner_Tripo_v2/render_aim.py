# render the placed rifle on the character in a given pose. args: WORK_BLEND POSE_FBX OUT_PREFIX
import bpy,sys,os,math
from mathutils import Vector
A=sys.argv[sys.argv.index("--")+1:]; BL,POSE,OUT=[os.path.abspath(a) for a in A[:3]]
bpy.ops.wm.open_mainfile(filepath=BL)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
a0=set(bpy.data.actions); n1=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=POSE)
act=[a for a in bpy.data.actions if a not in a0][0]
for o in [o for o in bpy.data.objects if o not in n1]: bpy.data.objects.remove(o)
arm.animation_data_create(); arm.animation_data.action=act
if hasattr(act,"slots") and len(act.slots): arm.animation_data.action_slot=act.slots[0]
bpy.context.scene.frame_set(int(act.frame_range[0])+int(len(sys.argv)>0 and 0)); bpy.context.view_layer.update()
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'
sh=sc.display.shading; sh.light='FLAT'; sh.color_type='TEXTURE'
sc.render.resolution_x=800; sc.render.resolution_y=800
sc.world=bpy.data.worlds.new("w"); sc.world.color=(0.12,0.13,0.15)
cam=bpy.data.objects.new("c",bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=2.0
def shot(name,loc,rot,scale=2.0):
    cam.data.ortho_scale=scale; cam.location=loc; cam.rotation_euler=[math.radians(r) for r in rot]
    sc.render.filepath=OUT+name; bpy.ops.render.render(write_still=True)
c=Vector((0,0,0.9))
shot("_front.png",(0,-4,0.7),(90,0,0),1.3); shot("_side.png",(4,0,0.7),(90,0,90),1.3); shot("_top.png",(0,0,5),(0,0,0),1.0)
