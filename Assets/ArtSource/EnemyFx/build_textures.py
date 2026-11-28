import numpy as np, zlib, struct, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Members", "HJH", "08.Effects", "Enemy", "Textures")
PREVIEW = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else None
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)
made = []


def write_png(path, rgba):
    h, w, _ = rgba.shape
    data = (np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8)
    raw = b"".join(b"\x00" + data[y].tobytes() for y in range(h))

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def save(name, alpha, lum=None):
    if lum is None:
        lum = np.ones_like(alpha)
    img = np.dstack([lum, lum, lum, np.clip(alpha, 0, 1)])
    write_png(os.path.join(OUT, name + ".png"), img)
    made.append((name, img))


def grid(n, m=None):
    m = m or n
    y, x = np.mgrid[0:n, 0:m]
    return (x + 0.5) / m * 2 - 1, 1 - (y + 0.5) / n * 2


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def vnoise(x, y, period, seed):
    g = np.random.default_rng(seed).random((period, period))
    xi, yi = np.floor(x).astype(int), np.floor(y).astype(int)
    xf, yf = x - xi, y - yi
    u, v = xf * xf * (3 - 2 * xf), yf * yf * (3 - 2 * yf)
    a = g[yi % period, xi % period]
    b = g[yi % period, (xi + 1) % period]
    c = g[(yi + 1) % period, xi % period]
    d = g[(yi + 1) % period, (xi + 1) % period]
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v


def fbm(x, y, base=4, octaves=4, seed=0):
    out, amp, total = 0, 1.0, 0
    for o in range(octaves):
        p = base * 2 ** o
        out = out + amp * vnoise(x * p, y * p, p, seed + o)
        total += amp
        amp *= 0.55
    return out / total


def seg_dist(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = np.clip(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy + 1e-9), 0, 1)
    return np.hypot(px - ax - dx * t, py - ay - dy * t), t


N = 512
x, y = grid(N)
r = np.hypot(x, y)
a = np.arctan2(y, x)
u01, v01 = (x + 1) / 2, (y + 1) / 2

n1 = fbm(u01, v01, 4, 5, 1)
n2 = fbm(u01, v01, 6, 4, 11)
n3 = fbm(u01, v01, 3, 4, 21)
write_png(os.path.join(OUT, "T_E_Noise.png"), np.dstack([n1, n2, n3, np.ones_like(n1)]))
made.append(("T_E_Noise", np.dstack([n1, n2, n3, np.ones_like(n1)])))

save("T_E_Glow", np.exp(-r * r * 4.2) * smooth(1.0, 0.75, r))

sheet_a = np.zeros((N * 2, N * 2))
sheet_l = np.ones((N * 2, N * 2))
for k in range(4):
    g = np.random.default_rng(100 + k)
    field = np.full_like(x, -1.0)
    lum = np.full_like(x, 0.62)
    blobs = [(0, -0.02, 0.46)]
    for i in range(11):
        ang = i / 11 * 6.283 + g.uniform(-0.25, 0.25)
        d = g.uniform(0.34, 0.54)
        blobs.append((np.cos(ang) * d, np.sin(ang) * d * 0.9, g.uniform(0.17, 0.31)))
    blobs.sort(key=lambda b: -b[1])
    wx = x + (fbm(u01, v01, 5, 3, 200 + k) - 0.5) * 0.22
    wy = y + (fbm(u01, v01, 5, 3, 210 + k) - 0.5) * 0.22
    for bx, by, br in blobs:
        d = 1 - np.hypot(wx - bx, wy - by) / br
        inside = smooth(-0.05, 0.4, d)
        side = np.clip(((wy - by) * 0.85 - (wx - bx) * 0.35) / br, -1, 1)
        tone = 0.74 + 0.2 * smooth(-0.4, 0.8, side)
        lum = lum * (1 - inside) + tone * inside
        field = np.maximum(field, d)
    wisp = fbm(u01, v01, 7, 4, 220 + k)
    lum = lum * 0.65 + 0.35 * (0.72 + 0.28 * smooth(-0.5, 0.6, wy * 0.9 - wx * 0.3))
    alpha = smooth(-0.04, 0.34, field + (wisp - 0.5) * 0.35) * (0.7 + 0.45 * wisp)
    iy, ix = divmod(k, 2)
    sheet_a[iy * N:(iy + 1) * N, ix * N:(ix + 1) * N] = alpha
    sheet_l[iy * N:(iy + 1) * N, ix * N:(ix + 1) * N] = lum
