import bpy
from collections import deque

def island_analysis(o):
    me = o.data
    uv = me.uv_layers.active
    if uv is None:
        return {'name': o.name, 'error': 'no uv'}
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
        rows.append({
            'tris': len(comp),
            'uv_min_x': min(uu), 'uv_max_x': max(uu),
            'uv_min_y': min(vv), 'uv_max_y': max(vv),
            'cx': (min(uu) + max(uu)) / 2,
            'cy': (min(vv) + max(vv)) / 2,
            'world_z_min': min(yy), 'world_z_max': max(yy),
        })
    rows.sort(key=lambda r: -r['tris'])
    return {'name': o.name, 'islands': rows[:12]}

out = []
for name in ['Soldier_LegL', 'Soldier_LegR', 'Soldier_Waist', 'Soldier_Pelvis',
             'Soldier_Chest', 'Soldier_TorsoL', 'Soldier_FootR', 'Soldier_Helmet',
             'Soldier_ArmL', 'Soldier_Head']:
    o = bpy.data.objects.get(name)
    if o and o.type == 'MESH':
        out.append(island_analysis(o))
    else:
        out.append({'name': name, 'error': 'not found'})
import json as _json
with open(r'D:\prounity\mvp\mvp\Tools\_islands.json', 'w', encoding='utf-8') as f:
    _json.dump(out, f)
print('WROTE islands.json')
