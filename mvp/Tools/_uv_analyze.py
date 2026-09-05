import bpy

for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    me = o.data
    uv = me.uv_layers.active
    img = None
    if o.active_material and o.active_material.node_tree:
        for n in o.active_material.node_tree.nodes:
            if n.type == 'TEX_IMAGE':
                img = n.image.name if n.image else None
                break
    print(o.name, '| tris', len(me.polygons), '| uv', (uv.name if uv else None), '| img', img)
