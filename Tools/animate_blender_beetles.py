"""Rig the existing beetles, author real Idle/Walk actions and export animated FBX.
Run after build_blender_art.py; produces the canonical ArtSource/BugArena.blend.
"""
import bpy, math, json, os, sys, shutil
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from art_palette import collapse_slots, material
from mathutils import Vector, Quaternion
ROOT=str(Path(__file__).resolve().parents[1])
SOURCE='/Users/mythchow/Desktop/projects/BugArenaModels'
OUT=ROOT+'/Assets/Resources/BugArenaArt'
bpy.ops.wm.open_mainfile(filepath=ROOT+'/Library/BugArena/Geometry.blend')
scene=bpy.context.scene
scene.render.fps=30
scene.frame_start=1;scene.frame_end=112

def xyz(v):return Vector((v[0],-v[2],v[1]))
def key(pose, frame):
 pose.keyframe_insert(data_path='rotation_quaternion',frame=frame)
 pose.keyframe_insert(data_path='location',frame=frame)
 pose.keyframe_insert(data_path='scale',frame=frame)
def assign_group(obj,name,vertices):
 group=obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
 if vertices:group.add(vertices,1,'REPLACE')
def local_rotation(bone,delta):
 rest=bone.bone.matrix_local.to_quaternion()
 return rest.inverted() @ delta @ rest

def solve_leg(hip,knee,foot,target):
 upper=(knee-hip).length;lower=(foot-knee).length
 direction=target-hip; distance=min(direction.length,upper+lower-.002)
 direction.normalize()
 pole=knee-hip;pole-=direction*pole.dot(direction);pole.normalize()
 along=(upper*upper-lower*lower+distance*distance)/(2*distance)
 bent=hip+direction*along+pole*math.sqrt(max(0,upper*upper-along*along))
 return bent

