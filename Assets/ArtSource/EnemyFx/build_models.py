import bpy, bmesh, math, random, os, sys
from mathutils import Vector, Matrix, Quaternion, noise

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Members", "HJH", "08.Effects", "Enemy", "Models")
PREVIEW = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else None
os.makedirs(OUT, exist_ok=True)

BARK_D = (0.055, 0.028, 0.014)
BARK = (0.17, 0.085, 0.032)
BARK_L = (0.33, 0.19, 0.075)
VINE_D = (0.012, 0.035, 0.024)
VINE = (0.035, 0.12, 0.065)
CRY_D = (0.006, 0.045, 0.032)
CRY = (0.025, 0.21, 0.11)
CRY_L = (0.18, 0.66, 0.30)
GLOW = (0.30, 1.00, 0.42)
ROCK_D = (0.030, 0.038, 0.045)
ROCK = (0.095, 0.115, 0.125)
LEAF_D = (0.025, 0.10, 0.025)
LEAF = (0.11, 0.33, 0.055)
TEAL = (0.15, 1.00, 0.80)


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


class Mesh:
    def __init__(self):
        self.v, self.f, self.c, self.uv = [], [], [], []

    def add(self, pts, cols, uvs=None):
        base = len(self.v)
        self.v.extend(pts)
        idx = list(range(base, base + len(pts)))
        self.f.append(idx)
        self.c.append(cols)
        self.uv.append(uvs or [(0.0, 0.0)] * len(pts))

    def tri(self, a, b, c, ca, cb=None, cc=None):
        self.add([a, b, c], [ca, cb or ca, cc or ca])

    def quad(self, a, b, c, d, ca, cb=None, cc=None, cd=None):
        self.add([a, b, c, d], [ca, cb or ca, cc or ca, cd or ca])

    def build(self, name, smooth=False):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(p) for p in self.v], [], self.f)
        col = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        uvl = me.uv_layers.new(name="UVMap")
        li = 0
        for fi, poly in enumerate(me.polygons):
            for k in range(poly.loop_total):
                c = self.c[fi][k]
                col.data[li].color = (c[0], c[1], c[2], c[3] if len(c) > 3 else 0.0)
                uvl.data[li].uv = self.uv[fi][k]
                li += 1
        if smooth:
            bm = bmesh.new()
            bm.from_mesh(me)
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
            bm.to_mesh(me)
            bm.free()
            for p in me.polygons:
                p.use_smooth = True
        ob = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(ob)
        return ob


def frames(spine):
    out = []
    normal = None
    for i, p in enumerate(spine):
        t = (spine[min(i + 1, len(spine) - 1)] - spine[max(i - 1, 0)]).normalized()
        if normal is None:
            ref = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 1, 0))
            normal = (ref - t * ref.dot(t)).normalized()
        else:
            normal = (normal - t * normal.dot(t)).normalized()
        out.append((t, normal.copy(), t.cross(normal).normalized()))
    return out


def tube(m, spine, radius, sides, color, twist=0.0, jitter=0.0, rng=None, closed=False):
    rng = rng or random.Random(1)
    fr = frames(spine)
    rings = []
    n = len(spine)
    jit = [1 + (rng.random() - 0.5) * 2 * jitter for _ in range(sides)]
    for i, p in enumerate(spine):
        u = i / (n - 1)
        t, a, b = fr[i]
        ring = []
        for s in range(sides):
            ang = 2 * math.pi * s / sides + twist * u
            r = radius(u) * jit[s] * (1 + (rng.random() - 0.5) * jitter * 0.7)
            ring.append(p + (a * math.cos(ang) + b * math.sin(ang)) * r)
        rings.append(ring)
    for i in range(n - 1):
        u0, u1 = i / (n - 1), (i + 1) / (n - 1)
        for s in range(sides):
            s2 = (s + 1) % sides
            m.add([rings[i][s], rings[i][s2], rings[i + 1][s2], rings[i + 1][s]],
                  [color(u0, s), color(u0, s2), color(u1, s2), color(u1, s)],
                  [(s / sides, u0), ((s + 1) / sides, u0), ((s + 1) / sides, u1), (s / sides, u1)])
    if not closed:
        for s in range(sides):
            s2 = (s + 1) % sides
            m.tri(spine[0], rings[0][s2], rings[0][s], color(0, s))
            m.tri(spine[-1], rings[-1][s], rings[-1][s2], color(1, s))
    return rings