save("T_E_Puff", sheet_a, sheet_l)

mist = fbm(u01 + (n2 - 0.5) * 0.35, v01 + (n3 - 0.5) * 0.35, 4, 5, 31)
save("T_E_Mist", smooth(0.34, 0.8, mist) * smooth(1.0, 0.25, r))

warp = (fbm(u01, v01, 5, 4, 41) - 0.5)
streak = fbm((a / 6.2832) % 1.0, r * 0.35 + warp * 0.2, 14, 3, 43)
d = np.abs(r - 0.80 - warp * 0.09)
width = 0.018 + 0.085 * smooth(0.35, 0.8, streak)
ring = smooth(width, width * 0.3, d) + 0.35 * np.exp(-(d / 0.09) ** 2) * streak
save("T_E_Ring", ring * smooth(1.0, 0.93, r))

alpha = np.zeros_like(x)
g = np.random.default_rng(5)
wa = a + warp * 0.12
for a0, a1, rad, w in ((0.15, 1.35, 0.80, 0.045), (1.75, 2.9, 0.74, 0.035), (3.35, 4.2, 0.83, 0.028),
                       (4.5, 5.85, 0.70, 0.05)):
    t = ((wa - a0) % 6.2832) / (a1 - a0)
    on = (t >= 0) & (t <= 1)
    taper = np.where(on, np.sin(np.clip(t, 0, 1) * np.pi) ** 0.7, 0)
    alpha = np.maximum(alpha, smooth(w * taper, w * taper * 0.55, np.abs(r - rad - warp * 0.06)) * (taper > 0.03))
for i in range(26):
    ang = i / 26 * 6.2832 + g.uniform(-0.1, 0.1)
    r0 = g.uniform(0.62, 0.8)
    r1 = r0 + g.uniform(0.1, 0.3) * (1.5 if i % 4 == 0 else 0.7)
    r1 = min(r1, 0.98)
    ang2 = ang + g.uniform(-0.12, 0.12)
    dist, t = seg_dist(x, y, np.cos(ang) * r0, np.sin(ang) * r0, np.cos(ang2) * r1, np.sin(ang2) * r1)
    w = g.uniform(0.015, 0.045) * np.sin(np.clip(t * 0.85 + 0.12, 0, 1) * np.pi)
    alpha = np.maximum(alpha, smooth(w, w * 0.4, dist))
save("T_E_ShockBreak", alpha)

alpha = np.zeros_like(x)
g = np.random.default_rng(8)
bx = x + (fbm(u01, v01, 6, 3, 51) - 0.5) * 0.10
for i in range(19):
    k = i / 18 * 2 - 1
    sx = k * 0.42 + g.uniform(-0.05, 0.05)
    sy = -0.72 + abs(k) * 0.35 + g.uniform(-0.08, 0.08)
    length = (1.45 - abs(k) * 0.55) * g.uniform(0.7, 1.05)
    ex = sx + k * 0.50 + g.uniform(-0.08, 0.08)
    ey = min(sy + length, 0.95)
    dist, t = seg_dist(bx, y, sx, sy, ex, ey)
    w = g.uniform(0.06, 0.12) * (1 - t) ** 0.8 * smooth(0.0, 0.25, t + 0.08)
    alpha = np.maximum(alpha, smooth(w, w * 0.25, dist) * (w > 0.003))
lines = fbm(u01 * 1.0, v01 * 0.18, 24, 2, 53)
save("T_E_Burst", alpha * (0.8 + 0.35 * lines))

g = np.random.default_rng(13)
wx = x + (fbm(u01, v01, 7, 4, 61) - 0.5) * 0.16
wy = y + (fbm(u01, v01, 7, 4, 62) - 0.5) * 0.16
pts = []
for ring_r, count in ((0.0, 1), (0.3, 6), (0.62, 11), (0.95, 16), (1.3, 20)):
    for i in range(count):
        ang = i / count * 6.283 + g.uniform(-0.2, 0.2)
        rr = ring_r + g.uniform(-0.07, 0.07)
        pts.append((np.cos(ang) * rr, np.sin(ang) * rr))
