import bpy,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
from mathutils import Vector
P='/Users/mythchow/Desktop/projects/BugArenaModels'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/BugArena.blend'))
s=bpy.context.scene
for o in bpy.data.objects:
 if o.name.startswith('EmberBeetle'):o.hide_render=True
cam=s.camera;cam.location=(-.8,-5,3);cam.rotation_euler=(Vector((-3.1,0,.65))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=3.9
s.render.resolution_x=640;s.render.resolution_y=480;s.render.resolution_percentage=100;s.cycles.samples=16
s.render.image_settings.file_format='PNG';os.makedirs(P+'/Previews/BlenderFrames',exist_ok=True)
for name,frames in [('Walk',range(1,25,2)),('Idle',range(40,112,4))]:
 for i,frame in enumerate(frames):
  s.frame_set(frame);s.render.filepath=P+f'/Previews/BlenderFrames/{name}_{i:03d}.png';bpy.ops.render.render(write_still=True)