def spline(points, steps):
    out = []
    n = len(points)
    for i in range(steps):
        x = i / (steps - 1) * (n - 1)
        k = min(int(x), n - 2)
        t = x - k
        p0, p1 = points[max(k - 1, 0)], points[k]
        p2, p3 = points[k + 1], points[min(k + 2, n - 1)]
        out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                          + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    return out


def rand_unit(rng):
    z = rng.uniform(-1, 1)
    a = rng.uniform(0, 2 * math.pi)
    r = math.sqrt(1 - z * z)
    return Vector((r * math.cos(a), r * math.sin(a), z))


def rock(m, center, size, rng, squash=0.75, glow=0.0):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    rot = Quaternion(rand_unit(rng), rng.uniform(0, 6.28))
    pos = {}
    for v in bm.verts:
        d = v.co.normalized()
        k = 0.65 + rng.random() * 0.6
        p = rot @ (d * k)
        pos[v.index] = center + Vector((p.x * size, p.y * size, p.z * size * squash))
    for f in bm.faces:
        pts = [pos[v.index] for v in f.verts]
        nz = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized().z
        c = mix(ROCK_D, ROCK, 0.45 + nz * 0.55 + (rng.random() - 0.5) * 0.3)
        lit = rng.random() < glow
        m.add(pts, [(mix(c, CRY_L, 0.7) + (0.5,)) if lit else c + (0.0,)] * 3)
    bm.free()


def thorn(m, cone_len, cone_r, base, direction, color):
    d = direction.normalized()
    spine = [base - d * cone_r * 0.3, base + d * cone_len * 0.45, base + d * cone_len + Vector((0, 0, cone_len * 0.15))]
    tube(m, spine, lambda u: cone_r * (1 - u) + 0.004, 4, lambda u, s: mix(color, BARK_L, u * 0.9) + (0.0,))


def leaf(m, base, direction, size, rng):
    d = direction.normalized()
    side = d.cross(rand_unit(rng)).normalized()
    up = side.cross(d).normalized()
    mid = base + d * size * 0.5 + up * size * 0.12
    tip = base + d * size
    l = mid + side * size * 0.32
    r = mid - side * size * 0.32
    dark, light = LEAF_D + (0.0,), LEAF + (0.08,)
    for a, b, c in ((base, l, mid), (mid, l, tip), (base, mid, r), (mid, tip, r)):
        m.tri(a, b, c, dark, light, light)
        m.tri(a, c, b, dark, light, light)


def spike(name, height, radius, lean, hook, seed):
    rng = random.Random(seed)
    m = Mesh()
    steps = 7
    spine = []
    for i in range(steps):
        u = i / (steps - 1)
        bend = lean * u * u * height + hook * math.sin(u * math.pi) * height
        spine.append(Vector((bend, math.sin(u * 5 + seed) * radius * 0.12 * (1 - u), u * height - 0.15)))
    sides = 6
    prof = lambda u: radius * (1.0 + 0.28 * math.sin(u * math.pi * 0.9)) * (1 - u) ** 0.85 + 0.006
    shade = [rng.uniform(-0.25, 0.25) for _ in range(sides)]

    def col(u, s):
        c = mix(CRY_D, CRY, u * 1.35 + shade[s])
        c = mix(c, CRY_L, (u - 0.55) * 1.8 + shade[s] * 0.5)
        return c + (max(0.0, u - 0.75) * 1.6,)

    rings = tube(m, spine, prof, sides, col, twist=0.7, jitter=0.28, rng=rng)
    for s in (0, 2, 4):
        line = [rings[i][s] for i in range(steps)]
        line = [p + (p - spine[i]) * 0.05 for i, p in enumerate(line)]
        tube(m, line, lambda u: radius * 0.065 * math.sin(math.pi * min(0.98, u * 0.85 + 0.08)), 4,
             lambda u, k: GLOW + (1.0 - u * 0.35,))
    for _ in range(3):
        a = rng.uniform(0, 6.28)
        o = Vector((math.cos(a), math.sin(a), 0))
        h = height * rng.uniform(0.22, 0.42)
        base = o * radius * 0.75 + Vector((0, 0, -0.1))
        tube(m, [base, base + o * h * 0.22 + Vector((0, 0, h * 0.55)), base + o * h * 0.55 + Vector((0, 0, h))],
             lambda u: radius * 0.36 * (1 - u) + 0.004, 5, lambda u, s: mix(CRY_D, CRY_L, u * 1.1 - 0.1) + (u * 0.4,),
             jitter=0.2, rng=rng)
    for i in range(9):
        a = i / 9 * 6.283 + rng.uniform(-0.2, 0.2)
        dist = radius * rng.uniform(1.0, 1.55)
        size = radius * rng.uniform(0.28, 0.62)
        rock(m, Vector((math.cos(a) * dist, math.sin(a) * dist, size * 0.18)), size, rng, glow=0.07)
    return m.build(name)


