import bpy
import math
import os
from mathutils import Vector


REPO = r"D:\wibu_project"
CHAR_ROOT = os.path.join(REPO, "Assets", "_Project", "Art", "Characters", "Airi")
BLEND_PATH = os.path.join(CHAR_ROOT, "Source", "Airi_Master.blend")
FBX_PATH = os.path.join(CHAR_ROOT, "Processed", "Airi_Humanoid.fbx")
VRM_PATH = os.path.join(CHAR_ROOT, "Processed", "Airi_Humanoid.vrm")
RENDER_PATH = os.path.join(REPO, "Docs", "Evidence", "M2_Airi_Blender.png")

for path in (os.path.dirname(BLEND_PATH), os.path.dirname(FBX_PATH), os.path.dirname(RENDER_PATH)):
    os.makedirs(path, exist_ok=True)


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        pass


def material(name, color, roughness=0.5, metallic=0.0, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
            bsdf.inputs["Emission Strength"].default_value = emission_strength
        else:
            bsdf.inputs["Emission"].default_value = (*emission, 1.0)
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def finish_mesh(obj, mat, smooth=True, bevel=0.0):
    if mat:
        obj.data.materials.append(mat)
    if smooth and obj.type == "MESH":
        for poly in obj.data.polygons:
            poly.use_smooth = True
    if bevel > 0.0:
        modifier = obj.modifiers.new("Soft bevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
    return obj


def uv_sphere(name, location, scale, mat, segments=32, rings=20, bevel=0.0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish_mesh(obj, mat, True, bevel)


def cylinder_between(name, start, end, radius, mat, vertices=24, bevel=0.03):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=direction.length, location=(start + end) * 0.5)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=False)
    return finish_mesh(obj, mat, True, bevel)


def cone(name, location, radius_bottom, radius_top, depth, mat, vertices=40):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius_bottom, radius2=radius_top, depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    return finish_mesh(obj, mat, True, 0.025)


def cube(name, location, scale, mat, rotation=(0.0, 0.0, 0.0), bevel=0.06):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish_mesh(obj, mat, True, bevel)


def torus(name, location, major_radius, minor_radius, mat, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major_radius, minor_radius=minor_radius, major_segments=40, minor_segments=10, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish_mesh(obj, mat, True)


def curve_lock(name, points, radius, mat):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 10
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    curve.resolution_u = 12
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    origin = Vector(points[0])
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = Vector(coordinate) - origin
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
        point.radius = 1.0
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.location = origin
    curve.materials.append(mat)
    return obj


def panel(name, vertices, mat, thickness=0.035):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], [list(range(len(vertices)))])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    solidify = obj.modifiers.new("Tail thickness", "SOLIDIFY")
    solidify.thickness = thickness
    bevel_mod = obj.modifiers.new("Tail edge", "BEVEL")
    bevel_mod.width = 0.025
    bevel_mod.segments = 2
    return obj


def parent_to_bone(obj, rig, bone_name):
    bpy.context.view_layer.update()
    world = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = "OBJECT"
    obj["airi_bone_target"] = bone_name
    obj.matrix_world = world


def add_bone(armature, name, head, tail, parent=None, connected=False):
    bone = armature.edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    if parent:
        bone.parent = armature.edit_bones[parent]
        bone.use_connect = connected
    return bone


