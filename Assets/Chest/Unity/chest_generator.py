import sys, os, math
import numpy as np
from PIL import Image

OUT = sys.argv[1]
TYPE = sys.argv[2] if len(sys.argv) > 2 else "Common"
A = 50                                  # chunky art grid; every pixel is drawn 2 x 2 in the 100 x 100 picture
def hx(h): return tuple(int(h[i:i+2], 16) for i in (1, 3, 5))
STYLES = {
    "Common": dict(wood=[hx("#ffd27c"), hx("#f59a30"), hx("#c8631e"), hx("#843c1c")], body=[hx("#b06c3a"), hx("#8c5228"), hx("#6a3a20"), hx("#482614")],
                   steel=[hx("#eef2fc"), hx("#b4bed8"), hx("#7c87a2"), hx("#4a5270")], outline=hx("#1a1420"), inner=[hx("#27324a"), hx("#1c2438"), hx("#141a2a")]),
}
S = STYLES[TYPE]
WOOD, BODY, STEEL, OL, INSIDE = S["wood"], S["body"], S["steel"], S["outline"], S["inner"]
def hsh(x, y, s=0):
    n = (int(x) * 374761393 + int(y) * 668265263 + s * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFF) / 255.0
def pick(ramp, t): return ramp[max(0, min(len(ramp) - 1, int(round(t))))]
Y, X = np.mgrid[0:A, 0:A]
X0, X1 = 9, 40                          # the chest is 36 art px wide (72 px)
CXA = (X0 + X1) / 2.0

class Cv:
    def __init__(self): self.rgb = np.zeros((A, A, 3), np.uint8); self.a = np.zeros((A, A), np.uint8); self.mat = np.zeros((A, A), np.uint8)
    def paint(self, mask, fn, mat):
        for y, x in zip(*np.nonzero(mask)):
            c = fn(x, y)
            if c is not None: self.rgb[y, x] = c; self.a[y, x] = 255; self.mat[y, x] = mat


BY0, BY1 = 31, 46                       # body rows (front face)
def lid_top(x, ybot, h):
    u = abs((x - CXA) / ((X1 - X0) / 2.0 + .5)); arch = max(0.0, 1 - u ** 3.2) ** .55      # full, round dome
    return ybot - h * (.28 + .72 * arch)

BANDS = ((X0 + 2, X0 + 5), (X1 - 5, X1 - 2))

def draw_body(cv):
    def f(x, y):
        lx = (x - X0) % 6
        t = {0: 2.6, 1: 0.3, 2: 0.9, 3: 1.0, 4: 1.2, 5: 1.8}[lx]
        if y >= BY0 + 2 and y <= BY0 + 3: t += 1.0
        if y >= BY1 - 2: t += 0.8
        if hsh(x, y, 5) < .04 and lx in (2, 3, 4): t += 1.0
        return pick(BODY, t)
    cv.paint((X >= X0) & (X <= X1) & (Y >= BY0) & (Y <= BY1), f, 1)

def draw_lid(cv, ybot, h, outer):
    inx = (X >= X0) & (X <= X1)
    top = np.array([lid_top(x, ybot, h) for x in range(A)])
    ctop = np.ceil(top).astype(int)
    region = inx & (Y >= ctop[None, :]) & (Y <= ybot - 1)
    if outer:
        def f(x, y):
            t = y - ctop[x]; fr = (y - top[x]) / max(1.0, ybot - top[x]); lx = (x - X0)
            tone = 0.9 + 1.6 * fr + 0.7 * (lx / float(X1 - X0)) ** 1.5
            if t < 1: tone = 0.0                                            # lit rim
            if 2 <= t <= 3 and 3 <= lx <= 16: tone = 0.0 if t == 2 else 0.5   # glossy shine
            if 5 <= t <= 6 and 3 <= lx <= 4: tone = 0.4
            if lx % 8 == 0 and t > 0: tone += 0.9                           # plank seams
            return pick(WOOD, tone)
        cv.paint(region, f, 1)
        for (a, b) in BANDS:                                                # steel bands that follow the dome
            def g(x, y):
                tone = {0: 0.0, 1: 0.8, 2: 1.7, 3: 2.6}[x - a]
                if y - ctop[x] < 1: tone = 0.0
                return pick(STEEL, tone)
            cv.paint(region & (X >= a) & (X <= b), g, 2)
            for k in (3, 6):
                cv.paint(region & (X == a + 1) & (Y == ctop[a + 1] + k), lambda x, y: OL, 2)
        cv.paint(inx & (Y >= ybot - 2) & (Y <= ybot - 1), lambda x, y: pick(STEEL, 0.0 if y == ybot - 2 else 1.1), 2)
    else:
        def f(x, y):
            t = y - ctop[x]; lx = (x - X0) % 6
            tone = {0: 3.4, 1: 2.4, 2: 2.6, 3: 2.7, 4: 2.8, 5: 3.0}[lx]
            if t < 1: tone = 1.6
            return pick(WOOD, tone)
        cv.paint(region, f, 1)
        for (a, b) in BANDS:
            cv.paint(region & (X >= a) & (X <= b), lambda x, y: pick(STEEL, 1.6 + (x - a) * .6), 2)
        cv.paint(inx & (Y >= ybot - 2) & (Y <= ybot - 1), lambda x, y: pick(STEEL, 2.2 if y == ybot - 2 else 3), 2)

