const fs = require('fs'), cp = require('child_process'), assert = require('assert');
for (const name of ['bridge-finale','farm-silos','skyscraper-works','convenience-store','roadside-motel','container-yard']) {
 const path = `Assets/DuckRails/Blocks/${name}-v1.prefab`;
 const old = cp.execFileSync('git',['show',`HEAD:${path}`],{maxBuffer:50*1024*1024}).toString().replace(/\r\n/g,'\n');
 const current = fs.readFileSync(path,'utf8').replace(/\r\n/g,'\n');
 const parts = s => s.split(/(?=^--- !u!)/m), key = s => s.split('\n')[0], norm = s => s.replace(/[ \t]+$/gm,'');
 const before = new Map(parts(old).slice(1).map(s=>[key(s),s])); let changed=0;
 const after = parts(current).map((s,i)=> {
  if(!i)return s;
  const previous=before.get(key(s));
  if(previous===undefined)return norm(s);
  before.delete(key(s));
  if(norm(s)===norm(previous))return previous;
  const added=s.split('\n').filter(l=>!previous.split('\n').includes(l));
  assert(added.length===1 && /^  - \{fileID:/.test(added[0]),name+' unexpected change');
  assert(s.replace(added[0]+'\n','')===previous,name+' existing fields changed');changed++;return s;
 });
 assert(before.size===0 && changed===1,name+' preservation failed');
 fs.writeFileSync(path,after.join(''));console.log(name+': all existing objects preserved; only root child added');
}