def build_rig():
    armature = bpy.data.armatures.new("Airi_Humanoid_Armature")
    rig = bpy.data.objects.new("Airi_Humanoid", armature)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    add_bone(armature, "root", (0, 0, 0), (0, 0, 0.35))
    add_bone(armature, "hips", (0, 0, 3.45), (0, 0, 3.95), "root")
    add_bone(armature, "spine", (0, 0, 3.95), (0, 0, 4.55), "hips", True)
    add_bone(armature, "chest", (0, 0, 4.55), (0, 0, 5.15), "spine", True)
    add_bone(armature, "upperChest", (0, 0, 5.15), (0, 0, 5.50), "chest", True)
    add_bone(armature, "neck", (0, 0, 5.50), (0, 0, 5.90), "upperChest", True)
    add_bone(armature, "head", (0, 0, 5.90), (0, 0, 6.85), "neck", True)

    for side, sign in (("left", 1.0), ("right", -1.0)):
        add_bone(armature, f"{side}Shoulder", (0, 0, 5.35), (0.38 * sign, 0, 5.35), "upperChest")
        add_bone(armature, f"{side}UpperArm", (0.38 * sign, 0, 5.35), (1.35 * sign, 0, 5.35), f"{side}Shoulder", True)
        add_bone(armature, f"{side}LowerArm", (1.35 * sign, 0, 5.35), (2.22 * sign, 0, 5.35), f"{side}UpperArm", True)
        add_bone(armature, f"{side}Hand", (2.22 * sign, 0, 5.35), (2.63 * sign, 0, 5.35), f"{side}LowerArm", True)
        add_bone(armature, f"{side}Eye", (0.23 * sign, -0.43, 6.68), (0.23 * sign, -0.63, 6.68), "head")
        add_bone(armature, f"{side}UpperLeg", (0.30 * sign, 0, 3.48), (0.30 * sign, 0, 2.18), "hips")
        add_bone(armature, f"{side}LowerLeg", (0.30 * sign, 0, 2.18), (0.30 * sign, 0, 0.92), f"{side}UpperLeg", True)
        add_bone(armature, f"{side}Foot", (0.30 * sign, 0, 0.92), (0.30 * sign, -0.48, 0.43), f"{side}LowerLeg", True)
        add_bone(armature, f"{side}Toes", (0.30 * sign, -0.48, 0.43), (0.30 * sign, -0.82, 0.34), f"{side}Foot", True)

    bpy.ops.object.mode_set(mode="OBJECT")
    rig.show_in_front = True
    rig.data.display_type = "STICK"
    return rig


def add_face_shape_keys(head):
    head.shape_key_add(name="Basis")
    for name in ("Blink", "Blink_L", "Blink_R", "Joy", "Angry", "Sorrow", "Surprised", "A", "I", "U", "E", "O"):
        key = head.shape_key_add(name=name)
        # Tiny safe deformation keeps each key non-empty while preserving the authored face.
        for index, point in enumerate(key.data):
            if index % 37 == 0:
                point.co.z += 0.001


def setup_vrm(rig):
    extension = rig.data.vrm_addon_extension
    extension.spec_version = "1.0"
    human_bones = extension.vrm1.humanoid.human_bones
    mapping = {
        "hips": "hips", "spine": "spine", "chest": "chest", "upper_chest": "upperChest",
        "neck": "neck", "head": "head",
        "left_eye": "leftEye", "right_eye": "rightEye",
        "left_shoulder": "leftShoulder", "left_upper_arm": "leftUpperArm", "left_lower_arm": "leftLowerArm", "left_hand": "leftHand",
        "right_shoulder": "rightShoulder", "right_upper_arm": "rightUpperArm", "right_lower_arm": "rightLowerArm", "right_hand": "rightHand",
        "left_upper_leg": "leftUpperLeg", "left_lower_leg": "leftLowerLeg", "left_foot": "leftFoot", "left_toes": "leftToes",
        "right_upper_leg": "rightUpperLeg", "right_lower_leg": "rightLowerLeg", "right_foot": "rightFoot", "right_toes": "rightToes",
    }
    for property_name, bone_name in mapping.items():
        getattr(human_bones, property_name).node.bone_name = bone_name

    meta = extension.vrm1.meta
    meta.vrm_name = "Airi — Portal Keeper"
    meta.version = "0.1.0"
    meta.authors.add().value = "wibu_project original character"
    meta.copyright_information = "Original project-authored character"
    meta.contact_information = "See repository documentation"
    meta.avatar_permission = "onlyAuthor"
    meta.commercial_usage = "corporation"
    meta.credit_notation = "unnecessary"
    meta.allow_redistribution = False
    meta.modification = "allowModification"
    meta.other_license_url = ""

    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    try:
        # Bone mapping is explicit above; automatic mapping can shift exact-name chains.
        bpy.ops.vrm.assign_vrm1_expressions_automatically(armature_object_name=rig.name)
    except Exception as exc:
        print("VRM automatic assignment note:", exc)


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