report={}
for rival,name in [(False,'JadeBeetle'),(True,'EmberBeetle')]:
 suffix='.001' if rival else ''
 body=bpy.data.objects['Carapace'+suffix]
 legs=[bpy.data.objects[f'Leg_{side}_{i}'+suffix] for side in [-1,1] for i in range(3)]
 # Undo the display layout applied by preview_blender_art.py, before rigging at origin.
 for obj in [body]+legs:
  obj.hide_set(False);obj.hide_render=False
  if bpy.context.scene.get('bugarena_display_layout',False) and rival:
   obj.location.x=3.1-obj.location.x;obj.location.y=-obj.location.y;obj.rotation_euler.z-=math.pi
  elif bpy.context.scene.get('bugarena_display_layout',False):obj.location.x+=3.1
  bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
  bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 # The 16-bone rig has two joints per leg and two independent antennae.
 data=bpy.data.armatures.new(name+'Skeleton');rig=bpy.data.objects.new(name+'Rig',data);scene.collection.objects.link(rig)
 bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 def bone(name,head,tail,parent=None):
  b=data.edit_bones.new(name);b.head=head;b.tail=tail
  if parent:b.parent=data.edit_bones[parent]
  return b
 bone('Root',xyz((0,0,0)),xyz((0,.25,0)))
 bone('Body',xyz((0,.5,0)),xyz((.4,.5,0)),'Root')
 rest={}
 for side in [-1,1]:
  for i in range(3):
   x=-.73+i*.55
   hip=xyz((x,.46,side*.36));knee=xyz((x-.16,.28,side*.8));foot=xyz((x+.08,.08,side*.99))
   bone(f'Leg_{side}_{i}',hip,knee,'Root');bone(f'Knee_{side}_{i}',knee,foot,f'Leg_{side}_{i}')
   rest[side,i]=(hip,knee,foot)
  bone(f'Antenna_{side}',xyz((.5,1,side*.23)),xyz((.7,1.37,side*.43)),'Body')
 bpy.ops.object.mode_set(mode='OBJECT')
 # Each disconnected antenna island gets a rigid antenna weight; the shell remains rigid.
 adjacency=[set() for _ in body.data.vertices]
 for edge in body.data.edges:
  a,b=edge.vertices;adjacency[a].add(b);adjacency[b].add(a)
 unseen=set(range(len(adjacency)))
 while unseen:
  seed=unseen.pop();island=[seed];todo=[seed]
  while todo:
   for n in adjacency[todo.pop()]:
    if n in unseen:unseen.remove(n);island.append(n);todo.append(n)
  center=sum((body.matrix_world @ body.data.vertices[n].co for n in island),Vector())/len(island)
  antenna=center.z>1.06 and .4<center.x<.85 and .2<abs(center.y)<.55
  assign_group(body,f'Antenna_{-1 if center.y>0 else 1}' if antenna else 'Body',island)
 leg_samples={}
 for obj,(side,i) in zip(legs,[(s,i) for s in [-1,1] for i in range(3)]):
  obj.name=f'Limb_{side}_{i}'+suffix
  leg_samples[side,i]=[(obj.matrix_world@v.co,max(0,min(1,((obj.matrix_world@v.co).z-.245)/.075))) for v in obj.data.vertices]
  thigh=obj.vertex_groups.new(name=f'Leg_{side}_{i}');shin=obj.vertex_groups.new(name=f'Knee_{side}_{i}')
  for v in obj.data.vertices:
   height=(obj.matrix_world@v.co).z
   upper=max(0,min(1,(height-.245)/.075))
   if upper>0:thigh.add([v.index],upper,'REPLACE')
   if upper<1:shin.add([v.index],1-upper,'REPLACE')
 bpy.ops.object.select_all(action='DESELECT')
 for o in [body]+legs:o.select_set(True)
 bpy.context.view_layer.objects.active=body;bpy.ops.object.join();mesh=bpy.context.object;mesh.name=name+'Mesh'
 # Bake object offset into mesh so all bones and vertices use the same origin.
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 modifier=mesh.modifiers.new('Beetle skin','ARMATURE');modifier.object=rig;mesh.parent=rig
 for pose in rig.pose.bones:pose.rotation_mode='QUATERNION'
 collapse_slots(mesh,material('BeetlePalette_Coral' if rival else 'BeetlePalette_Jade',(.95,.22,.16) if rival else (.08,.65,.44)))
 actions=[]
 for clip,frames in [('Walk',25),('Idle',73)]:
  action=bpy.data.actions.new(name+'_'+clip);rig.animation_data_create();rig.animation_data.action=action
  for frame in range(1,frames+1):
   phase=(frame-1)/(frames-1)*math.tau
   for pose in rig.pose.bones:
    pose.location=(0,0,0);pose.rotation_quaternion=Quaternion();pose.scale=(1,1,1)
   body_pose=rig.pose.bones['Body']
   if clip=='Walk':
    body_pose.location.z=math.sin(phase*2)*.045
    body_pose.rotation_quaternion=Quaternion((1,0,0),math.sin(phase)*.035)
   else:
    # Subtle shell breathing keeps the planted silhouette calm.
    body_pose.scale=(1+math.sin(phase)*.012,1+math.sin(phase)*.018,1+math.sin(phase)*.025)
   for side in [-1,1]:
    rig.pose.bones[f'Antenna_{side}'].rotation_quaternion=local_rotation(rig.pose.bones[f'Antenna_{side}'],Quaternion((1,0,0),math.sin(phase+(0 if side==1 else .6))*(.06 if clip=='Walk' else .10)))
    for i in range(3):
     hip,knee,foot=rest[side,i]
     # Tripod A = front/back left + middle right; B is its complement.
     t=((frame-1)/(frames-1)+((i+(0 if side==-1 else 1))%2)*.5)%1
     target=foot.copy();lift=0
     if clip=='Walk':
      if t<.5:
       u=t*2;target.x+=-.30+.60*(u*u*(3-2*u));lift=math.sin(math.pi*u)*.25
      else:target.x+=.30-1.20*(t-.5)
     # Iterate against the deformed mesh sole, rather than just the ankle joint.
     # Legs are parented to Root, so breathing and shell bob do not drag planted feet.
     desired=.035+lift
     for correction in range(12):
      newknee=solve_leg(hip,knee,foot,target)
      hipdelta=(knee-hip).rotation_difference(newknee-hip)
      kneedelta=(foot-knee).rotation_difference(target-newknee)
      low=min((w*(hip+hipdelta@(v-hip))+(1-w)*(newknee+kneedelta@(v-knee))).z for v,w in leg_samples[side,i])
      error=desired-low
      if abs(error)<.00001:break
      target.z+=error
     upper=rig.pose.bones[f'Leg_{side}_{i}'];lower=rig.pose.bones[f'Knee_{side}_{i}']
     upper.rotation_quaternion=local_rotation(upper,hipdelta)
     lower.rotation_quaternion=local_rotation(lower,hipdelta.inverted()@kneedelta)
   for pose in rig.pose.bones:key(pose,frame)
  source_count=0;removed=0
  def reduce_keys(points,tolerance):
   if len(points)<=2:return points
   a,b=points[0],points[-1]
   errors=[abs(y-(a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]))) for x,y in points]
   index=max(range(len(points)),key=lambda i:errors[i])
   if errors[index]<=tolerance:return [a,b]
   return reduce_keys(points[:index+1],tolerance)[:-1]+reduce_keys(points[index:],tolerance)
  for layer in action.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:
     for curve in list(bag.fcurves):
      points=[(float(k.co.x),float(k.co.y)) for k in curve.keyframe_points];source_count+=len(points)
      values=[p[1] for p in points]
      default=1 if (curve.data_path.endswith('scale') or curve.data_path.endswith('rotation_quaternion') and curve.array_index==0) else 0
      constant=max(values)-min(values)<1e-7
      if constant and abs(values[0]-default)<1e-7:
       removed+=len(points);bag.fcurves.remove(curve);continue
      kept=reduce_keys(points,.00001 if curve.data_path.endswith('rotation_quaternion') else .0001)
      removed+=len(points)-len(kept)
      curve.keyframe_points.clear()
      for x,y in kept:
       k=curve.keyframe_points.insert(x,y,options={'FAST'});k.interpolation='LINEAR'
      curve.update()
  print('KEY_REDUCTION',name,clip,source_count,'->',source_count-removed)
  actions.append((clip,action,frames))
 rig.animation_data.action=None
 track=rig.animation_data.nla_tracks.new();track.name='Walk then Idle (loop clips)'
 for clip,action,frames in actions:
  strip=track.strips.new(clip,1 if clip=='Walk' else 40,action);strip.name=clip
  strip.extrapolation='NOTHING';strip.blend_type='REPLACE'
 scene.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_step=.5,bake_anim_simplify_factor=.1,mesh_smooth_type='FACE')
 shutil.copy2(OUT+'/'+name+'.fbx',SOURCE+'/Exports/'+name+'.fbx')
 mesh.data.calc_loop_triangles()
 report[name]={'bones':len(data.bones),'clips':{'Walk':.8,'Idle':2.4},'triangles':len(mesh.data.loop_triangles)}
 # Place rigs for editing/preview only; all exported motion stays in place.
 rig.location.x=3.1 if rival else -3.1
 if rival:rig.rotation_euler.z=math.pi
 rig.show_in_front=True
