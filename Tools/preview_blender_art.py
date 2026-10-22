"""Render the canonical source without changing or saving its display layout."""
import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SOURCE=Path('/Users/mythchow/Desktop/projects/BugArenaModels')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/BugArena.blend'))
scene=bpy.context.scene;scene.frame_set(40)
scene.render.resolution_x=1400;scene.render.resolution_y=850;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(SOURCE/'BugArena-preview.png')
bpy.ops.render.render(write_still=True)