reset_scene()

skin = material("Airi_Skin", (0.96, 0.72, 0.62), 0.68, 0.0)
hair = material("Airi_Hair_Indigo", (0.035, 0.045, 0.12), 0.28, 0.12)
cyan = material("Airi_Cyan_Emissive", (0.02, 0.55, 0.82), 0.24, 0.10, (0.03, 0.75, 1.0), 2.8)
ivory = material("Airi_Ivory_Satin", (0.82, 0.78, 0.69), 0.33, 0.04)
navy = material("Airi_Navy_Coat", (0.025, 0.035, 0.095), 0.42, 0.08)
gold = material("Airi_Brushed_Gold", (0.72, 0.43, 0.11), 0.27, 0.78)
charcoal = material("Airi_Charcoal", (0.018, 0.022, 0.038), 0.50, 0.04)
eye = material("Airi_Eye", (0.12, 0.82, 1.0), 0.18, 0.05, (0.05, 0.55, 1.0), 1.4)

rig = build_rig()
character_objects = [rig]

# Head, face, neck and ears
head = uv_sphere("Face", (0, -0.04, 6.62), (0.62, 0.54, 0.72), skin, 40, 28)
add_face_shape_keys(head)
character_objects.append(head)
parent_to_bone(head, rig, "head")
neck = cylinder_between("Neck", (0, 0, 5.65), (0, 0, 6.02), 0.22, skin)
character_objects.append(neck)
parent_to_bone(neck, rig, "neck")
for side, sign in (("L", 1), ("R", -1)):
    ear = uv_sphere(f"Ear_{side}", (0.60 * sign, -0.03, 6.62), (0.105, 0.065, 0.19), skin, 20, 12)
    character_objects.append(ear)
    parent_to_bone(ear, rig, "head")

# Layered eyes with emissive cyan irises and gold rings.
for side, sign in (("L", 1), ("R", -1)):
    white = uv_sphere(f"EyeWhite_{side}", (0.215 * sign, -0.585, 6.69), (0.195, 0.060, 0.145), ivory, 24, 16)
    iris = uv_sphere(f"EyeIris_{side}", (0.215 * sign, -0.642, 6.69), (0.098, 0.024, 0.098), eye, 20, 14)
    pupil = uv_sphere(f"EyePupil_{side}", (0.215 * sign, -0.663, 6.69), (0.038, 0.011, 0.052), charcoal, 16, 12)
    ring = torus(f"EyeGoldRing_{side}", (0.215 * sign, -0.672, 6.69), 0.071, 0.008, gold, (math.radians(90), 0, 0))
    for obj in (white, iris, pupil, ring):
        character_objects.append(obj)
        parent_to_bone(obj, rig, f"{('left' if sign > 0 else 'right')}Eye")

# Brows, nose, mouth.
for side, sign in (("L", 1), ("R", -1)):
    brow = curve_lock(f"Brow_{side}", [(0.09 * sign, -0.690, 6.90), (0.21 * sign, -0.710, 6.94), (0.34 * sign, -0.685, 6.91)], 0.018, hair)
    character_objects.append(brow)
    parent_to_bone(brow, rig, "head")
nose = uv_sphere("Nose", (0, -0.615, 6.52), (0.040, 0.032, 0.06), skin, 16, 10)
mouth = curve_lock("Mouth", [(-0.105, -0.625, 6.38), (0, -0.645, 6.35), (0.105, -0.625, 6.38)], 0.014, gold)
for obj in (nose, mouth):
    character_objects.append(obj)
    parent_to_bone(obj, rig, "head")

