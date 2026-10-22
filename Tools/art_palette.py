"""Portable 8x1 nearest-filtered palette. RGB is sRGB; alpha stores smoothness."""
import bpy, os, struct, zlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SOURCE=Path('/Users/mythchow/Desktop/projects/BugArenaModels')
OUT=ROOT/'Assets/Resources/BugArenaArt'
EXPORT=SOURCE/'Exports'
COLORS=[(.08,.65,.44),(.035,.075,.085),(.98,.86,.59),(.95,.58,.12),(.24,.19,.13),(.16,.32,.16),(.52,.39,.22),(1,1,1)]
ROUGHNESS=[.35,.75,.45,.28,.90,.95,.85,.5]
INDEX={'Jade':0,'Coral':0,'Charcoal':1,'Ivory':2,'Gold':3,'Stone':4,'Moss':5,'Sand':6}
def srgb(x):return 12.92*x if x<=.0031308 else 1.055*x**(1/2.4)-.055
def png(path,shell):
 colors=[shell]+COLORS[1:]
 row=bytes([0])+bytes(round(max(0,min(1,x))*255) for i,c in enumerate(colors) for x in (*[srgb(v) for v in c],1-ROUGHNESS[i]))
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',8,1,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(row))+chunk(b'IEND',b''))
def material(name,shell=COLORS[0]):
 EXPORT.mkdir(parents=True,exist_ok=True);OUT.mkdir(parents=True,exist_ok=True)
 filename='CoralPalette.png' if name.endswith('Coral') else 'JadePalette.png'
 png(EXPORT/filename,shell);(OUT/filename).write_bytes((EXPORT/filename).read_bytes())
 image=bpy.data.images.load(str(EXPORT/filename),check_existing=True)
 image.colorspace_settings.name='sRGB'
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True
 nodes=m.node_tree.nodes;nodes.clear();output=nodes.new('ShaderNodeOutputMaterial');bsdf=nodes.new('ShaderNodeBsdfPrincipled');tex=nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest';tex.extension='EXTEND'
 inverse=nodes.new('ShaderNodeMath');inverse.operation='SUBTRACT';inverse.inputs[0].default_value=1
 links=m.node_tree.links;links.new(tex.outputs['Color'],bsdf.inputs['Base Color']);links.new(tex.outputs['Alpha'],inverse.inputs[1]);links.new(inverse.outputs[0],bsdf.inputs['Roughness']);links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
 m.diffuse_color=(*shell,1)
 return m
def collapse_slots(obj,mat):
 uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='PaletteUV')
 uv.name='PaletteUV'
 for p in obj.data.polygons:
  name=obj.data.materials[p.material_index].name.split('.')[0]
  cell=INDEX.get(name,7)
  for loop in p.loop_indices:uv.data[loop].uv=((cell+.5)/8,.5)
  p.material_index=0
 obj.data.materials.clear();obj.data.materials.append(mat)
 obj.data.name=obj.name+'_Geometry'
