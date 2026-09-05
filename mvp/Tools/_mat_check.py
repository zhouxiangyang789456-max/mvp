import bpy

names = ['Soldier_TorsoL', 'Soldier_LegL', 'Soldier_Head', 'Soldier_Helmet',
         'Soldier_FootR', 'Soldier_ArmL', 'Soldier_Chest', 'Infantry_Rifle']
for name in names:
    o = bpy.data.objects.get(name)
    if not o or o.type != 'MESH':
        continue
    mats = o.data.materials
    for mi, m in enumerate(mats):
        if m is None:
            continue
        met = None
        rough = None
        base = None
        if m.node_tree:
            for n in m.node_tree.nodes:
                if n.type == 'BSDF_PRINCIPLED':
                    met = n.inputs['Metallic'].default_value
                    rough = n.inputs['Roughness'].default_value
                    base = n.inputs['Base Color'].default_value
                    break
        print('%s[%d] mat=%s metallic=%s roughness=%s base=%s'
              % (name, mi, m.name, met, rough, base))
