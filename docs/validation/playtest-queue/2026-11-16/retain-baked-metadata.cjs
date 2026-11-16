const fs=require('fs'), path=require('path'), assert=require('assert');
const root='X:/Work/duck-rails', guid='593af95ad35fbc3498d4f46c01f1ca1b';
const changes=[], lines=[];
for(const asset of fs.readFileSync(path.join(root,'Temp/queue-baked-prefab-paths.txt'),'utf8').replace(/^\uFEFF/,'').trim().split(/\r?\n/)) {
  const original=fs.readFileSync(path.join(root,'Temp/queue-baked-metadata-before',asset),'utf8');
  const saved=fs.readFileSync(path.join(root,asset),'utf8');
  const docs=s=>new Map([...s.matchAll(/^--- !u!.*?(?=^--- !u!|(?![\s\S]))/gms)].map(m=>[m[0].split(/\r?\n/)[0],m[0]]));
  const oldDocs=docs(original), newDocs=docs(saved), newline=original.includes('\r\n')?'\r\n':'\n';
  let result=original;const additions=[],stripped=[];
  for(const [key,doc] of oldDocs) if(doc.includes(guid)) {
    const match=newDocs.get(key).match(/^  bakedFixtures:[^\r\n]*(?:\r?\n  -[^\r\n]*)*/m);
    assert(match,asset);assert(!doc.includes('  bakedFixtures:'),asset);
    const field=match[0].replace(/\r?\n/g,newline)+newline;
    result=result.replace(doc,doc+field);additions.push(field);
  }
  for(const [key,doc] of newDocs) if(!oldDocs.has(key)) {
    assert(/^--- !u!108 &\d+ stripped$/.test(key),asset+' unexpected new document '+key);
    stripped.push(doc.replace(/\r?\n/g,newline));
  }
  result+=stripped.join('');let reversed=result;
  for(const addition of [...additions,...stripped]) {assert.equal(reversed.split(addition).length,2,asset);reversed=reversed.replace(addition,'');}
  assert.equal(reversed,original,asset+' prior bytes differ');
  changes.push([path.join(root,asset),result]);
  lines.push(`PASS ${asset}: metadata fields=${additions.length}, new stripped Light references=${stripped.length}, all prior bytes preserved`);
}
for(const [file,content] of changes)fs.writeFileSync(file,content);
fs.writeFileSync(path.join(root,'docs/validation/playtest-queue/2026-11-16/baked-metadata-preservation.txt'),lines.join('\n')+'\n');
console.log(lines.join('\n'));