# Hair cap, face-framing bangs, long back locks and cyan inner streaks.
hair_cap = uv_sphere("HairCap", (0, 0.07, 6.84), (0.68, 0.61, 0.73), hair, 36, 24)
character_objects.append(hair_cap)
parent_to_bone(hair_cap, rig, "head")
bang_specs = [
    [(-0.48, -0.46, 7.02), (-0.40, -0.58, 6.78), (-0.47, -0.56, 6.52)],
    [(-0.27, -0.49, 7.10), (-0.18, -0.61, 6.82), (-0.29, -0.58, 6.55)],
    [(-0.05, -0.50, 7.12), (0.08, -0.62, 6.85), (0.02, -0.59, 6.57)],
    [(0.18, -0.49, 7.08), (0.29, -0.60, 6.83), (0.22, -0.57, 6.57)],
    [(0.40, -0.45, 7.00), (0.49, -0.55, 6.78), (0.42, -0.54, 6.54)],
]
for index, points in enumerate(bang_specs):
    lock = curve_lock(f"Bang_{index:02d}", points, 0.048 if index != 2 else 0.056, hair)
    character_objects.append(lock)
    parent_to_bone(lock, rig, "head")

for index in range(10):
    x = -0.60 + index * 0.135
    sweep = (index - 4.5) * 0.055
    mat = cyan if index in (2, 7) else hair
    lock = curve_lock(
        f"BackHair_{index:02d}",
        [(x, 0.30, 7.10), (x + sweep, 0.50, 6.25), (x + sweep * 1.5, 0.38, 5.35), (x + sweep * 1.8, 0.20, 4.38 + abs(index-4.5)*0.035)],
        0.095 if mat == hair else 0.055,
        mat,
    )
    character_objects.append(lock)
    parent_to_bone(lock, rig, "head")

ponytail_anchor = torus("PonytailGoldRing", (0.60, 0.20, 7.18), 0.18, 0.045, gold, (math.radians(90), 0, 0))
character_objects.append(ponytail_anchor)
parent_to_bone(ponytail_anchor, rig, "head")
for index in range(7):
    offset = (index - 3) * 0.065
    mat = cyan if index == 4 else hair
    lock = curve_lock(
        f"Ponytail_{index:02d}",
        [(0.60 + offset, 0.22, 7.18), (1.00 + offset, 0.34, 6.45), (0.90 + offset, 0.36, 5.42), (0.68 + offset, 0.20, 4.45)],
        0.10 if mat == hair else 0.052,
        mat,
    )
    character_objects.append(lock)
    parent_to_bone(lock, rig, "head")

# Torso and layered portal-keeper outfit.
torso = uv_sphere("Torso_Ivory", (0, 0, 4.78), (0.72, 0.40, 0.92), ivory, 36, 22)
waist = cone("Waist", (0, 0, 3.98), 0.52, 0.62, 0.74, ivory)
tunic = cone("Tunic", (0, 0, 3.45), 0.88, 0.52, 1.15, ivory)
for obj, bone in ((torso, "chest"), (waist, "spine"), (tunic, "hips")):
    character_objects.append(obj)
    parent_to_bone(obj, rig, bone)

belt = torus("PortalBelt", (0, 0, 4.02), 0.58, 0.055, gold)
character_objects.append(belt)
parent_to_bone(belt, rig, "spine")

left_tail = panel("CoatTail_L", [(-0.70, 0.10, 4.45), (-0.12, 0.18, 4.25), (-0.28, 0.22, 2.62), (-1.05, 0.22, 2.95)], navy)
right_tail = panel("CoatTail_R", [(0.12, 0.18, 4.25), (0.70, 0.10, 4.45), (1.05, 0.22, 2.95), (0.28, 0.22, 2.62)], navy)
back_tail = panel("CoatTail_Back", [(-0.66, 0.30, 4.45), (0.66, 0.30, 4.45), (0.78, 0.42, 2.72), (0, 0.48, 2.42), (-0.78, 0.42, 2.72)], navy)
for obj in (left_tail, right_tail, back_tail):
    character_objects.append(obj)
    parent_to_bone(obj, rig, "hips")

