import bpy
import json
from collections import deque

def mesh_islands(o):
    """Return island list: each island = {'polys':[[(u,v),...],...], 'z_min','z_max','tris'}."""
    me = o.data
    uv = me.uv_layers.active
    if uv is None:
        return []
    polys = me.polygons
    n = len(polys)
    # polygon UV loops
    puv_polys = []
    pz = []
    for p in polys:
        verts = []
        sy = 0.0
        for li in range(p.loop_start, p.loop_start + p.loop_total):
            verts.append((uv.data[li].uv.x, uv.data[li].uv.y))
            sy += me.vertices[p.vertices[li - p.loop_start]].co.z
        puv_polys.append(verts)
        pz.append(sy / max(1, len(p.vertices)))
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
    out = []
    for comp in comps:
        zz = [pz[i] for i in comp]
        out.append({
            'tris': len(comp),
            'z_min': min(zz),
            'z_max': max(zz),
            'polys': [puv_polys[i] for i in comp],
        })
    return out

result = {}
for name in ['Soldier_LegL', 'Soldier_LegR', 'Soldier_Chest']:
    o = bpy.data.objects.get(name)
    if o and o.type == 'MESH':
        result[name] = mesh_islands(o)
    else:
        result[name] = None

with open(r'D:\prounity\mvp\mvp\Tools\_uv_polys.json', 'w', encoding='utf-8') as f:
    json.dump(result, f)
print('WROTE uv_polys.json', {k: (len(v) if v else None) for k, v in result.items()})
