import bpy, sys
A=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=A[0])
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; W=arm.matrix_world
bpy.context.view_layer.update()
for n in ["Hips","Spine","Spine1","Spine2","Neck","Head","RightShoulder","RightArm","RightForeArm","RightHand","RightHandIndex1","RightHandIndex2","RightHandIndex3","RightHandIndex4","LeftArm","LeftForeArm","LeftHand","RightUpLeg","RightFoot"]:
    for b in arm.pose.bones:
        if b.name.endswith(":"+n) or b.name==n:
            p=W@b.head; print("%-18s %.3f %.3f %.3f"%(n,p.x,p.y,p.z)); break
    else: print(n,"MISSING")
print("arm scale",arm.scale[:],"world scale",W.to_scale()[:])
