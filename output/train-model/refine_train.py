import bpy, bmesh, json
from mathutils import Vector
col = bpy.data.collections['Train Working Model']
assert not bpy.data.collections.get('Train Export'), 'Already refined'
excluded = bpy.data.collections.new('Interactive reference - excluded from export')
bpy.context.scene.collection.children.link(excluded)
for o in list(col.objects):
    p = o.get('unity_path','')
    if p.startswith('RadioTable/') or (p.startswith('CockpitControls/') and o.name not in ('Integrated housing','Dash trim')):
        excluded.objects.link(o)
        col.objects.unlink(o)
excluded.hide_viewport = True
excluded.hide_render = True

# Keep only the structural brow and brackets in trim. Exclude gauge geometry
# and permanently omit both decorative dash light strips from the revision.
o = col.objects['Dash trim']
bm = bmesh.new(); bm.from_mesh(o.data)
remove = [f for f in bm.faces if f.calc_center_median().y < 4.65]
bmesh.ops.delete(bm, geom=remove, context=next(i.identifier for i in bpy.ops.mesh.delete.get_rna_type().properties['type'].enum_items if i.identifier=='FACE')) if False else None
bmesh.ops.delete(bm, geom=remove, context='FACES')
bm.to_mesh(o.data); bm.free(); o.data.update()

# The forward sightline is protected: grow the aperture downwards 25%, with
# ceiling/nose upper surface unchanged. No gameplay anchors are changed here.
for o in col.objects:
    if o.get('unity_path','').startswith('Furnace/') and not any(s in o.name for s in ('Stack','exhaust')):
        for v in o.data.vertices:
            if v.co.z < .35:
                v.co.z = .35 + (v.co.z-.35)*1.25
        # Reproject repeating furnace textures with square 32 texels/metre.
        uv=o.data.uv_layers.active
        o.data.update()
        for p in o.data.polygons:
            n=p.normal
            u=Vector((1,0,0)) if abs(n.z)>.99 else Vector((0,0,1)).cross(n).normalized()
            w=n.cross(u)
            for li in p.loop_indices:
                co=o.data.vertices[o.data.loops[li].vertex_index].co
                uv.data[li].uv=(co.dot(u)*.5,co.dot(w)*.5)

# Half-width hollow exhaust, same route and height; extend the duct to meet it.
for o in col.objects:
    if o.name.startswith('Stack'):
        for v in o.data.vertices:
            v.co.x=2.16+(v.co.x-2.16)*.5
            v.co.y=5.26+(v.co.y-5.26)*.5
    elif o.name == 'Low exhaust duct':
        for v in o.data.vertices:
            v.co.x=.98+(v.co.x-.98)*(2.16-.98)/.9
            v.co.y=5.26+(v.co.y-5.31)*.5
            v.co.z=.26+(v.co.z-.26)*.5

# A fitted cowl bridges the dash to the firebox and side cheeks. Its top
# meets the existing ceiling at 0.43 m, below the 0.5 m reference eye line.
outline=[(-1.65,4.77,.354),(1.65,4.77,.354),(1.65,4.88,.43),(1.08,4.88,.43),(1.08,5.08,.43),(-1.08,5.08,.43),(-1.08,4.88,.43),(-1.65,4.88,.43)]
verts=outline+[(x,y,z-.07) for x,y,z in outline]
faces=[(0,1,2,3,6,7),(3,4,5,6),(15,14,11,10,9,8),(14,13,12,11)]
for i in range(8):
    j=(i+1)%8
    faces.append((i,i+8,j+8,j))
mesh=bpy.data.meshes.new('Fitted cowl');mesh.from_pydata(verts,[],faces);mesh.update()
o=bpy.data.objects.new('Dash to furnace cowl',mesh);col.objects.link(o)
mesh.materials.append(col.objects['Chamber ceiling'].data.materials[0])
uv=mesh.uv_layers.new(name='UVMap')
for p in mesh.polygons:
    n=p.normal;u=Vector((1,0,0)) if abs(n.z)>.99 else Vector((0,0,1)).cross(n).normalized();w=n.cross(u)
    for li in p.loop_indices:
        co=mesh.vertices[mesh.loops[li].vertex_index].co
        uv.data[li].uv=(co.dot(u)*.5,co.dot(w)*.5)

# Keep individual authoring parts hidden; exported geometry has three sections.
export=bpy.data.collections.new('Train Export');bpy.context.scene.collection.children.link(export)
groups={'Train Body':[], 'Canopy and spotlight':[], 'Wheels':[]}
for o in col.objects:
    group='Wheels' if o.name=='Flanged wheels' else 'Canopy and spotlight' if o.name in ('Faceted canopy','Large roof spotlight') else 'Train Body'
    c=o.copy();c.data=o.data.copy();export.objects.link(c);groups[group].append(c)
for name,objects in groups.items():
    for o in bpy.context.selected_objects:o.select_set(False)
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    o=bpy.context.object;o.name=name
    for key in list(o.keys()):del o[key]
    o['source_paths']=json.dumps([p.get('unity_path',p.name) for p in col.objects if ('Wheels' if p.name=='Flanged wheels' else 'Canopy and spotlight' if p.name in ('Faceted canopy','Large roof spotlight') else 'Train Body')==name])
    bm=bmesh.new();bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    bm.to_mesh(o.data);bm.free();o.data.update()
    print(name,len(o.data.vertices),'vertices',sum(len(p.vertices)-2 for p in o.data.polygons),'triangles',len(o.data.materials),'materials')
col.hide_viewport=True;col.hide_render=True
for o in bpy.context.selected_objects:o.select_set(False)
print('Completed revised model; controls/radio excluded, dash light strips removed.')