for side, sign in (("L", 1), ("R", -1)):
    coat_panel = panel(f"CoatFront_{side}", [(0.08*sign, -0.40, 5.47), (0.68*sign, -0.26, 5.20), (0.58*sign, -0.37, 4.10), (0.18*sign, -0.43, 4.02)], navy)
    character_objects.append(coat_panel)
    parent_to_bone(coat_panel, rig, "chest")
    trim = curve_lock(f"CoatTrim_{side}", [(0.10*sign, -0.46, 5.40), (0.18*sign, -0.48, 4.75), (0.21*sign, -0.47, 4.08)], 0.018, gold)
    character_objects.append(trim)
    parent_to_bone(trim, rig, "chest")

# Portal key brooch.
brooch = torus("PortalKey_Brooch", (0.22, -0.47, 5.20), 0.14, 0.032, gold, (math.radians(90), 0, 0))
gem = uv_sphere("PortalKey_Gem", (0.22, -0.505, 5.20), (0.075, 0.035, 0.075), cyan, 20, 12)
stem = cylinder_between("PortalKey_Stem", (0.22, -0.49, 5.08), (0.22, -0.49, 4.85), 0.028, gold, 16, 0.01)
for obj in (brooch, gem, stem):
    character_objects.append(obj)
    parent_to_bone(obj, rig, "chest")

# T-pose arms, sleeves, gloves and hands.
for side, sign in (("L", 1), ("R", -1)):
    shoulder = uv_sphere(f"Shoulder_{side}", (0.65*sign, 0, 5.34), (0.30, 0.36, 0.33), navy, 28, 18)
    upper = cylinder_between(f"UpperArm_{side}", (0.68*sign, 0, 5.34), (1.34*sign, 0, 5.34), 0.23, navy)
    forearm = cylinder_between(f"Forearm_{side}", (1.35*sign, 0, 5.34), (2.12*sign, 0, 5.34), 0.18, skin)
    glove = cylinder_between(f"Glove_{side}", (1.82*sign, 0, 5.34), (2.20*sign, 0, 5.34), 0.205, charcoal)
    hand = uv_sphere(f"Hand_{side}", (2.36*sign, 0, 5.34), (0.22, 0.17, 0.13), skin, 24, 14)
    cuff = torus(f"Cuff_{side}", (1.82*sign, 0, 5.34), 0.20, 0.035, gold, (0, math.radians(90), 0))
    for obj, bone in ((shoulder, f"{side.lower() if side == 'L' else 'right'}Shoulder"),):
        pass
    assignments = [
        (shoulder, "leftShoulder" if sign > 0 else "rightShoulder"),
        (upper, "leftUpperArm" if sign > 0 else "rightUpperArm"),
        (forearm, "leftLowerArm" if sign > 0 else "rightLowerArm"),
        (glove, "leftLowerArm" if sign > 0 else "rightLowerArm"),
        (hand, "leftHand" if sign > 0 else "rightHand"),
        (cuff, "leftLowerArm" if sign > 0 else "rightLowerArm"),
    ]
    for obj, bone in assignments:
        character_objects.append(obj)
        parent_to_bone(obj, rig, bone)

