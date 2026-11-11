# Stone_Big_* / Stone_Mid_* FBX -> 저폴리 LOD 메시 (_L1 = 35%, _L2 = 12%). Unity 의 Stones/Models 폴더에서 읽고 같은 폴더에 씀.
# 실행: blender -b -P lod_gen.py -- <Models 폴더 절대경로> [접두사,접두사...]
import bpy, sys, glob, os
A = sys.argv[sys.argv.index("--") + 1:]
MODELS = A[0]
PREFIXES = tuple((A[1] if len(A) > 1 else "Stone_Big,Stone_Mid").split(","))
LEVELS = [("L1", 0.35), ("L2", 0.12)]

def tris(objs):
    t = 0
    for o in objs:
        if o.type == 'MESH':
            o.data.calc_loop_triangles(); t += len(o.data.loop_triangles)
    return t

for f in sorted(glob.glob(os.path.join(MODELS, "Stone_*.fbx"))):
    base = os.path.basename(f)
    if base.endswith("_L1.fbx") or base.endswith("_L2.fbx") or not base.startswith(PREFIXES):
        continue
    for tag, ratio in LEVELS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=f)
        meshes = [o for o in bpy.data.objects if o.type == 'MESH']
        before = tris(meshes)
        # 크기 비교용 (원본과 같은 크기로 나와야 함)
        import mathutils
        mn = mathutils.Vector((1e9,) * 3); mx = mathutils.Vector((-1e9,) * 3)
        for o in meshes:
            for c in o.bound_box:
                w = o.matrix_world @ mathutils.Vector(c)
                mn = mathutils.Vector((min(mn[i], w[i]) for i in range(3))); mx = mathutils.Vector((max(mx[i], w[i]) for i in range(3)))
        for o in meshes:
            bpy.context.view_layer.objects.active = o
            o.select_set(True)
            m = o.modifiers.new("dec", 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = ratio; m.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=m.name)
            bpy.ops.object.shade_smooth()
        after = tris(meshes)
        out = f[:-4] + "_%s.fbx" % tag
        bpy.ops.export_scene.fbx(filepath=out, use_selection=False, apply_scale_options='FBX_SCALE_ALL',
                                 bake_anim=False, mesh_smooth_type='OFF', add_leaf_bones=False)
        print("LOD", base, tag, before, "->", after, "size", tuple(round(mx[i] - mn[i], 3) for i in range(3)))
