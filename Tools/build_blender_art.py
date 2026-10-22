"""Rebuild BugArena art with Blender --background --python this_file."""
import bpy, math, os, json, random, sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from art_palette import collapse_slots, material
from mathutils import Vector
ROOT=str(Path(__file__).resolve().parents[1])
os.makedirs(ROOT+'/Library/BugArena',exist_ok=True)
OUT=ROOT+'/Assets/Resources/BugArenaArt'
SOURCE='/Users/mythchow/Desktop/projects/BugArenaModels'
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def mat(name,c):
 m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*c,1); p.inputs['Roughness'].default_value=.4
 return m
jade=mat('Jade',(.08,.65,.44)); coral=mat('Coral',(.95,.22,.16)); dark=mat('Charcoal',(.035,.075,.085)); ivory=mat('Ivory',(.98,.86,.59)); gold=mat('Gold',(.95,.58,.12)); moss=mat('Moss',(.16,.32,.16)); stone=mat('Stone',(.24,.19,.13)); sand=mat('Sand',(.52,.39,.22))
# Author coordinates match Unity: X forward, Y up, Z width; convert to Blender Z up.
def xyz(v): return (v[0],-v[2],v[1])
def ell(name,p,s,m,segments=16,rings=8,smooth=True):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=xyz(p)); o=bpy.context.object; o.name=name; o.scale=(s[0]/2,s[2]/2,s[1]/2); o.data.materials.append(m)
 for f in o.data.polygons:f.use_smooth=smooth
 return o
def rod(name,a,b,r,m,r2=None):
 a,b=Vector(xyz(a)),Vector(xyz(b)); d=b-a
 bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=r,radius2=r if r2 is None else r2,depth=d.length,location=(a+b)/2)
 o=bpy.context.object;o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();o.data.materials.append(m)
 for f in o.data.polygons:f.use_smooth=True
 return o
def join(obs,name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.convert(target='MESH');bpy.ops.object.join();o=bpy.context.object;o.name=name
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');return o
def export(obs,name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
 for o in obs: o.data.calc_loop_triangles()
 import shutil
 os.makedirs(SOURCE+'/Exports',exist_ok=True)
 shutil.copy2(OUT+'/'+name+'.fbx',SOURCE+'/Exports/'+name+'.fbx')
 return sum(len(o.data.loop_triangles) for o in obs)
report={}
for name,shell in [('JadeBeetle',jade),('EmberBeetle',coral)]:
 body=[ell('Belly',(-.25,.58,0),(1.65,.77,1.12),dark)]
 for side in [-1,1]:
  body += [ell('Elytron',(-.35,.82,side*.29),(1.55,.85,.67),shell),ell('Eye',(.66,.88,side*.40),(.39,.38,.19),ivory),ell('Pupil',(.75,.89,side*.485),(.17,.22,.07),dark),ell('Glint',(.78,.95,side*.52),(.065,.065,.025),ivory),rod('Antenna',(.5,1,side*.23),(.7,1.37,side*.43),.035,dark),ell('Tip',(.7,1.37,side*.43),(.13,.13,.13),gold)]
 body += [ell('Head',(.5,.65,0),(.94,.73,.91),shell),rod('Horn',(.84,.63,0),(1.27,.96,0),.17,ivory,.10),rod('Horn point',(1.27,.96,0),(1.34,1.36,0),.10,ivory,.012)]
 body=join(body,'Carapace');obs=[body]
 for side in [-1,1]:
  for i in range(3):
   x=-.73+i*.55
   leg=join([rod('Thigh',(x,.46,side*.36),(x-.16,.28,side*.8),.085,dark),rod('Shin',(x-.16,.28,side*.8),(x+.08,.08,side*.99),.065,dark),ell('Foot',(x+.12,.08,side*.99),(.3,.14,.2),dark)],f'Leg_{side}_{i}')
   # Set origin at hip so existing procedural gait can rotate each limb.
   bpy.context.scene.cursor.location=xyz((x,.46,side*.36));bpy.context.view_layer.objects.active=leg;bpy.ops.object.origin_set(type='ORIGIN_CURSOR');obs.append(leg)
 report[name]=export(obs,name)
 for o in obs:o.hide_set(True)
# Elliptical moss and stone plinth, with chunky perimeter stones and leaf ornaments.
obs=[]
def disc(name,y,rx,rz,h,m):
 bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=1,depth=h,location=xyz((0,y,0)));o=bpy.context.object;o.name=name;o.scale=(rx,rz,1);o.data.materials.append(m);return o
obs += [disc('Rock foundation',-.38,10,3.6,.7,stone),disc('Moss rim',-.055,10.05,3.63,.12,moss),disc('Sand ring',.005,9.45,2.9,.045,sand)]
for i in range(32):
 a=i*math.tau/32
 obs.append(ell('Rim stone',(9.8*math.cos(a),-.15,3.4*math.sin(a)),(.65,.4,.47),stone,8,3,False))
for i in range(18):
 a=i*math.tau/18
 leaf=ell('Moss cushion',(9.6*math.cos(a),.03,3.32*math.sin(a)),(.6,.16,.33),moss,8,3,True);leaf.rotation_euler.z=-a;obs.append(leaf)
obs=[join(obs,'MosswoodArena')];collapse_slots(obs[0],material('ArenaPalette'));report['MosswoodArena']=export(obs,'MosswoodArena')
for o in bpy.data.objects:o.hide_set(False)
# Keep a curated source layout with both fighters beside the arena.
for o in bpy.data.objects:
 if o.name.startswith('Carapace') or o.name.startswith('Leg_'):o.hide_render=True
bpy.context.scene['bugarena_display_layout']=False
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Library/BugArena/Geometry.blend')
with open(SOURCE+'/mesh-report.json','w') as f:json.dump(report,f,indent=2)
print('ART_REPORT',report)
