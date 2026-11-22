"""Run in a fresh Blender background process; never opens or replaces a user scene."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/duckz')
OUT=ROOT/'Tools/LIVCamera'
ASSETS=ROOT/'Assets/Models/LIVCamera'
assert Path.cwd().resolve()==ROOT.resolve()
OUT.mkdir(exist_ok=True); ASSETS.mkdir(parents=True,exist_ok=True)
# This generator is for new output only; deliberate regeneration requires --replace.
import sys
for p in [OUT/'RemoteLIVCamera.blend',ASSETS/'RemoteLIVCamera.fbx',ASSETS/'RemoteLIVCameraAtlas.png']:
 if p.exists() and '--replace' not in sys.argv: raise RuntimeError('Refusing to replace '+str(p))
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
# One tiny atlas: casing, handles, screen, and back label share one material.
W=128; pixels=[0.0]*(W*W*4)
def rect(x0,y0,x1,y1,c):
 for y in range(y0,y1):
  for x in range(x0,x1): pixels[(y*W+x)*4:(y*W+x)*4+4]=[*c,1]
# Match Assets/Materials/LIVCamera.mat exactly, with a dark static screen.
orange=(1,.4776187,0); ink=(.55,.72,.75); screen=(.018,.033,.043)
rect(0,0,128,128,orange)
rect(0,32,128,128,screen)
rect(27,57,77,103,ink);rect(33,63,71,97,screen)
for x in range(79,103):
 h=int((x-79)*.65)+7;rect(x,80-h,x+1,81+h,ink)
img=bpy.data.images.new('RemoteLIVCameraAtlas',width=W,height=W,alpha=False)
img.pixels.foreach_set(pixels);img.filepath_raw=str(ASSETS/'RemoteLIVCameraAtlas.png');img.file_format='PNG';img.save()
mat=bpy.data.materials.new('RemoteLIVCamera');mat.use_nodes=True
bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.85
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Closest';mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
verts=[];faces=[];uvs=[]
def face(vs,uv=(.8,.12)):
 i=len(verts);verts.extend(vs);faces.append(tuple(range(i,i+len(vs))));uvs.append([uv]*len(vs) if isinstance(uv[0],float) else uv)
def box(x0,x1,y0,y1,z0,z1):
 p=[(x0,y0,z0),(x1,y0,z0),(x1,y0,z1),(x0,y0,z1),(x0,y1,z0),(x1,y1,z0),(x1,y1,z1),(x0,y1,z1)]
 for ids in [(0,1,2,3),(5,4,7,6),(4,0,3,7),(1,5,6,2),(3,2,6,7),(4,5,1,0)]:face([p[i] for i in ids])
# Blender upright; front is -Y. 53.2 cm wide, 31 cm tall including handles.
box(-.218,.218,-.015,.035,-.076,.234)
def handle(x0,x1):
 outer=[(x0,0),(x1,0),(x1,.234),(x0,.234)]
 innerX=(.216378,.236378) if x0>0 else (-.236378,-.216378)
 inner=[(innerX[0],.024994),(innerX[1],.024994),(innerX[1],.208995),(innerX[0],.208995)]
 def depth(x,front):
  x=abs(x)
  if x<=.236378:
   t=max(0.,(x-.216378)/.02)
   return .005325173*t if front else .05-(.05-.045145672)*t
  t=(x-.236378)/(.266006-.236378)
  return .005325173+(.0125-.005325173)*t if front else .045145672+(.0375-.045145672)*t
 for i in range(4):
  j=(i+1)%4
  def p(r,k,y):return (r[k][0],depth(r[k][0],y<0),r[k][1])
  face([p(outer,i,-.01),p(outer,j,-.01),p(inner,j,-.01),p(inner,i,-.01)],(.38,.12))
  face([p(inner,i,.03),p(inner,j,.03),p(outer,j,.03),p(outer,i,.03)],(.38,.12))
  face([p(outer,j,-.01),p(outer,i,-.01),p(outer,i,.03),p(outer,j,.03)],(.38,.12))
  face([p(inner,i,-.01),p(inner,j,-.01),p(inner,j,.03),p(inner,i,.03)],(.14,.12))
handle(-.266006,-.212);handle(.212,.266006)
face([(-.201,-.0155,-.047),(.201,-.0155,-.047),(.201,-.0155,.218),(-.201,-.0155,.218)],[(.01,.26),(.99,.26),(.99,.99),(.01,.99)])
face([(.09,.0355,.025),(-.09,.0355,.025),(-.09,.0355,.13),(.09,.0355,.13)],[(.25,.36),(.75,.36),(.75,.91),(.25,.91)])
mesh=bpy.data.meshes.new('RemoteLIVCamera');mesh.from_pydata(verts,[],faces);mesh.update()
o=bpy.data.objects.new('RemoteLIVCamera',mesh);bpy.context.collection.objects.link(o);mesh.materials.append(mat)
uv=mesh.uv_layers.new(name='Atlas')
for poly,coords in zip(mesh.polygons,uvs):
 for li,co in zip(poly.loop_indices,coords):uv.data[li].uv=co
# Recalculate outward surface normals without merging the intentional screen/label planes.
bpy.context.view_layer.objects.active=o;o.select_set(True)
mesh.calc_loop_triangles(); print('MODEL_TRIANGLES',len(mesh.loop_triangles))
bpy.ops.export_scene.fbx(filepath=str(ASSETS/'RemoteLIVCamera.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
# Preview stage is stored only in Tools, never exported into the game model.
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=24
s.world.color=(.18,.18,.18)
cam=bpy.data.cameras.new('PreviewCamera');c=bpy.data.objects.new('PreviewCamera',cam);s.collection.objects.link(c);s.camera=c
c.location=(.65,-1.1,.58);c.rotation_euler=(Vector((0,0,.075))-c.location).to_track_quat('-Z','Y').to_euler();cam.type='ORTHO';cam.ortho_scale=.79
for name,loc,power,size in [('Key',(-.5,-.6,1.1),45,.7),('Fill',(.7,-.2,.5),20,.6),('Rim',(0,.6,.7),35,.5)]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;l=bpy.data.objects.new(name,d);s.collection.objects.link(l);l.location=loc;l.rotation_euler=(Vector((0,0,.08))-l.location).to_track_quat('-Z','Y').to_euler()
s.render.resolution_x=1200;s.render.resolution_y=900;s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG';s.render.filepath=str(OUT/'RemoteLIVCamera-dark-screen-v3.png')
img.pack();bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'RemoteLIVCamera.blend'))
bpy.ops.render.render(write_still=True)
