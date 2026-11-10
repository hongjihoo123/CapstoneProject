# Clean + reduce the Tripo smart-mesh rifle. args: IN_FBX OUT_DIR [target_tris]
import bpy, bmesh, sys, os, math
from mathutils import Vector
A=sys.argv[sys.argv.index("--")+1:]; IN,OUT=A[0],A[1]; TARGET=int(A[2]) if len(A)>2 else 5000
os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=IN)
ob=[o for o in bpy.data.objects if o.type=='MESH'][0]; ob.name="Rifle"
bpy.context.view_layer.objects.active=ob; ob.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
def tris(o): return sum(len(p.vertices)-2 for p in o.data.polygons)
before=tris(ob)
# remove tiny floating islands (<1.5% of faces)
bm=bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
seen=set(); islands=[]
for f in bm.faces:
    if f.index in seen: continue
    stack=[f]; isl=[]; seen.add(f.index)
    while stack:
        c=stack.pop(); isl.append(c)
        for e in c.edges:
            for n in e.link_faces:
                if n.index not in seen: seen.add(n.index); stack.append(n)
    islands.append(isl)
tot=len(bm.faces); junk=[f for isl in islands if len(isl)<tot*0.015 for f in isl]
print("islands",len(islands),"junk faces",len(junk))
if junk: bmesh.ops.delete(bm,geom=junk,context='FACES')
bm.to_mesh(ob.data); bm.free()
# triangulate then collapse to target (UV-preserving)
tm=ob.modifiers.new("tri","TRIANGULATE"); bpy.ops.object.modifier_apply(modifier=tm.name)
cur=tris(ob)
if cur>TARGET:
    d=ob.modifiers.new("dec","DECIMATE"); d.ratio=TARGET/cur; d.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=d.name)
bpy.ops.object.shade_smooth()
print("TRIS before",before,"after",tris(ob),"dims",tuple(ob.dimensions))
# normalise: centre, keep orientation
bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY',center='BOUNDS')
ob.location=(0,0,0); bpy.ops.object.transform_apply(location=True)
# save texture
for im in bpy.data.images:
    if im.name.startswith("tripo_rgb"):
        im.scale(2048,2048); im.filepath_raw=os.path.join(OUT,"Rifle_BaseColor.png"); im.file_format='PNG'; im.save()
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,"rifle_clean.glb"),export_format='GLB')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,"rifle_clean.blend"))