def thorny_vine(name, seed, length):
    rng = random.Random(seed)
    m = Mesh()

    def branch(start, direction, ln, r, depth):
        pts = [start]
        d = direction.normalized()
        p = start.copy()
        for i in range(4):
            d = (d + rand_unit(rng) * 0.65 + Vector((0, 0, 0.22))).normalized()
            p = p + d * ln / 4
            pts.append(p.copy())
        sp = spline(pts, 14)
        tube(m, sp, lambda u: r * (1 - u * 0.93), 6,
             lambda u, s: mix(VINE_D, VINE, u * 0.9 + (0.25 if s % 3 == 0 else 0)) + (0.55 if s == 1 and u > 0.25 else 0.0,),
             twist=2.5)
        for i in range(2, 12, 2):
            t = (sp[i + 1] - sp[i - 1]).normalized()
            out = t.cross(rand_unit(rng)).normalized()
            thorn(m, r * 2.1 * (1 - i / 16), r * 0.45 * (1 - i / 18), sp[i] + out * r * 0.5 * (1 - i / 14), out, VINE)
        if depth > 0:
            for i in (4, 8):
                t = (sp[i + 1] - sp[i - 1]).normalized()
                side = t.cross(rand_unit(rng)).normalized()
                branch(sp[i], side + t * 0.5, ln * 0.5, r * 0.55 * (1 - i / 20), depth - 1)

    branch(Vector((0, 0, -0.1)), Vector((0, 0, 1)), length, length * 0.085, 1)
    return m.build(name)


def thick_vine(name, seed, height):
    rng = random.Random(seed)
    m = Mesh()
    steps = 22
    lean = rng.uniform(0.15, 0.35) * height
    core = []
    for i in range(steps):
        u = i / (steps - 1)
        core.append(Vector((math.sin(u * 3.4) * lean * 0.55 + u * u * lean * 0.6,
                            math.sin(u * 2.2 + seed) * lean * 0.3, u * height - 0.25)))
    strands = 4
    for k in range(strands):
        path = []
        for i, p in enumerate(core):
            u = i / (steps - 1)
            ang = k / strands * 6.283 + u * 6.5
            off = height * 0.075 * (1 - u * 0.75) + (height * 0.1) * max(0, 0.22 - u) * 5
            path.append(p + Vector((math.cos(ang) * off, math.sin(ang) * off, 0)))
        r = height * 0.075 * rng.uniform(0.8, 1.15)
        tone = [BARK_D, BARK, BARK_L, BARK][k]
        tube(m, path, lambda u, r=r: r * (1 - u * 0.88) * (1 + 0.5 * max(0, 0.18 - u) * 5), 7,
             lambda u, s, tone=tone: mix(BARK_D, tone, 0.35 + (s % 3) * 0.35 + u * 0.2) + (0.0,), twist=3.0,
             jitter=0.18, rng=rng)
        if k == 1:
            glow = [path[i] + (path[i] - core[i]) * 0.65 for i in range(3, 17)]
            tube(m, glow, lambda u: height * 0.011 * math.sin(math.pi * (u * 0.9 + 0.05)), 4,
                 lambda u, s: TEAL + (1.0,))
    for i in range(5, steps - 2, 3):
        t = (core[i + 1] - core[i - 1]).normalized()
        out = t.cross(rand_unit(rng)).normalized()
        ln = height * rng.uniform(0.18, 0.3)
        a = core[i] + out * height * 0.07
        mid = a + out * ln * 0.55 + Vector((0, 0, ln * 0.25))
        end = mid + out * ln * 0.25 + Vector((0, 0, ln * 0.45))
        tube(m, spline([a, mid, end], 8), lambda u: height * 0.028 * (1 - u * 0.9), 5,
             lambda u, s: mix(BARK, BARK_L, u) + (0.0,))
        for _ in range(4):
            leaf(m, mid.lerp(end, rng.random()), rand_unit(rng) + out * 0.4, height * 0.1, rng)
        thorn(m, height * 0.09, height * 0.022, core[i] - out * height * 0.06, -out, BARK)
    for _ in range(5):
        leaf(m, core[-1], rand_unit(rng) + Vector((0, 0, 0.5)), height * 0.09, rng)
    for i in range(6):
        a = i / 6 * 6.283 + rng.uniform(-0.3, 0.3)
        o = Vector((math.cos(a), math.sin(a), 0))
        path = [Vector((0, 0, height * 0.14)), o * height * 0.17 + Vector((0, 0, height * 0.06)),
                o * height * 0.34 + Vector((0, 0, -0.06))]
        tube(m, spline(path, 8), lambda u: height * 0.055 * (1 - u * 0.85), 6,
             lambda u, s: mix(BARK_D, BARK, (s % 3) * 0.45) + (0.0,))
    return m.build(name)


