"""Export metre-scale, flat-normal geometry in RadioTable-local coordinates."""
import bpy,json,os
OUT='X:/Work/duck-rails/Assets/DuckRails/Train/RadioConsole'
os.makedirs(OUT,exist_ok=True)
parts=[]
for o in bpy.data.collections['PA Console - export'].objects:
    o.data.calc_loop_triangles();vs=[];uv=[];indices=[]
    for tri in o.data.loop_triangles:
        # Mapping changes handedness; reverse winding to retain outward normals.
        for li in reversed(tri.loops):
            p=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co
            vs.append(dict(x=.09+p.y,y=p.z,z=-.1-p.x))
            t=o.data.uv_layers.active.data[li].uv;uv.append(dict(x=t.x,y=t.y));indices.append(len(indices))
    parts.append(dict(name=o.name,vertices=vs,uv=uv,triangles=indices))
with open(OUT+'/geometry.json','w') as f:json.dump(dict(parts=parts),f)
print('Exported',len(parts),'parts',sum(len(p['triangles'])//3 for p in parts),'triangles')
