# Feedback pass for the Gunner v2 body (team review 11-09): clearer colours on dark maps + bigger goggles seen from the top-down camera.
#   apply(body, tex_in, tex_out, goggle_scale=1.5)  -> edits body mesh in place (rest pose) and writes a recoloured copy of the texture.
import bpy, bmesh, numpy as np, colorsys, os
from mathutils import Vector

def _hsv(rgb):
    r,g,b=rgb[...,0],rgb[...,1],rgb[...,2]; mx=rgb.max(-1); mn=rgb.min(-1); d=mx-mn
    h=np.zeros_like(mx); nz=d>1e-6
    rc=np.where(nz,(mx-r)/np.where(nz,d,1),0); gc=np.where(nz,(mx-g)/np.where(nz,d,1),0); bc=np.where(nz,(mx-b)/np.where(nz,d,1),0)
    h=np.where(mx==r,bc-gc,np.where(mx==g,2+rc-bc,4+gc-rc)); h=(h/6)%1.0; h=np.where(nz,h,0)
    s=np.where(mx>0,d/np.where(mx>0,mx,1),0); return np.stack([h,s,mx],-1)
def _rgb(hsv):
    h,s,v=hsv[...,0],hsv[...,1],hsv[...,2]; i=np.floor(h*6).astype(int)%6; f=h*6-np.floor(h*6)
    p=v*(1-s); q=v*(1-f*s); t=v*(1-(1-f)*s)
    r=np.choose(i,[v,q,p,p,t,v]); g=np.choose(i,[t,v,v,q,p,p]); b=np.choose(i,[p,p,t,v,v,q]); return np.stack([r,g,b],-1)

def _raster(uvtris,size):
    """mask of every texel covered by the given UV triangles (list of 3x2 arrays in 0..1)"""
    m=np.zeros((size,size),bool)
    for t in uvtris:
        p=t*size; x0=int(max(0,np.floor(p[:,0].min()))); x1=int(min(size-1,np.ceil(p[:,0].max())))
        y0=int(max(0,np.floor(p[:,1].min()))); y1=int(min(size-1,np.ceil(p[:,1].max())))
        if x1<x0 or y1<y0: continue
        xs,ys=np.meshgrid(np.arange(x0,x1+1)+.5,np.arange(y0,y1+1)+.5)
        a,b,c=p; d=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(d)<1e-9: continue
        w1=((b[1]-c[1])*(xs-c[0])+(c[0]-b[0])*(ys-c[1]))/d; w2=((c[1]-a[1])*(xs-c[0])+(a[0]-c[0])*(ys-c[1]))/d; w3=1-w1-w2
        k=(w1>=-.02)&(w2>=-.02)&(w3>=-.02); m[y0:y1+1,x0:x1+1]|=k
    return m

def apply(body, tex_in, tex_out, goggle_scale=1.5, log=print):
    img=bpy.data.images.load(tex_in); W,H=img.size; px=np.array(img.pixels[:],dtype=np.float32).reshape(H,W,4)[...,:3]   # bottom-up rows == uv v
    me=body.data; me.calc_loop_triangles(); uv=me.uv_layers.active.data
    mw=body.matrix_world; zs=[(mw@v.co).z for v in me.vertices]; top=max(zs); bot=min(zs); Ht=top-bot
    def sample(u,v): return px[int(np.clip(v,0,.9999)*H),int(np.clip(u,0,.9999)*W)]
    faces=[]   # (poly index, centroid world, uv centroid colour hsv)
    for p in me.polygons:
        c=mw@p.center; us=np.mean([uv[l].uv[:] for l in p.loop_indices],0); col=sample(us[0]%1,us[1]%1)
        faces.append((p.index,c,_hsv(col[None])[0]))
    # ---- goggles: orange lens faces on the head, plus the dark rim/strap right next to them ----
    head=[f for f in faces if f[1].z>bot+0.86*Ht]
    lens=[f for f in head if 0.0<=f[2][0]<=0.14 and f[2][1]>0.55 and f[2][2]>0.6]
    gset=set()
    if lens:
        lc=np.array([f[1][:] for f in lens]); 
        for f in head:
            d=np.min(np.linalg.norm(lc-np.array(f[1][:]),axis=1))
            if d<0.045*Ht/1.0 and (f in lens or f[2][2]<0.45): gset.add(f[0])
        for f in lens: gset.add(f[0])
    log("goggle faces",len(gset),"lens faces",len(lens))
    # ---- mesh: scale the goggles up about their own centre and lift them a little so they read from above ----
    if gset and goggle_scale!=1:
        vi=set(v for i in gset for v in me.polygons[i].vertices)
        cen=sum((mw@me.vertices[v].co for v in vi),Vector())/len(vi)
        inv=mw.inverted()
        for v in vi:
            wv=mw@me.vertices[v].co; wv=cen+(wv-cen)*goggle_scale+Vector((0,0,0.004)); me.vertices[v].co=inv@wv
        log("goggles scaled x%.2f about"%goggle_scale,tuple(round(x,3) for x in cen))
    # ---- texture recolour ----
    def face_mask(sel):
        tris=[]
        for i in sel:
            ps=np.array([uv[l].uv[:] for l in me.polygons[i].loop_indices])
            for k in range(1,len(ps)-1): tris.append(np.array([ps[0],ps[k],ps[k+1]]))
        return _raster(tris,W)
    band=lambda lo,hi:[f[0] for f in faces if bot+lo*Ht<=f[1].z<bot+hi*Ht]
    torso=face_mask(band(0.12,0.89)); hsv=_hsv(px); out=px.copy()
    h,s,v=hsv[...,0],hsv[...,1],hsv[...,2]
    # light lavender-grey jacket -> vivid coral red, keeping the painted shading
    jacket=torso&(v>0.40)&(s<0.36)&(h>0.55)&(h<0.95)
    # dark clothes (top, shorts, stockings, belts) -> deep indigo-blue, brighter, so they do not melt into a dark map
    dark=torso&(v<=0.40)&~jacket
    n=hsv.copy()
    n[jacket,0]=0.015; n[jacket,1]=np.clip(0.45+s[jacket]*0.8,0,0.75); n[jacket,2]=np.clip(0.30+v[jacket]*0.85,0,1)
    n[dark,0]=0.60; n[dark,1]=np.clip(0.30+s[dark]*0.5,0,0.55); n[dark,2]=np.clip(0.08+v[dark]*1.3,0,0.5)
    if gset:
        gm=face_mask(sorted(gset)); gl=gm&(h<0.14)&(s>0.5)&(v>0.55)
        n[gl,0]=0.14; n[gl,1]=0.9; n[gl,2]=1.0                       # lens: bright glowing yellow-orange
        gr=gm&~gl; n[gr,0]=0.5; n[gr,1]=0.75; n[gr,2]=np.clip(0.25+v[gr]*1.2,0.35,0.9)  # rim/strap: cyan
    out=_rgb(n)
    arr=np.concatenate([out,np.ones((H,W,1),np.float32)],-1).reshape(-1)
    ni=bpy.data.images.new("styled",W,H,alpha=False); ni.pixels.foreach_set(arr); ni.filepath_raw=tex_out; ni.file_format='JPEG' if tex_out.lower().endswith('jpg') else 'PNG'; ni.save()
    log("jacket px",int(jacket.sum()),"dark px",int(dark.sum()),"-> saved",tex_out)
