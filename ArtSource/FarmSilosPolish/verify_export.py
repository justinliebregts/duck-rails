"""Independent comparison of exported triangle corners with the approved Unity snapshot. Run in Blender (numpy)."""
import json
from pathlib import Path
import numpy as np
base=Path('X:/Work/duck-rails/ArtSource/FarmSilosPolish')
source=json.loads((base/'source.json').read_text()); output=json.loads((base/'unity-meshes.json').read_text()); manifest=json.loads((base/'manifest.json').read_text())
parts=json.loads((base/'part-checks.json').read_text()); bykey={p['sourceKey']:p for p in parts}; groups={}
for part in source['parts']:
    v=np.array(part['vertices']);mn=v.min(axis=0);mx=v.max(axis=0);size=mx-mn
    cell=('large',part['sourceKey']) if max(size[0],size[2])>20 or size[1]>12 else tuple(int(x) for x in np.floor((mn+mx)*.5/[12,4,12]))
    mi=part['material']; atlas=bykey[part['sourceKey']]['atlas'];key=(str(cell),part['lightmap'],part['settings'],'' if atlas else source['materials'][mi]['assetPath'])
    groups.setdefault(key,[]).append(part)
assert len(groups)==len(output['parts'])
maximum=dict(position=0.,normal=0.,lightmap_uv=0.,repeat_phase=0.)
count=0; cubes=0;area_before=0.;area_after=0.
def array(o,key,dims):return np.array([[v[a] for a in dims] for v in o[key]])
for (key,input_parts),out in zip(groups.items(),output['parts']):
    assert (out['lightmap'],out['settings'],out['materialPath'])==key[1:]
    ix=np.array(out['triangles']);v=array(out,'vertices','xyz')[ix];n=array(out,'normals','xyz')[ix];u=array(out,'uv','xy')[ix];l=array(out,'uv2','xy')[ix];cursor=0
    for part in input_parts:
        ids=part['triangles'];length=len(ids);end=cursor+length;m=source['materials'][part['material']]
        srcv=np.array(part['vertices'])[ids];srcn=np.array(part['normals'])[ids];srcl=np.array(part['uv2'])[ids]*part['lightmapST'][:2]+part['lightmapST'][2:]
        srcu=np.array(part['uv'])[ids]*m['scale']+m['offset'];actualu=u[cursor:end]
        if not out['materialPath']:
            tx,ty,w,h=manifest['tiles'][part['material']];ru,rv=manifest['repeats'][m['name']]
            actualu=(actualu*manifest['atlas_size']-[tx,ty])/[w/ru,h/rv]
            assert (actualu>=-2e-5).all() and (actualu<=[ru+2e-5,rv+2e-5]).all()
        else:actualu=actualu*m['scale']+m['offset']
        delta=(srcu-actualu).reshape(-1,3,2);integer=np.rint(delta[:,0,:])[:,None,:]
        errors=dict(position=np.max(np.abs(srcv-v[cursor:end])),normal=np.max(np.abs(srcn-n[cursor:end])),lightmap_uv=np.max(np.abs(srcl-l[cursor:end])),repeat_phase=np.max(np.abs(delta-integer)))
        for k,e in errors.items():maximum[k]=max(maximum[k],float(e))
        assert errors['position']<2e-5 and errors['normal']<2e-6 and errors['lightmap_uv']<2e-6 and errors['repeat_phase']<5e-5,(part['path'],errors)
        for a,which in [(srcv,'source'),(v[cursor:end],'output')]:
            tris=a.reshape(-1,3,3);area=np.linalg.norm(np.cross(tris[:,1]-tris[:,0],tris[:,2]-tris[:,0]),axis=1).sum()*.5
            if which=='source':area_before+=area
            else:area_after+=area
        cubes+=length==36;count+=length//3;cursor=end
    assert cursor==len(ix)
assert count==sum(len(p['triangles'])//3 for p in source['parts'])
report=dict(status='PASS',source_parts=len(parts),twelve_triangle_parts_preserved=int(cubes),triangles_compared=count,chunks=len(groups),max_corner_errors=maximum,source_surface_area=float(area_before),export_surface_area=float(area_after),relative_area_error=float(abs(area_before-area_after)/area_before),checks='Every original triangle corner retained in order with original winding, normal, lightmap UV, renderer settings and lightmap identity. Texture repeat phase differs only by one constant integer offset per triangle; UV gradients preserved. No generated cuts or omitted triangles.')
(base/'geometry-checks.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))