def vine_wheel(name, seed):
    rng = random.Random(seed)
    m = Mesh()
    R = 0.74
    count = 8
    for k in range(count):
        offset = -0.5 + k / (count - 1) + rng.uniform(-0.06, 0.06)
        tilt_a = rng.uniform(-0.75, 0.75)
        tilt_b = rng.uniform(0, 6.28)
        rad = R * (1.0 - abs(offset) * 0.35) * rng.uniform(0.9, 1.06)
        r = rng.uniform(0.055, 0.12)
        steps = 34
        pts = []
        phase = rng.uniform(0, 6.28)
        for i in range(steps):
            a = i / (steps - 1) * 6.283185
            wob = 1 + 0.09 * math.sin(a * 3 + phase) + 0.05 * math.sin(a * 5 + phase * 2)
            x = offset * 0.85 + math.sin(a + tilt_b) * tilt_a * 0.5 + 0.07 * math.sin(a * 4 + phase)
            pts.append(Vector((x, math.cos(a) * rad * wob, math.sin(a) * rad * wob)))
        tone = rng.choice([(BARK_D, BARK), (BARK, BARK_L), (VINE_D, VINE), (BARK_D, BARK)])
        glow = k in (2, 5)
        tube(m, pts, lambda u, r=r: r * (0.9 + 0.1 * math.sin(u * 18.85)), 6,
             lambda u, s, tone=tone, glow=glow: (TEAL + (0.8,)) if glow and s == 0 and math.sin(u * 12.57) > 0.2
             else mix(tone[0], tone[1], (s % 3) * 0.45 + 0.1) + (0.0,), twist=6.283 * 2, closed=True)
        for i in range(2, steps - 1, 5):
            out = Vector((0, pts[i].y, pts[i].z)).normalized()
            out = (out + rand_unit(rng) * 0.55).normalized()
            thorn(m, rng.uniform(0.14, 0.27), r * 0.55, pts[i] + out * r * 0.6, out, tone[0])
        for i in range(4, steps - 1, 9):
            out = Vector((0, pts[i].y, pts[i].z)).normalized()
            leaf(m, pts[i] + out * r, out + rand_unit(rng) * 0.8, rng.uniform(0.18, 0.3), rng)
    for k in range(6):
        a = rand_unit(rng) * 0.55
        b = rand_unit(rng) * 0.55
        c = rand_unit(rng) * 0.3
        tube(m, spline([a, c, b], 9), lambda u: 0.055, 5, lambda u, s: mix(BARK_D, BARK, (s % 2) * 0.6) + (0.0,))
    return m.build(name)


def revolve(name, rings, sides, profile, color, wobble=0.0, seed=0):
    m = Mesh()
    pts = []
    for i in range(rings + 1):
        v = i / rings
        y, r = profile(v)
        ring = []
        for s in range(sides + 1):
            a = (s % sides) / sides * 6.283185
            d = Vector((math.cos(a), y, math.sin(a)))
            n = noise.noise(Vector((math.cos(a) * 2.2 + seed, y * 2.2, math.sin(a) * 2.2)))
            k = r * (1 + n * wobble)
            ring.append(Vector((math.cos(a) * k, y, math.sin(a) * k)))
        pts.append(ring)
    for i in range(rings):
        for s in range(sides):
            m.add([pts[i][s], pts[i + 1][s], pts[i + 1][s + 1], pts[i][s + 1]],
                  [color(i / rings), color((i + 1) / rings), color((i + 1) / rings), color(i / rings)],
                  [(s / sides, i / rings), (s / sides, (i + 1) / rings), ((s + 1) / sides, (i + 1) / rings),
                   ((s + 1) / sides, i / rings)])
    return m.build(name, smooth=True)