def draw_straps(cv, ybot, opened):
    inx = (X >= X0) & (X <= X1)
    cv.paint(inx & (Y >= BY0) & (Y <= BY0 + 1), lambda x, y: pick(STEEL, 1.4 if y == BY0 else 2.6), 2)   # rim band under the lid
    for (a, b) in BANDS:                                                                                  # vertical bands on the body
        m = (X >= a) & (X <= b) & (Y >= BY0 + 2) & (Y <= BY1)
        cv.paint(m, lambda x, y: pick(STEEL, {0: 0.2, 1: 0.9, 2: 1.8, 3: 2.7}[x - a] + (y >= BY1 - 1) * 0.8), 2)
        for ry in (BY0 + 5, BY0 + 11):
            cv.paint((X == a + 1) & (Y == ry), lambda x, y: OL, 2)
    cv.paint(inx & (Y >= BY1 - 1) & (Y <= BY1), lambda x, y: pick(STEEL, 2.3 if y == BY1 - 1 else 3), 2)  # foot plate
    yb = BY0 if opened else ybot
    for left in (True, False):                                                                            # round hinge knobs at the sides
        xs = (X0 - 2, X0 - 1) if left else (X1 + 1, X1 + 2)
        for y in range(yb - 3, yb + 3):
            for x in xs:
                outer_x = (x == xs[0]) if left else (x == xs[1])
                if outer_x and y in (yb - 3, yb + 2): continue
                t = (0.3 if outer_x else 1.2) + (y - (yb - 3)) * 0.3
                cv.paint((X == x) & (Y == y), lambda xx, yy, t=t: pick(STEEL, t), 2)

def draw_lock(cv, opened):
    if not opened:
        for y in range(BY0 - 2, BY0 + 4):                                                                 # shackle
            cv.paint((X == 22) & (Y == y), lambda x, yy: pick(STEEL, 1.4), 2)
            cv.paint((X == 27) & (Y == y), lambda x, yy: pick(STEEL, 2.4), 2)
        cv.paint((X >= 23) & (X <= 26) & (Y == BY0 - 3), lambda x, y: pick(STEEL, 0.6), 2)
        cv.paint((X == 22) & (Y == BY0 - 3), lambda x, y: pick(STEEL, 1.2), 2); cv.paint((X == 27) & (Y == BY0 - 3), lambda x, y: pick(STEEL, 2.0), 2)
        m = (X >= 20) & (X <= 29) & (Y >= BY0 + 3) & (Y <= BY0 + 12)
        cv.paint(m, lambda x, y: pick(STEEL, 0.1 + (x - 20) * .22 + (y - BY0 - 3) * .16 + (x == 29) * .5 + (y == BY0 + 12) * .6), 2)
        cv.paint((X >= 20) & (X <= 29) & (Y == BY0 + 3), lambda x, y: pick(STEEL, 0.0), 2)
        cv.paint((X >= 24) & (X <= 25) & (Y >= BY0 + 6) & (Y <= BY0 + 9), lambda x, y: OL, 4)
        cv.paint((X >= 23) & (X <= 26) & (Y == BY0 + 6), lambda x, y: OL, 4)
    else:
        m = (X >= 22) & (X <= 27) & (Y >= BY0 + 3) & (Y <= BY0 + 10)
        cv.paint(m, lambda x, y: pick(STEEL, 0.3 + (x - 22) * .3 + (y - BY0 - 3) * .25 + (x == 27) * .5), 2)
        cv.paint((X >= 24) & (X <= 25) & (Y >= BY0 + 5) & (Y <= BY0 + 8), lambda x, y: OL, 4)