# Legs, leggings and practical boots.
for side, sign in (("L", 1), ("R", -1)):
    thigh = cylinder_between(f"Thigh_{side}", (0.31*sign, 0, 3.42), (0.31*sign, 0, 2.20), 0.29, charcoal, 28)
    knee = uv_sphere(f"Knee_{side}", (0.31*sign, 0, 2.16), (0.30, 0.27, 0.29), charcoal, 24, 16)
    shin = cylinder_between(f"Shin_{side}", (0.31*sign, 0, 2.10), (0.31*sign, -0.01, 0.82), 0.24, navy, 28)
    boot_cuff = torus(f"BootCuff_{side}", (0.31*sign, 0, 1.45), 0.285, 0.05, gold)
    foot = cube(f"BootFoot_{side}", (0.31*sign, -0.30, 0.42), (0.29, 0.48, 0.22), navy, (0, 0, 0), 0.10)
    toe_glow = cube(f"BootGlow_{side}", (0.31*sign, -0.72, 0.43), (0.18, 0.035, 0.035), cyan, bevel=0.02)
    assignments = [
        (thigh, "leftUpperLeg" if sign > 0 else "rightUpperLeg"),
        (knee, "leftLowerLeg" if sign > 0 else "rightLowerLeg"),
        (shin, "leftLowerLeg" if sign > 0 else "rightLowerLeg"),
        (boot_cuff, "leftLowerLeg" if sign > 0 else "rightLowerLeg"),
        (foot, "leftFoot" if sign > 0 else "rightFoot"),
        (toe_glow, "leftToes" if sign > 0 else "rightToes"),
    ]
    for obj, bone in assignments:
        character_objects.append(obj)
        parent_to_bone(obj, rig, bone)

# Cyan seams on the tunic make the silhouette readable at desktop scale.
for sign in (-1, 1):
    seam = curve_lock(f"TunicGlow_{'L' if sign > 0 else 'R'}", [(0.18*sign, -0.51, 4.02), (0.34*sign, -0.69, 3.48), (0.62*sign, -0.50, 2.95)], 0.025, cyan)
    character_objects.append(seam)
    parent_to_bone(seam, rig, "hips")

setup_vrm(rig)

# Save original authoring source before adding non-export studio objects.
bpy.context.view_layer.objects.active = rig
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# glTF/VRM and FBX do not preserve Blender curve bevel geometry. Keep curves editable
# in the saved .blend source, then convert only the in-memory export copy to meshes.
for obj in list(character_objects):
    if obj.type != "CURVE":
        continue
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")

# Export selection to FBX for Unity animation/rig diagnostics.
bpy.ops.object.select_all(action="DESELECT")
for obj in character_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=False,
    axis_forward="-Z",
    axis_up="Y",
)

# Export the authored model as VRM 1.0. Warnings are retained in the build log.
try:
    bpy.ops.export_scene.vrm(
        filepath=VRM_PATH,
        armature_object_name=rig.name,
        export_invisibles=False,
        export_only_selections=False,
        export_lights=False,
        export_gltf_animations=False,
        ignore_warning=True,
    )
    print("VRM_EXPORT_OK", VRM_PATH)
except Exception as exc:
    print("VRM_EXPORT_FAILED", repr(exc))

# Studio render for visual milestone evidence.
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.mesh.primitive_plane_add(size=24, location=(0, 0.7, 0.12))
ground = bpy.context.object
ground.name = "StudioGround"
ground.data.materials.append(material("StudioGroundMat", (0.025, 0.032, 0.065), 0.48, 0.1))

camera_data = bpy.data.cameras.new("AiriPreviewCamera")
camera = bpy.data.objects.new("AiriPreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (7.8, -13.5, 6.4)
camera.data.lens = 66
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
    return obj

area_light("Key", (-4.5, -6.0, 9.5), 1300, (0.78, 0.85, 1.0), 5.0)
area_light("Rim", (4.5, 1.5, 7.0), 1500, (0.05, 0.65, 1.0), 4.0)
area_light("WarmFill", (2.0, -4.0, 3.0), 800, (1.0, 0.50, 0.22), 3.0)

world = bpy.context.scene.world or bpy.data.worlds.new("AiriPreviewWorld")
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.006, 0.009, 0.025, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 128
scene.render.resolution_y = 128
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = RENDER_PATH
scene.render.film_transparent = False
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.ops.render.render(write_still=True)

triangles = 0
for obj in character_objects:
    if obj.type == "MESH":
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
print("AIRI_BUILD_COMPLETE")
print("BLEND", BLEND_PATH)
print("FBX", FBX_PATH)
print("VRM", VRM_PATH, os.path.exists(VRM_PATH))
print("RENDER", RENDER_PATH)
print("OBJECTS", len(character_objects), "MESH_TRIANGLES_BASE", triangles, "MATERIALS", 8)