def orb_core():
    return revolve("SM_Orb_Core", 20, 28, lambda v: (-math.cos(v * math.pi) * 0.5, math.sin(v * math.pi) * 0.5),
                   lambda v: (1, 1, 1, 1))


def orb_shell():
    def prof(v):
        if v < 0.45:
            a = v / 0.45 * math.pi * 0.5
            return -math.cos(a) * 0.62, math.sin(a) * 0.62
        k = (v - 0.45) / 0.55
        return k * 1.75, 0.62 * (1 - k) ** 1.35 + 0.002

    return revolve("SM_Orb_Shell", 28, 32, prof, lambda v: (1, 1, 1, 1 - max(0, v - 0.55) / 0.45), wobble=0.22, seed=3)


def spit():
    rng = random.Random(9)
    m = Mesh()
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=0.42)
    pos = {}
    for v in bm.verts:
        d = v.co.normalized()
        n = noise.noise(d * 2.4)
        p = d * 0.42 * (1 + n * 0.35)
        if p.y > 0:
            p.y *= 1.7
            p.x *= 1 - p.y * 0.35
            p.z *= 1 - p.y * 0.35
        pos[v.index] = p
    for f in bm.faces:
        pts = [pos[v.index] for v in f.verts]
        cols = []
        for p in pts:
            n = noise.noise(p * 5.0)
            hot = max(0.0, n) * 1.6
            cols.append(mix((0.02, 0.10, 0.005), (0.45, 0.85, 0.03), hot + 0.2 - p.y * 0.4) + (min(1.0, hot),))
        m.add(pts, cols)
    bm.free()
    for _ in range(7):
        a = rng.uniform(0, 6.28)
        front = Vector((math.cos(a) * 0.5, rng.uniform(-0.25, 0.1), math.sin(a) * 0.5)).normalized()
        base = front * 0.36
        tube(m, [base, base + front * 0.12 + Vector((0, 0.1, 0)), base + front * 0.17 + Vector((0, 0.3, 0))],
             lambda u: 0.07 * (1 - u) + 0.004, 5, lambda u, s: mix((0.10, 0.035, 0.12), (0.55, 0.16, 0.5), u) + (u * 0.35,))
    for i in range(4):
        c = Vector((rng.uniform(-0.18, 0.18), 0.75 + i * 0.22, rng.uniform(-0.18, 0.18)))
        r = 0.11 * (1 - i * 0.17)
        tube(m, [c + Vector((0, -r, 0)), c, c + Vector((0, r * 2.2, 0))],
             lambda u, r=r: r * math.sin(math.pi * (u * 0.9 + 0.05)), 6, lambda u, s: (0.28, 0.65, 0.03, 0.5))
    return m.build("SM_PlantSpit")


def scream_arc():
    m = Mesh()
    segs = 28
    half = math.radians(22.5)
    for layer, (z0, z1, lift) in enumerate(((0.0, 0.0, 0),)):
        rows = [(0.80, -0.045), (0.90, 0.0), (1.0, 0.045), (1.0, 0.075), (0.94, 0.12)]
        for j in range(len(rows) - 1):
            for i in range(segs):
                u0, u1 = i / segs, (i + 1) / segs
                quad = []
                uvs = []
                for u, k in ((u0, j), (u1, j), (u1, j + 1), (u0, j + 1)):
                    a = -half + u * half * 2
                    taper = math.sin(u * math.pi) ** 0.55
                    r, h = rows[k]
                    r = 1 - (1 - r) * taper
                    quad.append(Vector((math.sin(a) * r, -math.cos(a) * r, h * taper)))
                    uvs.append((u, k / (len(rows) - 1)))
                m.add(quad, [(1, 1, 1, 1)] * 4, uvs)
    return m.build("SM_ScreamArc", smooth=True)


def debris(name, seed):
    rng = random.Random(seed)
    m = Mesh()
    rock(m, Vector((0, 0, 0)), 0.5, rng, squash=0.8, glow=0.1)
    return m.build(name)


