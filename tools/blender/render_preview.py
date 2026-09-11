"""Render the currently open character source for visual pipeline verification."""
from pathlib import Path
import bpy
from mathutils import Vector

camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (4.2, -7.5, 3.1)
camera.rotation_euler = ((Vector((0, 0, 1.05)) - camera.location).to_track_quat('-Z', 'Y')).to_euler()
bpy.context.scene.camera = camera
for location, energy, size in [((-4, -4, 7), 1100, 4), ((4, 1, 4), 650, 3)]:
    data = bpy.data.lights.new("PreviewLight", 'AREA'); data.energy = energy; data.shape = 'DISK'; data.size = size
    light = bpy.data.objects.new("PreviewLight", data); bpy.context.collection.objects.link(light); light.location = location
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 600; scene.render.resolution_y = 600; scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
if scene.world is None: scene.world = bpy.data.worlds.new("PreviewWorld")
scene.world.color = (.035, .045, .07)
root = Path(bpy.data.filepath).resolve().parents[2]
(root / "artifacts").mkdir(exist_ok=True)
scene.render.filepath = str(root / "artifacts" / "character-preview.png")
bpy.ops.render.render(write_still=True)
