# Imports the sleeve FBX and marks the new jacket bones so their placement can be checked.
#   blender -b -P show_sleeve_bones.py -- FBX OUT_PREFIX   -> renders PNGs (workbench, x-ray body, red = new bones, blue = Mixamo shoulder/arm/forearm)
#   blender -P show_sleeve_bones.py -- FBX                 -> opens the GUI with the same markers
import bpy, sys, math
from mathutils import Vector

A = sys.argv[sys.argv.index("--") + 1:]
FBX = A[0]
OUT = A[1] if len(A) > 1 else None
if OUT:
    bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
arm = bpy.data.objects["Armature"]
body = bpy.data.objects["GunnerV2_Body"]

def marker(name, pos, color, size):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=size, location=pos, segments=16, ring_count=8)
    o = bpy.context.active_object
    o.name = "MK_" + name
    m = bpy.data.materials.new("MK_" + name)
    m.diffuse_color = color
    o.data.materials.append(m)
    o.show_in_front = True
    return o

def line(name, a, b, color, r=0.004):
    mid = (a + b) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=(b - a).length, location=mid, vertices=8)
    o = bpy.context.active_object
    o.name = "MK_" + name
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = (b - a).to_track_quat('Z', 'Y')
    m = bpy.data.materials.new("MK_" + name)
    m.diffuse_color = color
    o.data.materials.append(m)
    o.show_in_front = True

for b in arm.data.bones:
    h, t = arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local
    if b.name.startswith("JacketR"):
        marker(b.name + "_head", h, (1, 0, 0, 1), 0.012)
        line(b.name, h, t, (1, 0, 0, 1), 0.006)
        print("NEWBONE", b.name, "head", tuple(round(x, 3) for x in h), "tail", tuple(round(x, 3) for x in t), "parent", b.parent.name)
    elif b.name in ("mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand"):
        marker(b.name + "_head", h, (0.1, 0.3, 1, 1), 0.008)
        line(b.name, h, t, (0.1, 0.3, 1, 1), 0.003)
        print("OLDBONE", b.name, "head", tuple(round(x, 3) for x in h), "tail", tuple(round(x, 3) for x in t))

# jacket vertices that took weight from the new bones (so you can see which cloth they drive)
sh = body.vertex_groups.get("JacketR_Shoulder"); sl = body.vertex_groups.get("JacketR_Sleeve")
pts = {"sh": [], "sl": []}
for v in body.data.vertices:
    for g in v.groups:
        if g.weight > 0.3:
            if sh and g.group == sh.index: pts["sh"].append(v.co.copy())
            if sl and g.group == sl.index: pts["sl"].append(v.co.copy())
for k, vs in pts.items():
    if vs:
        c = sum(vs, Vector()) / len(vs)
        print("VERTS", k, len(vs), "centre", tuple(round(x, 3) for x in c))

if not OUT:
    for a in bpy.context.screen.areas:
        if a.type == 'VIEW_3D':
            a.spaces[0].shading.show_xray = True
            a.spaces[0].shading.xray_alpha = 0.35
    sys.exit(0) if False else None
else:
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.show_xray = True
    sc.display.shading.xray_alpha = 0.3
    sc.render.resolution_x = sc.render.resolution_y = 900
    sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.12, 0.13, 0.16)
    focus = Vector((-0.12, 0.03, 0.8))
    cam = bpy.data.cameras.new("c"); cam.type = 'ORTHO'; cam.ortho_scale = 0.55
    co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
    for name, d in (("front", Vector((0, -1, 0))), ("back", Vector((0, 1, 0))), ("side", Vector((-1, 0, 0))), ("top", Vector((0, 0, 1)))):
        co.location = focus + d * 2
        co.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler() if name != "top" else (0, 0, 0)
        sc.render.filepath = f"{OUT}_{name}.png"
        bpy.ops.render.render(write_still=True)
    print("RENDERED", OUT)
