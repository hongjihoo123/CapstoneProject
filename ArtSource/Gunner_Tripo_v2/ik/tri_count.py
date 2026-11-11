import bpy, sys, glob, os
A=sys.argv[sys.argv.index("--")+1:]
for f in sorted(glob.glob(A[0]+"/*.fbx")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f)
    t=0; v=0
    for o in bpy.data.objects:
        if o.type=='MESH':
            o.data.calc_loop_triangles(); t+=len(o.data.loop_triangles); v+=len(o.data.vertices)
    print("TRI", os.path.basename(f), t, v)