dists = np.stack([np.hypot(wx - px, wy - py) for px, py in pts])
dists.sort(axis=0)
edge = dists[1] - dists[0]
width = 0.05 * np.clip(1 - r, 0, 1) + 0.006
crack = smooth(width, width * 0.35, edge)
halo = np.exp(-(edge / 0.07) ** 2) * 0.35
fade = smooth(0.98, 0.5, r + (n2 - 0.5) * 0.45)
save("T_E_Crack", np.clip(crack + halo, 0, 1) * fade, np.clip(0.45 + crack * 0.55, 0, 1))

edge_r = 0.84 + (fbm((a / 6.2832) % 1.0, v01 * 0.0, 9, 3, 71) - 0.5) * 0.22
inner = fbm(u01 + (n2 - 0.5) * 0.3, v01 + (n3 - 0.5) * 0.3, 5, 4, 73)
body = smooth(edge_r + 0.02, edge_r - 0.05, r)
lum = 0.35 + 0.35 * smooth(0.4, 0.7, inner)
rim = body * smooth(edge_r - 0.13, edge_r - 0.03, r)
lum = np.maximum(lum, rim * 1.0)
alpha = body * (0.55 + 0.25 * inner) + rim * 0.35
g = np.random.default_rng(17)
for i in range(38):
    ang, rr, br = g.uniform(0, 6.283), np.sqrt(g.uniform(0, 1)) * 0.68, g.uniform(0.018, 0.075)
    dist = np.hypot(x - np.cos(ang) * rr, y - np.sin(ang) * rr)
    hoop = smooth(0.016, 0.004, np.abs(dist - br))
    disc = smooth(br, br - 0.01, dist)
    lum = lum * (1 - disc * 0.7) + 0.28 * disc * 0.7
    lum = np.maximum(lum, hoop)
    alpha = np.maximum(alpha, hoop * 0.95)
for i in range(16):
    ang, br = g.uniform(0, 6.283), g.uniform(0.02, 0.055)
    rr = g.uniform(0.86, 0.94)
    disc = smooth(br, br * 0.6, np.hypot(x - np.cos(ang) * rr, y - np.sin(ang) * rr))
    alpha = np.maximum(alpha, disc * 0.8)
    lum = np.maximum(lum, disc * 0.8)
save("T_E_Pool", np.clip(alpha, 0, 1), np.clip(lum, 0, 1))

half = 0.46 * np.sin(np.clip((x + 0.92) / 1.84, 0, 1) * np.pi) ** 0.9 * (1 - 0.25 * np.clip(x, 0, 1))
shape = smooth(0.02, -0.01, np.abs(y - 0.08 * np.sin(x * 2.4)) - half)
vein = smooth(0.028, 0.008, np.abs(y - 0.08 * np.sin(x * 2.4)))
lum = np.where(y > 0.08 * np.sin(x * 2.4), 1.0, 0.72) + vein * 0.3
save("T_E_Leaf", shape, np.clip(lum, 0, 1))

alpha = np.zeros_like(x)
g = np.random.default_rng(23)
poly = np.array([(-0.55, -0.3), (-0.1, -0.7), (0.62, -0.28), (0.4, 0.45), (-0.18, 0.72), (-0.6, 0.2)])
inside = np.ones_like(x, dtype=bool)
for i in range(len(poly)):
    p, q = poly[i], poly[(i + 1) % len(poly)]
    inside &= ((q[0] - p[0]) * (y - p[1]) - (q[1] - p[1]) * (x - p[0])) > 0
lum = np.where(y + x * 0.4 > 0.05, 1.0, 0.55)
save("T_E_Chip", inside.astype(float), lum)

hx = (x - 0.35) / 0.5
head = np.hypot(hx, y / 0.46)
tail_t = np.clip((0.35 - x) / 1.3, 0, 1)
flow = fbm(u01 * 0.35, v01 + (n2 - 0.5) * 0.18, 12, 3, 81)
tail_w = 0.46 * (1 - tail_t) ** 0.75 * (0.75 + 0.5 * fbm(u01 * 0.8, v01 * 0.25, 8, 3, 83))
tail = smooth(tail_w, tail_w * 0.25, np.abs(y + (n3 - 0.5) * 0.3 * tail_t)) * smooth(0.62, 0.15, x) * (1 - tail_t) ** 0.35
tail *= smooth(0.28, 0.62, flow + (1 - tail_t) * 0.4)
crescent = smooth(0.28, 0.0, np.abs(head - 0.86)) * smooth(-0.5, 0.35, hx)
core = smooth(0.95, 0.2, head)
alpha = np.clip(np.maximum.reduce([tail * 0.9, crescent, core * 0.85]), 0, 1) * smooth(1.0, 0.86, np.abs(x))
save("T_E_Comet", alpha, np.clip(0.55 + core * 0.2 + crescent * 0.6 + tail * 0.15, 0, 1))