scene.frame_set(40)
bpy.ops.object.select_all(action='DESELECT')
bpy.data.objects['JadeBeetleRig'].select_set(True);bpy.context.view_layer.objects.active=bpy.data.objects['JadeBeetleRig']
# Keep one authoritative source file, with packed palette textures for portability.
for image in bpy.data.images:
 if image.source=='FILE' and image.name.endswith('.png'):image.pack()
if not scene.camera:
 def area(name,location,energy):
  bpy.ops.object.light_add(type='AREA',location=location);lamp=bpy.context.object;lamp.name=name;lamp.data.energy=energy;lamp.data.shape='DISK';lamp.data.size=7;lamp.rotation_euler=(Vector((0,0,.5))-lamp.location).to_track_quat('-Z','Y').to_euler()
 area('Warm key',(-3,-5,8),1500);area('Soft fill',(3,5,6),1100)
 bpy.ops.object.camera_add(location=(8,-15,11));cam=bpy.context.object;cam.name='Art preview camera';cam.rotation_euler=(Vector((0,0,.1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=23;scene.camera=cam
 scene.render.engine='CYCLES';scene.cycles.samples=24;scene.world.color=(.16,.16,.16)
scene.render.resolution_x=1400;scene.render.resolution_y=850;scene.render.resolution_percentage=100
os.makedirs(ROOT+'/ArtSource',exist_ok=True)
master=ROOT+'/ArtSource/BugArena.blend'
bpy.ops.wm.save_as_mainfile(filepath=master)
with open(SOURCE+'/animation-report.json','w') as f:json.dump(report,f,indent=2)
print('ANIMATION_REPORT',report)