def draw_inside(cv, top_y, light):
    """an empty, dark chest; only light comes out of it (the game tints the glow on top)"""
    if top_y >= BY0: return
    inside = (X >= X0 + 3) & (X <= X1 - 3) & (Y >= top_y) & (Y <= BY0)
    def f(x, y):
        k = (y - top_y) / max(1.0, BY0 - top_y)
        base = np.array(INSIDE[0]) * (1 - k) + np.array(INSIDE[2]) * k
        g = light * 0.55 * (0.25 + 0.75 * k) * max(0.0, 1 - abs(x - CXA) / 15.0) ** 0.7
        return tuple(int(v) for v in np.clip(base * (1 - g) + np.array((255, 250, 235)) * g, 0, 255))
    cv.paint(inside, f, 4)
def finish(cv, shim=None, sparkles=()):
    img = np.dstack([cv.rgb, cv.a]).astype(np.uint8)
    if shim is not None:
        d = np.abs((X + Y * 0.9) - shim)
        sel = (cv.mat == 2) & (d < 2.4)
        f = np.clip(1 - d / 2.4, 0, 1)
        for c in range(3): img[..., c] = np.where(sel, np.clip(img[..., c] * (1 - f) + 255 * f, 0, 255), img[..., c]).astype(np.uint8)
    opaque = img[..., 3] == 255
    dil = np.zeros_like(opaque)
    dil[1:, :] |= opaque[:-1, :]; dil[:-1, :] |= opaque[1:, :]; dil[:, 1:] |= opaque[:, :-1]; dil[:, :-1] |= opaque[:, 1:]
    ol = dil & ~opaque
    img[ol, :3] = OL; img[ol, 3] = 255
    pil = Image.fromarray(img, "RGBA").copy(); px = pil.load()
    for (sx, sy, size) in sparkles:
        pts = [(0, 0, 3)] + [(i * dx, i * dy, 2 if i == 1 else 1) for i in range(1, size + 1) for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1))]
        for (dx, dy, lv) in pts:
            if 0 <= sx + dx < A and 0 <= sy + dy < A: px[sx + dx, sy + dy] = {3: (255, 255, 255, 255), 2: (235, 242, 255, 255), 1: (200, 214, 255, 235)}[lv]
    big = pil.resize((100, 100), Image.NEAREST)
    # soft ground shadow at full resolution
    arr = np.array(big)
    yy, xx = np.mgrid[0:100, 0:100]
    sh = np.clip(1 - (((xx - 49.5) / 41.0) ** 2 + ((yy - 95.5) / 4.0) ** 2), 0, 1) ** .7 * 140
    free = arr[..., 3] == 0
    arr[free, 0] = 18; arr[free, 1] = 10; arr[free, 2] = 24; arr[free, 3] = sh[free].astype(np.uint8)
    return Image.fromarray(arr, "RGBA")

def chest(lid=(31, 19), inside_top=None, light=0.0, opened=False, outer=True, shim=None, sparkles=()):
    cv = Cv()
    if inside_top is not None: draw_inside(cv, inside_top, light)
    draw_body(cv)
    ybot, h = lid
    draw_lid(cv, ybot, h, outer)
    draw_straps(cv, ybot, opened)
    draw_lock(cv, opened)
    return finish(cv, shim, sparkles)

SPARK = {1: [(15, 21, 1)], 2: [(15, 21, 2), (24, 31, 1)], 3: [(15, 21, 1), (24, 31, 2)], 4: [(35, 19, 1), (24, 31, 1)], 5: [(35, 19, 2)], 6: [(35, 19, 1)]}
IDLE = [chest(lid=(31, 19), shim=(-14 + k * 14) if k < 7 else None, sparkles=SPARK.get(k, ())) for k in range(8)]
OPEN_SPEC = [dict(lid=(31, 19), top=None, outer=True, light=0), dict(lid=(29, 18), top=29, outer=True, light=1.0), dict(lid=(26, 16), top=26, outer=True, light=1.0),
             dict(lid=(23, 13), top=23, outer=True, light=1.0), dict(lid=(26, 5), top=26, outer=False, light=1.0), dict(lid=(26, 12), top=26, outer=False, light=.9),
             dict(lid=(26, 22), top=26, outer=False, light=.8), dict(lid=(26, 20), top=26, outer=False, light=.8)]
OPEN = [chest(lid=s["lid"], inside_top=s["top"], light=s["light"], opened=(i >= 4), outer=s["outer"]) for i, s in enumerate(OPEN_SPEC)]

