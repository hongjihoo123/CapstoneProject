import bpy, sys, os, math
from mathutils import Vector
A=sys.argv[sys.argv.index("--")+1:]; BLEND,OUT=os.path.abspath(A[0]),os.path.abspath(A[1])
bpy.ops.wm.open_mainfile(filepath=BLEND)
for im in bpy.data.images:
    if im.name.startswith("tripo_rgb"):
        im.scale(2048,2048); im.save_render(os.path.join(OUT,"Rifle_BaseColor.png")); print("tex saved")
ob=bpy.data.objects["Rifle"]
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'
sh=sc.display.shading; sh.light='STUDIO'; sh.color_type='TEXTURE'; sh.show_cavity=False
sc.render.resolution_x=900; sc.render.resolution_y=500
sc.world=bpy.data.worlds.new("w"); sc.world.color=(0.18,0.19,0.2)
cam=bpy.data.objects.new("c",bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=1.2
def shot(name,loc,rot):
    cam.location=loc; cam.rotation_euler=[math.radians(r) for r in rot]
    sc.render.filepath=os.path.join(OUT,name); bpy.ops.render.render(write_still=True)
shot("rifle_side.png",(3,0,0),(90,0,90))      # looking along -X : sees Y-long axis side
shot("rifle_top.png",(0,0,3),(0,0,90))        # top-down
shot("rifle_3q.png",(2,-2,1.5),(65,0,45))
