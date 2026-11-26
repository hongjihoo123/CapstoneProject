# Adds three bones to the RIGHT front jacket flap (the coral panel hanging from shoulder to waist) so Unity can press it
# against the torso when the arm / rifle touches it.
# New bones are CHILDREN of mixamorig:Spine1 (no existing bone/path changes -> baked clips keep working; new bones get no curves).
# usage: blender -b -P add_flap_bones.py -- IN_FBX TEXTURE OUT_FBX     (IN_FBX = the ORIGINAL model without any Jacket bones)
import bpy, sys, os
from mathutils import Vector

IN, TEX, OUT = sys.argv[sys.argv.index("--") + 1:][:3]
BONE_Z = [("JacketR_Flap1", 0.790), ("JacketR_Flap2", 0.755), ("JacketR_Flap3", 0.720), ("JacketR_Flap4", 0.695)]   # model metres (jacket hem is at ~0.69)
ATTACH_Z = 0.82         # above this the flap stays glued to the shoulder/chest (no weight moved)
HEM_Z = 0.69            # flap bottom edge; vertices below Z_MIN are belt/shorts, not jacket
Z_MIN = 0.66
RAMP = 0.025            # weight ramps from 0 at ATTACH_Z to 1 within this many metres below it -> the hanging part follows the flap bones 100%
X_MAX = -0.05          # the flap strip is outside this; the chest lapels (closer to the centre line) must not follow the arm
ARM_CLEAR = 0.07        # flap = coral front panel farther than this from the shoulder->elbow line (that part is the sleeve)
MAX_INFL = 4

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=IN)
arm = bpy.data.objects["Armature"]; body = bpy.data.objects["GunnerV2_Body"]; me = body.data
if any(n.startswith("JacketR") for n in arm.data.bones.keys()):
    raise SystemExit("input already has JacketR bones - use the original model")
sh, fa = (arm.matrix_world @ arm.data.bones[n].head_local for n in ("mixamorig:RightShoulder", "mixamorig:RightForeArm"))

# ---- coral test per vertex
img = bpy.data.images.load(TEX); W, H = img.size; px = list(img.pixels); uv = me.uv_layers.active.data
vcol = {}
for p in me.polygons:
    for li in p.loop_indices:
        vi = me.loops[li].vertex_index; u, v = uv[li].uv
        i = (min(H - 1, max(0, int(v * H))) * W + min(W - 1, max(0, int(u * W)))) * 4
        vcol.setdefault(vi, (px[i], px[i + 1], px[i + 2]))

def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * t)).length

flap = []
for v in me.vertices:
    r, g, b = vcol.get(v.index, (0, 0, 0))
    pw = body.matrix_world @ v.co
    if r > 0.45 and r > g * 1.35 and r > b * 1.5 and pw.x < X_MAX and Z_MIN < pw.z < ATTACH_Z and seg_dist(pw, sh, fa) > ARM_CLEAR:
        flap.append((v, pw))
# fill texture holes (logos / dark stitching inside the coral panel): add a vertex when most of its neighbours are already in
nbr = {}
for e in me.edges:
    a, b = e.vertices
    nbr.setdefault(a, []).append(b); nbr.setdefault(b, []).append(a)
sel = {v.index for v, _ in flap}
for _ in range(4):
    add_now = []
    for v in me.vertices:
        if v.index in sel: continue
        pw = body.matrix_world @ v.co
        if not (pw.x < X_MAX and Z_MIN < pw.z < ATTACH_Z and seg_dist(pw, sh, fa) > ARM_CLEAR): continue
        ns = nbr.get(v.index, ())
        if ns and sum(1 for n in ns if n in sel) >= 0.6 * len(ns):
            add_now.append(v)
    if not add_now: break
    for v in add_now:
        sel.add(v.index); flap.append((v, body.matrix_world @ v.co))
print("FLAP after hole fill", len(flap))

# keep only the largest connected piece of the candidates (stray bits of chest/lapel that happen to pass the tests are dropped)
cand = {v.index for v, _ in flap}
adj = {}
for e in me.edges:
    a, b = e.vertices
    if a in cand and b in cand:
        adj.setdefault(a, []).append(b); adj.setdefault(b, []).append(a)
seen, best = set(), set()
for s0 in cand:
    if s0 in seen: continue
    comp, st = set(), [s0]; seen.add(s0)
    while st:
        i = st.pop(); comp.add(i)
        for j in adj.get(i, ()):
            if j not in seen: seen.add(j); st.append(j)
    if len(comp) > len(best): best = comp
print("FLAP candidates", len(cand), "largest piece", len(best))
flap = [(v, pw) for v, pw in flap if v.index in best]
print("FLAP verts", len(flap))

# ---- bones: head at the centroid of the flap vertices in its height band
heads = []
for name, z in BONE_Z:
    band = [pw for _, pw in flap if abs(pw.z - z) < 0.05] or [pw for _, pw in flap]
    c = sum(band, Vector()) / len(band)
    heads.append(Vector((c.x, c.y, z)))
    print("FLAP bone", name, tuple(round(x, 3) for x in heads[-1]), "band verts", len(band))

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
saved = {b.name: (b.head.copy(), b.tail.copy(), b.roll) for b in eb}
inv = arm.matrix_world.inverted()
for (name, _), h in zip(BONE_Z, heads):
    b = eb.new(name)
    b.head, b.tail = inv @ h, inv @ (h + Vector((0, 0, -0.04)))
    b.parent = eb["mixamorig:Spine1"]; b.use_connect = False
for n, (h, t, ro) in saved.items():
    eb[n].head, eb[n].tail, eb[n].roll = h, t, ro
bpy.ops.object.mode_set(mode='OBJECT')

# ---- weights: moved share ramps 0 (at ATTACH_Z) -> 1 (hem); split between the two nearest bones by height
low_z = HEM_Z
groups = [body.vertex_groups.new(name=n) for n, _ in BONE_Z]
names = {i: g.name for i, g in enumerate(body.vertex_groups)}
zs = [z for _, z in BONE_Z]
def smooth(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
moved = 0
for v, pw in flap:
    f = smooth((ATTACH_Z - pw.z) / RAMP)
    w = {names[x.group]: x.weight for x in v.groups if not names[x.group].startswith("JacketR")}
    total = sum(w.values())
    if f < 0.02 or total < 1e-6:
        continue
    for n in list(w):
        body.vertex_groups[n].add([v.index], w[n] * (1 - f), 'REPLACE'); w[n] *= (1 - f)
    # interpolate between bones by z
    if pw.z >= zs[0]: share = {0: 1.0}
    elif pw.z <= zs[-1]: share = {len(zs) - 1: 1.0}
    else:
        for i in range(len(zs) - 1):
            if zs[i] >= pw.z >= zs[i + 1]:
                t = (zs[i] - pw.z) / (zs[i] - zs[i + 1]); share = {i: 1 - t, i + 1: t}; break
    for i, s in share.items():
        groups[i].add([v.index], total * f * s, 'REPLACE'); w[groups[i].name] = total * f * s
    if len(w) > MAX_INFL:
        for n, _ in sorted(w.items(), key=lambda kv: kv[1])[:len(w) - MAX_INFL]:
            body.vertex_groups[n].remove([v.index]); del w[n]
        s = sum(w.values())
        for n, x in w.items(): body.vertex_groups[n].add([v.index], x / s, 'REPLACE')
    moved += 1
print("FLAP verts reweighted", moved)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'}, add_leaf_bones=False, bake_anim=False,
                         mesh_smooth_type='FACE', axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', path_mode='STRIP')
print("FLAP exported", OUT)
