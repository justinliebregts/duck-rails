import bpy,json,numpy as np
from pathlib import Path
p=Path('X:/Work/duck-rails/docs/validation/farm-silos-polish-2026-11-15'); out=[]
for name in ['slide','ramps','silo','shed','west-lanterns','barn-south','path','pond']:
 a=bpy.data.images.load(str(p/('source-'+name+'.png')));b=bpy.data.images.load(str(p/('atlas-'+name+'.png')))
 x=np.array(a.pixels[:]).reshape(-1,4)[:,:3]*255;y=np.array(b.pixels[:]).reshape(-1,4)[:,:3]*255;d=np.abs(x-y)
 out.append(dict(view=name,mean_rgb_byte_difference=float(d.mean()),pixels_over_8_percent=float((d.mean(axis=1)>8).mean()*100)))
(p/'visual-comparison.json').write_text(json.dumps(out,indent=2));print(json.dumps(out))