xs, ys = grid(128, 512)
us, vs = (xs + 1) / 2, (ys + 1) / 2
flow = fbm(us * 0.5, vs, 8, 4, 91)
w = 0.35 + 0.6 * us
alpha = smooth(w, w * 0.15, np.abs(ys + (flow - 0.5) * 0.55 * (1 - us))) * smooth(0.0, 0.55, us + (flow - 0.5) * 0.5)
save("T_E_Trail", alpha * smooth(0.22, 0.6, flow + us * 0.45), 0.7 + 0.3 * smooth(0.5, 0.15, np.abs(ys)))

wy = y
sway = np.sin(wy * 4.6) * 0.20 * np.clip((wy + 0.2), 0, 1) + (n2 - 0.5) * 0.22
head = np.hypot(x / 0.34, (y + 0.42) / 0.34)
tt = np.clip((y + 0.42) / 1.32, 0, 1)
tw = 0.34 * (1 - tt) ** 1.1
tail = smooth(tw, tw * 0.2, np.abs(x - sway)) * (y > -0.42) * (1 - tt) ** 0.3
alpha = np.maximum(smooth(1.15, 0.55, head), tail * smooth(0.2, 0.6, n1 + (1 - tt) * 0.5))
save("T_E_Wisp", np.clip(alpha, 0, 1), np.clip(0.65 + smooth(0.9, 0.2, head) * 0.35, 0, 1))

alpha = np.zeros_like(x)
for rad, w, power in ((1.50, 0.11, 1.0), (1.18, 0.06, 0.75), (0.92, 0.04, 0.5)):
    rr = np.hypot(x, y + 0.85)
    ang = np.arctan2(x, y + 0.85)
    taper = smooth(0.62, 0.15, np.abs(ang))
    alpha = np.maximum(alpha, smooth(w * taper + 0.004, w * taper * 0.2, np.abs(rr - rad + (n2 - 0.5) * 0.07)) * taper * power)
save("T_E_Arc", alpha)

star = np.exp(-(np.abs(x) * np.abs(y)) * 60) * smooth(0.95, 0.0, r)
save("T_E_Flash", np.clip(star * 0.8 + np.exp(-r * r * 14), 0, 1))

drop = smooth(0.9, 0.7, r)
save("T_E_Bubble", smooth(0.14, 0.02, np.abs(r - 0.68)) + drop * 0.18 + smooth(0.2, 0.08, np.hypot(x + 0.25, y - 0.28)))

xs, ys = grid(128, 512)
us, vs = (xs + 1) / 2, (ys + 1) / 2
lines = fbm(us, vs * 0.2, 16, 3, 95)
band = smooth(0.0, 0.62, vs) ** 1.6 * smooth(1.0, 0.9, vs) * smooth(0.0, 0.2, us) * smooth(1.0, 0.8, us)
save("T_E_Band", np.clip(band * (0.6 + 0.8 * lines), 0, 1), np.clip(0.55 + 0.55 * smooth(0.55, 0.8, vs), 0, 1))

if PREVIEW:
    cell = 256
    cols = 6
    rows = (len(made) + cols - 1) // cols
    canvas = np.zeros((rows * cell, cols * cell, 4))
    canvas[..., :3] = (0.12, 0.14, 0.18)
    canvas[..., 3] = 1
    for i, (name, img) in enumerate(made):
        h, w, _ = img.shape
        yi = np.linspace(0, h - 1, cell if h >= w else cell * h // w).astype(int)
        xi = np.linspace(0, w - 1, cell).astype(int)
        s = img[yi][:, xi]
        if name == "T_E_Noise":
            s = s.copy()
            s[..., :3] = s[..., :1]
        oy, ox = (i // cols) * cell, (i % cols) * cell
        al = s[..., 3:4]
        region = canvas[oy:oy + s.shape[0], ox:ox + cell, :3]
        canvas[oy:oy + s.shape[0], ox:ox + cell, :3] = region * (1 - al) + s[..., :3] * al
    write_png(PREVIEW, canvas)
print("TEXTURES", len(made))
