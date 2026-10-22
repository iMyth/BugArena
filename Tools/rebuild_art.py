"""One-command deterministic rebuild; final editable source is BugArenaOptimized.blend."""
import subprocess, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[1]
blender='/Applications/Blender.app/Contents/MacOS/Blender'
for script in ['build_blender_art.py','animate_blender_beetles.py','validate_blender_art.py']:
 subprocess.run([blender,'--background','--python-exit-code','1','--python',str(root/'Tools'/script)],check=True,cwd=root)
