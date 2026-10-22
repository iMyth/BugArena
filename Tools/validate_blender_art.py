"""Validate palette slots, loop continuity, skinning and actual animated sole clearance."""
import bpy,json,math,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SOURCE=Path('/Users/mythchow/Desktop/projects/BugArenaModels')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/BugArena.blend'))
scene=bpy.context.scene
report={'meshes':{},'actions':{},'ground':{},'before':{'arena_triangles':11956,'action_keys':31360,'imported_asset_submeshes':11},'palette':'8x1 sRGB PNG; alpha=smoothness; point sampling; UV cell centers'}
for name in ['JadeBeetleMesh','EmberBeetleMesh','MosswoodArena']:
 obj=bpy.data.objects[name];obj.data.calc_loop_triangles()
 report['meshes'][name]={'triangles':len(obj.data.loop_triangles),'vertices':len(obj.data.vertices),'material_slots':len(obj.data.materials),'data_name':obj.data.name}
 assert len(obj.data.materials)==1,name+' has excess slots'
 assert obj.data.uv_layers.active.name=='PaletteUV'
 if 'Beetle' in name:
  rig=obj.parent;assert len(rig.data.bones)==16
  bone_names=set(rig.data.bones.keys());max_weights=0
  for vertex in obj.data.vertices:
   weights=[g.weight for g in vertex.groups if obj.vertex_groups[g.group].name in bone_names and g.weight>1e-6]
   assert weights and abs(sum(weights)-1)<.001,'Skin weights are invalid'
   max_weights=max(max_weights,len(weights))
  assert max_weights<=2
  report['meshes'][name]['bones']=16;report['meshes'][name]['max_weights']=max_weights
for action in bpy.data.actions:
 curves=[c for l in action.layers for s in l.strips for bag in s.channelbags for c in bag.fcurves]
 if not curves:continue
 report['actions'][action.name]={'curves':len(curves),'keys':sum(len(c.keyframe_points) for c in curves),'constant':sum(max(k.co.y for k in c.keyframe_points)-min(k.co.y for k in c.keyframe_points)<1e-6 for c in curves)}
report['after_action_keys']=sum(a['keys'] for a in report['actions'].values())
assert report['after_action_keys']<31360*.3,'Animation keys were not reduced'
for name in ['JadeBeetleMesh','EmberBeetleMesh']:
 obj=bpy.data.objects[name];report['ground'][name]={}
 for clip,start,end,step in [('Walk',1,25,.25),('Idle',40,112,.5)]:
  lows=[];first=None;last=None
  for i in range(round((end-start)/step)+1):
   value=start+i*step;scene.frame_set(int(value),subframe=value-int(value))
   dg=bpy.context.evaluated_depsgraph_get();ev=obj.evaluated_get(dg);mesh=ev.to_mesh()
   positions=[ev.matrix_world@v.co for v in mesh.vertices]
   lows.append(min(v.z for v in positions))
   if first is None:first=[v.copy() for v in positions]
   last=positions;ev.to_mesh_clear()
  delta=max((a-b).length for a,b in zip(first,last))
  report['ground'][name][clip]={'minimum_sole_z':min(lows),'maximum_lowest_vertex_z':max(lows),'loop_mesh_delta':delta,'samples':len(lows)}
  assert min(lows)>=.0275-.001,f'{name} {clip} penetrates sand: {min(lows)}'
  assert max(lows)<.055,f'{name} {clip} floats: {max(lows)}'
  assert delta<.0001,f'{name} {clip} loop is discontinuous'
assert report['meshes']['MosswoodArena']['triangles']<=2400
for name in ['JadeBeetle','EmberBeetle','MosswoodArena']:
 file=SOURCE/'Exports'/f'{name}.fbx'
 report.setdefault('fbx_bytes',{})[name]=file.stat().st_size
 report.setdefault('before_fbx_bytes',{})[name]=(SOURCE/'Archive/before-optimization'/f'{name}.fbx').stat().st_size
(ROOT/'Docs').mkdir(exist_ok=True)
(ROOT/'Docs/art-optimization.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(SOURCE/'optimization-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('ART_VALIDATION',json.dumps(report))
