# Adds two jacket bones on the RIGHT shoulder/upper-arm so Unity can push the sleeve away from the rifle stock at runtime.
# New bones are CHILDREN of existing Mixamo bones (no existing bone/path changes -> baked clips keep working; new bones get no curves).
# Jacket vertices (coral texture) near the shoulder->elbow line hand part of their weight over to the new bones.
# usage: blender -b -P add_sleeve_bones.py -- IN_FBX TEXTURE OUT_FBX
import bpy, sys, os
from mathutils import Vector

IN, TEX, OUT = sys.argv[sys.argv.index("--") + 1:][:3]
R_OUTER, R_INNER = 0.085, 0.045      # falloff: full weight transfer inside R_INNER, none beyond R_OUTER (model metres)
MAX_INFL = 4

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=IN)
arm = bpy.data.objects["Armature"]
body = bpy.data.objects["GunnerV2_Body"]
me = body.data
bones = arm.data.bones
sh, ua, fa = (arm.matrix_world @ bones[n].head_local for n in ("mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm"))

# ---- bones
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
def add(name, parent, head, tail):
    b = eb.new(name)
    b.head, b.tail = arm.matrix_world.inverted() @ head, arm.matrix_world.inverted() @ tail
    b.parent = eb[parent]
    b.use_connect = False
    return b
saved = {b.name: (b.head.copy(), b.tail.copy(), b.roll) for b in eb}   # adding children can move parent tails -> keep every existing bone exactly as it was
mid = (ua + fa) * 0.5
add("JacketR_Shoulder", "mixamorig:RightShoulder", sh, sh + Vector((0, 0, 0.03)))
add("JacketR_Sleeve", "mixamorig:RightArm", mid, mid + Vector((0, 0, 0.03)))
for n, (h, t, ro) in saved.items():
    eb[n].head, eb[n].tail, eb[n].roll = h, t, ro
bpy.ops.object.mode_set(mode='OBJECT')

# ---- coral (jacket) test per vertex from the texture
img = bpy.data.images.load(TEX)
W, H = img.size
px = list(img.pixels)
uv = me.uv_layers.active.data
vcol = {}
for p in me.polygons:
    for li in p.loop_indices:
        vi = me.loops[li].vertex_index
        u, v = uv[li].uv
        i = (min(H - 1, max(0, int(v * H))) * W + min(W - 1, max(0, int(u * W)))) * 4
        vcol.setdefault(vi, (px[i], px[i + 1], px[i + 2]))

def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * t)).length, t

vg_sh = body.vertex_groups.new(name="JacketR_Shoulder")
vg_sl = body.vertex_groups.new(name="JacketR_Sleeve")
names = {i: g.name for i, g in enumerate(body.vertex_groups)}
SRC = {"mixamorig:RightShoulder", "mixamorig:RightArm"}
moved = 0
for v in me.vertices:
    r, g, b = vcol.get(v.index, (0, 0, 0))
    if not (r > 0.45 and r > g * 1.35 and r > b * 1.5):
        continue
    pw = body.matrix_world @ v.co
    d, t = seg_dist(pw, sh, fa)
    if d >= R_OUTER or pw.x > -0.02:
        continue
    f = 1.0 if d <= R_INNER else 1.0 - (d - R_INNER) / (R_OUTER - R_INNER)
    f = f * f * (3 - 2 * f)
    w = {names[x.group]: x.weight for x in v.groups}
    take = sum(w.get(n, 0.0) for n in SRC) * f
    if take < 0.01:
        continue
    for n in SRC:
        if n in w:
            body.vertex_groups[n].add([v.index], w[n] * (1 - f), 'REPLACE')
            w[n] *= (1 - f)
    target = vg_sh if t < 0.5 else vg_sl
    target.add([v.index], take, 'REPLACE')
    w[target.name] = take
    # keep at most MAX_INFL influences (Unity default), renormalised
    if len(w) > MAX_INFL:
        for n, _ in sorted(w.items(), key=lambda kv: kv[1])[:len(w) - MAX_INFL]:
            body.vertex_groups[n].remove([v.index]); del w[n]
        s = sum(w.values())
        for n, x in w.items():
            body.vertex_groups[n].add([v.index], x / s, 'REPLACE')
    moved += 1
print("SLEEVE verts moved", moved, "shoulder", sum(1 for v in me.vertices if any(names.get(x.group) == "JacketR_Shoulder" for x in v.groups)),
      "sleeve", sum(1 for v in me.vertices if any(names.get(x.group) == "JacketR_Sleeve" for x in v.groups)))

# ---- export (same settings as gun_place.py)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='FACE', axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', path_mode='STRIP')
print("SLEEVE exported", OUT)
