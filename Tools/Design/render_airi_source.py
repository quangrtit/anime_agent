import bpy
import os
from mathutils import Vector

REPO = r"D:\wibu_project"
BLEND_PATH = os.path.join(REPO, "Assets", "_Project", "Art", "Characters", "Airi", "Source", "Airi_Master.blend")
RENDER_PATH = os.path.join(REPO, "Docs", "Evidence", "M2_Airi_Blender.png")

bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)

def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()

def studio_material(name, color, roughness=0.5, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat

bpy.ops.mesh.primitive_plane_add(size=24, location=(0, 0.7, 0.10))
ground = bpy.context.object
ground.data.materials.append(studio_material("StudioGroundMat", (0.018, 0.025, 0.060), 0.48, 0.1))

camera_data = bpy.data.cameras.new("AiriPreviewCamera")
camera = bpy.data.objects.new("AiriPreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (7.5, -14.8, 6.7)
camera.data.lens = 68
look_at(camera, (0, 0, 3.75))
bpy.context.scene.camera = camera

def area_light(name, location, energy, color, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    look_at(obj, (0, 0, 4.0))

area_light("Key", (-4.5, -6.0, 9.5), 1200, (0.78, 0.85, 1.0), 5.0)
area_light("Rim", (4.5, 1.5, 7.0), 1450, (0.05, 0.65, 1.0), 4.0)
area_light("WarmFill", (2.0, -4.0, 3.0), 700, (1.0, 0.50, 0.22), 3.0)

world = bpy.context.scene.world or bpy.data.worlds.new("AiriPreviewWorld")
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.006, 0.009, 0.025, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = RENDER_PATH
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.ops.render.render(write_still=True)
print("AIRI_SOURCE_RENDER_OK", RENDER_PATH)