def root_chunk():
    rng = random.Random(41)
    m = Mesh()
    pts = [Vector((-0.5, 0, 0)), Vector((-0.1, 0.12, 0.08)), Vector((0.2, -0.08, -0.02)), Vector((0.55, 0.05, 0.1))]
    tube(m, spline(pts, 10), lambda u: 0.13 * (1 - u * 0.8), 5, lambda u, s: mix(BARK_D, BARK_L, (s % 3) * 0.4) + (0.0,),
         jitter=0.3, rng=rng)
    thorn(m, 0.25, 0.06, pts[1], Vector((0.1, 0.4, 0.9)), BARK)
    thorn(m, 0.2, 0.05, pts[2], Vector((0.1, -0.9, -0.2)), BARK)
    return m.build("SM_Debris_Root")


def leaf_mesh():
    rng = random.Random(5)
    m = Mesh()
    leaf(m, Vector((-0.5, 0, 0)), Vector((1, 0, 0)), 1.0, rng)
    return m.build("SM_Leaf")


def export(ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True,
                             object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, colors_type="LINEAR",
                             mesh_smooth_type="OFF", add_leaf_bones=False, bake_anim=False)


bpy.ops.wm.read_factory_settings(use_empty=True)
objs = [
    spike("SM_Thorn_L", 4.2, 0.62, 0.10, 0.0, 11),
    spike("SM_Thorn_M", 3.0, 0.48, -0.14, 0.0, 23),
    spike("SM_Thorn_S", 1.8, 0.34, 0.20, 0.0, 37),
    spike("SM_Thorn_Hook", 3.4, 0.46, 0.34, -0.14, 53),
    thorny_vine("SM_Vine_Thorny_A", 7, 2.8),
    thorny_vine("SM_Vine_Thorny_B", 19, 2.2),
    thick_vine("SM_Vine_Thick", 3, 4.0),
    vine_wheel("SM_VineWheel_A", 101),
    vine_wheel("SM_VineWheel_B", 202),
    vine_wheel("SM_VineWheel_C", 303),
    vine_wheel("SM_VineWheel_D", 404),
    vine_wheel("SM_VineWheel_E", 505),
    orb_core(),
    orb_shell(),
    spit(),
    scream_arc(),
    debris("SM_Debris_RockA", 61),
    debris("SM_Debris_RockB", 62),
    root_chunk(),
    leaf_mesh(),
]
for o in objs:
    export(o)
    print("MODEL", o.name, len(o.data.polygons))

if PREVIEW:
    mat = bpy.data.materials.new("vc")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "Col"
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    emit = nt.nodes.new("ShaderNodeEmission")
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = 2.5
    add = nt.nodes.new("ShaderNodeAddShader")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(attr.outputs["Color"], diff.inputs["Color"])
    nt.links.new(attr.outputs["Color"], emit.inputs["Color"])
    nt.links.new(attr.outputs["Alpha"], mul.inputs[0])
    nt.links.new(mul.outputs[0], emit.inputs["Strength"])
    nt.links.new(diff.outputs[0], add.inputs[0])
    nt.links.new(emit.outputs[0], add.inputs[1])
    nt.links.new(add.outputs[0], out.inputs["Surface"])
    cols = 5
    for i, o in enumerate(objs):
        o.data.materials.append(mat)
        d = max(o.dimensions)
        k = 3.2 / d
        o.scale = (k, k, k)
        bpy.context.view_layer.update()
        corners = [o.matrix_world @ Vector(c) for c in o.bound_box]
        center = sum(corners, Vector()) / 8
        o.location = Vector(((i % cols) * 4.2, 0, -(i // cols) * 4.0)) - center
        o.rotation_euler = (0, 0, math.radians(25))
        bpy.context.view_layer.update()
        corners = [o.matrix_world @ Vector(c) for c in o.bound_box]
        center = sum(corners, Vector()) / 8
        o.location += Vector(((i % cols) * 4.2, 0, -(i // cols) * 4.0)) - center
    cam_data = bpy.data.cameras.new("c")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 21.5
    cam = bpy.data.objects.new("c", cam_data)
    bpy.context.collection.objects.link(cam)
    target = Vector((8.4, 0, -6.0))
    direction = Vector((0.0, -1.0, 0.35)).normalized()
    cam.location = target + direction * 40
    cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN"))
    sun.data.energy = 4.0
    sun.rotation_euler = (math.radians(45), math.radians(25), 0)
    bpy.context.collection.objects.link(sun)
    sc = bpy.context.scene
    sc.camera = cam
    world = bpy.data.worlds.new("w")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.09, 0.11, 0.14, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x = 1600
    sc.render.resolution_y = 1300
    sc.render.filepath = PREVIEW
    bpy.ops.render.render(write_still=True)
