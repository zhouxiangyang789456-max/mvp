import bpy
import os
from collections import deque

BACKUP = r'D:\prounity\mvp\mvp\Tools\_backup_infantry_textures'
_img_cache = {}

def load_img(name):
    if name in _img_cache:
        return _img_cache[name]
    p = os.path.join(BACKUP, name)
    if not os.path.exists(p):
        _img_cache[name] = None
        return None
    img = bpy.data.images.load(p)
    _img_cache[name] = img
    return img

def sample(name, u, v):
    img = load_img(name)
    if img is None:
        return None
    w = img.size[0]
    h = img.size[1]
    if w <= 1 or h <= 1:
        return None
    x = max(0, min(w - 1, int((u % 1.0) * w)))
    y = max(0, min(h - 1, int((v % 1.0) * h)))
    px = img.pixels
    i = (y * w + x) * 4
    return (int(px[i] * 255), int(px[i + 1] * 255), int(px[i + 2] * 255))

def island_analysis(o):
    me = o.data
    uv = me.uv_layers.active
    if uv is None:
        print(o.name, 'no uv')
        return
    polys = me.polygons
    n = len(polys)
    puv = []
    for p in polys:
        u = 0.0
        v = 0.0
        for li in range(p.loop_start, p.loop_start + p.loop_total):
            u += uv.data[li].uv.x
            v += uv.data[li].uv.y
        nv = max(1, p.loop_total)
        puv.append((u / nv, v / nv))
    py = []
    for p in polys:
        sy = 0.0
        for v_idx in p.vertices:
            sy += me.vertices[v_idx].co.z
        py.append(sy / max(1, len(p.vertices)))
    edge_map = {}
    for p in polys:
        for e in p.edge_keys:
            edge_map.setdefault(e, []).append(p.index)
    adj = {i: [] for i in range(n)}
    for e, plist in edge_map.items():
        for i in range(len(plist)):
            for j in range(i + 1, len(plist)):
                adj[plist[i]].append(plist[j])
                adj[plist[j]].append(plist[i])
    visited = [False] * n
    comps = []
    for start in range(n):
        if visited[start]:
            continue
        q = deque([start])
        visited[start] = True
        comp = []
        while q:
            cur = q.popleft()
            comp.append(cur)
            for nb in adj[cur]:
                if not visited[nb]:
                    visited[nb] = True
                    q.append(nb)
        comps.append(comp)
    rows = []
    for comp in comps:
        uu = [puv[i][0] for i in comp]
        vv = [puv[i][1] for i in comp]
        yy = [py[i] for i in comp]
        cu = sum(uu) / len(uu)
        cv = sum(vv) / len(vv)
        col = sample(o.name + '_BaseColor.png', cu, cv)
        colstr = 'None'
        if col:
            colstr = '(%d,%d,%d)' % col
        rows.append((len(comp), min(uu), max(uu), min(vv), max(vv),
                     min(yy), max(yy), colstr))
    rows.sort(key=lambda r: -r[0])
    print('=== %s (%d polys, %d islands) ===' % (o.name, n, len(comps)))
    for r in rows[:10]:
        print('  tris %4d | uvb x=%s-%s y=%s-%s | worldZ [%.2f..%.2f] | center(%.3f,%.3f) col=%s'
              % (r[0], '%.2f' % r[1], '%.2f' % r[2], '%.2f' % r[3], '%.2f' % r[4],
                 r[5], r[6], (r[1] + r[2]) / 2, (r[3] + r[4]) / 2, r[7]))

for name in ['Soldier_LegL', 'Soldier_LegR', 'Soldier_Waist', 'Soldier_Pelvis',
             'Soldier_Chest', 'Soldier_TorsoL']:
    o = bpy.data.objects.get(name)
    if o and o.type == 'MESH':
        island_analysis(o)
