"""Export hand-edited canonical Blender art without regenerating geometry or animation."""
import bpy, shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Resources/BugArenaArt'
EXPORT=Path('/Users/mythchow/Desktop/projects/BugArenaModels/Exports')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/BugArena.blend'))
OUT.mkdir(parents=True,exist_ok=True);EXPORT.mkdir(parents=True,exist_ok=True)
for image in bpy.data.images:
 if image.name in ['JadePalette.png','CoralPalette.png']:
  image.filepath_raw=str(EXPORT/image.name);image.file_format='PNG';image.save()
  shutil.copy2(EXPORT/image.name,OUT/image.name)
for name in ['JadeBeetle','EmberBeetle','MosswoodArena']:
 bpy.ops.object.select_all(action='DESELECT')
 rig=bpy.data.objects.get(name+'Rig');matrix=rig.matrix_world.copy() if rig else None
 try:
  if rig:
   rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale=(1,1,1)
   rig.select_set(True);bpy.data.objects[name+'Mesh'].select_set(True);bpy.context.view_layer.objects.active=rig
  else:bpy.data.objects[name].select_set(True)
  bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=bool(rig),bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_step=.5,bake_anim_simplify_factor=.1,mesh_smooth_type='FACE')
  shutil.copy2(EXPORT/(name+'.fbx'),OUT/(name+'.fbx'))
 finally:
  if rig:rig.matrix_world=matrix
