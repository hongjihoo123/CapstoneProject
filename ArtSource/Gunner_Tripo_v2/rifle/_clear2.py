import bpy,os,numpy as np,sys
from numpy.linalg import norm
exec(open('rifle/_clear_base.py').read().split('print("HEAD"')[0])
vg={g.name:g.index for g in body.vertex_groups}; hi=vg["mixamorig:Head"]
idx=[v.index for v in body.data.vertices if any(g.group==hi and g.weight>0.6 for g in v.groups)]
be=body.evaluated_get(dg); bv=np.array([(be.matrix_world@v.co)[:] for v in be.data.vertices]); hv=bv[idx]
ge=gun.evaluated_get(dg); gv=np.array([(ge.matrix_world@v.co)[:] for v in ge.data.vertices])
dm=np.array([norm(hv-g,axis=1).min() for g in gv[::3]])
print("RES head verts",len(hv),"gun verts within 1cm of head:",int((dm<0.01).sum()),"within 2cm:",int((dm<0.02).sum()),"min %.4f"%dm.min())
lh=arm.matrix_world@arm.pose.bones["mixamorig:LeftHand"].head; rh=arm.matrix_world@arm.pose.bones["mixamorig:RightHand"].head
f=np.array((lh-rh).normalized()[:]); rt=np.cross(f,[0,0,1.0]); rt/=norm(rt); up2=np.cross(rt,f); rhp=np.array(rh[:]); gl=gv-rhp
hs=hv[::3]; cen=hv.mean(0); print("head centre",cen,"extent",hv.min(0),hv.max(0))
def pen(p):
    # signed-ish penetration: gun verts inside the head's convex-ish hull approximated by ellipsoid from head verts
    c=cen; ax=np.percentile(np.abs(hv-c),90,axis=0)
    return int((np.sum(((p-c)/ax)**2,axis=1)<0.8).sum())
res=[]
for k in (1.0,0.9,0.8,0.7):
  for r_ in (0,0.015,0.03,0.045):
    for u_ in (0,-0.015,-0.03):
      for f_ in (0,0.03,0.06):
        p=rhp+gl*k+rt*r_+up2*u_+f*f_; res.append((pen(p),k,r_,u_,f_))
res.sort(); print("G3",res[:8]); print("BASE",[x for x in res if x[1:]==(1.0,0,0,0)])
for kk in (1.0,0.85,0.7):
    r2=[]
    for r_ in (0,0.02,0.04,0.06):
      for u_ in (0,-0.02,-0.04):
        for f_ in (0,0.03,0.06,0.09):
            p=rhp+gl*kk+rt*r_+up2*u_+f*f_; r2.append((pen(p),round(float(np.hypot(r_,u_)),3),r_,u_,f_))
    r2.sort(key=lambda x:(x[0]>0,x[1],x[0])); print("K",kk,r2[:3])
