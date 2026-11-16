import re,json
from pathlib import Path
p=Path('X:/Work/duck-rails');base=p/'ArtSource/FarmSilosPolish'
def records(path):
 s=path.read_text();return {m.group(2):(int(m.group(1)),m.group(3)) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)',s,re.M|re.S)}
a=records(base/'before.prefab');b=records(p/'Assets/DuckRails/Blocks/farm-silos-v1.prefab')
def name(records,record):
 q=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',record)
 if not q:return ''
 g=records.get(q.group(1),(0,''))[1];n=re.search(r'  m_Name: (.*)',g);return n.group(1) if n else ''
changed=[];markers=0;missing=[]
for key,(typ,body) in a.items():
 if typ!=4:continue
 n=name(a,body)
 if re.match(r'^(Coal|Gold|Enemy) ',n):
  assert key in b and b[key]==a[key],('Changed spawn transform',key,n);markers+=1
 if key in b and b[key]!=a[key]:changed.append(dict(id=key,name=n))
 if key not in b:missing.append(dict(id=key,name=n))
report=dict(status='PASS',unchanged_spawn_group_transforms=markers,changed_original_transforms=changed,removed_transforms=missing)
(base/'identity-checks.json').write_text(json.dumps(report,indent=2));print('Spawn transforms unchanged:',markers,'changed transforms:',changed,'removed transforms:',len(missing))