# ------------------------------------------------------------------ glow: white, tinted by the game. 200 x 200, light origin at (100, 106)
GW = 200
GY, GX = np.mgrid[0:GW, 0:GW]
EDGE = np.clip(np.minimum.reduce([GX, GW - 1 - GX, GY, GW - 1 - GY]) / 28.0, 0, 1) ** 1.3
def glow(k, n=8):
    ph = 2 * math.pi * k / n
    ox, oy = 100.0, 106.0
    dx = GX - ox; dy = oy - GY; dist = np.hypot(dx, dy) + 1e-6; ang = np.arctan2(dx, dy)
    beams = np.zeros((GW, GW))
    for i, (a0, w) in enumerate(((-0.62, .07), (-0.42, .085), (-0.2, .07), (0.0, .11), (0.2, .07), (0.42, .085), (0.62, .07))):
        amp = 0.72 + 0.28 * math.sin(ph * 2 + i * 1.3)
        beams += amp * np.exp(-((ang - (a0 + .03 * math.sin(ph + i))) / w) ** 2)
    beams *= np.exp(-dist / 96.0) * (dy > -4)
    core = np.clip(1 - np.hypot(dx / 32.0, (oy - 6 - GY) / 24.0), 0, 1) ** 1.6 * (0.92 + 0.08 * math.sin(ph))
    bloom = np.clip(1 - np.hypot(dx / 74.0, (oy - 34 - GY) / 62.0), 0, 1) ** 2 * .6
    pool = np.clip(1 - np.hypot(dx / 78.0, (GY - 112) / 12.0), 0, 1) ** 1.4 * .5
    al = np.clip(beams * .72 + core + bloom + pool, 0, 1) * EDGE
    img = np.zeros((GW, GW, 4), np.uint8); img[..., :3] = 255; img[..., 3] = (al * 255).astype(np.uint8)
    pil = Image.fromarray(img, "RGBA").copy(); p = pil.load()
    for i in range(9):                                                        # rising motes of light
        t = (k / n + i / 9.0) % 1.0
        sx = 100 + math.sin(i * 2.4) * 36 + math.sin(t * 6 + i) * 7; sy = 98 - t * 84
        life = math.sin(t * math.pi)
        if life < .12: continue
        r = 2.4 if life > .55 else 1.5
        for yy in range(int(sy - 3), int(sy + 4)):
            for xx in range(int(sx - 3), int(sx + 4)):
                d = math.hypot(xx - sx, yy - sy)
                if d <= r + .6 and 0 <= xx < GW and 0 <= yy < GW:
                    a = int(255 * life * (1 if d <= r - .4 else .45)); old = p[xx, yy][3]
                    p[xx, yy] = (255, 255, 255, max(old, a))
    return pil
GLOW = [glow(k) for k in range(8)]
def burst(k, n=5):
    t = k / (n - 1.0); ox, oy = 100.0, 106.0
    dx = GX - ox; dy = oy - GY; dist = np.hypot(dx, dy) + 1e-6; ang = np.arctan2(dx, dy)
    ring = np.exp(-((dist - 16 - t * 78) / (6 + t * 7)) ** 2) * (1 - t) ** .8
    star = sum(np.exp(-((ang - (i - 4.5) * .3) / .055) ** 2) for i in range(10)) * np.exp(-dist / (50 + 34 * t)) * (dy > -4) * (1 - t) ** .6
    core = np.clip(1 - dist / (34 + t * 18), 0, 1) ** 2 * (1 - t) * 1.2
    al = np.clip(ring + star * .9 + core, 0, 1) * EDGE
    img = np.zeros((GW, GW, 4), np.uint8); img[..., :3] = 255; img[..., 3] = (al * 255).astype(np.uint8)
    return Image.fromarray(img, "RGBA")
BURST = [burst(k) for k in range(5)]

for sub in (f"{TYPE}/Idle", f"{TYPE}/Open", "Glow", "Burst"): os.makedirs(f"{OUT}/{sub}", exist_ok=True)
for i, im in enumerate(IDLE): im.save(f"{OUT}/{TYPE}/Idle/Chest_{TYPE}_Idle_{i+1:02d}.png")
for i, im in enumerate(OPEN): im.save(f"{OUT}/{TYPE}/Open/Chest_{TYPE}_Open_{i+1:02d}.png")
for i, im in enumerate(GLOW): im.save(f"{OUT}/Glow/Chest_Glow_{i+1:02d}.png")
for i, im in enumerate(BURST): im.save(f"{OUT}/Burst/Chest_Burst_{i+1:02d}.png")
for name, lst in (("Idle", IDLE), ("Open", OPEN)):
    sh = Image.new("RGBA", (100 * len(lst), 100), (0, 0, 0, 0))
    for i, im in enumerate(lst): sh.paste(im, (i * 100, 0))
    sh.save(f"{OUT}/{TYPE}/Chest_{TYPE}_{name}_Sheet.png")
print("ok")
