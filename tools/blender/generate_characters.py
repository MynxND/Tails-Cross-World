"""Generate original low-poly, articulated 12 Tails character sources and FBX exports."""
from __future__ import annotations

import math
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art-source" / "characters"
EXPORT = ROOT / "client" / "Assets" / "TwelveTails" / "Art" / "Characters"

ROSTER = [
    ("Wolf", "Swordmaster", (0.32, 0.40, 0.52, 1)), ("Bison", "Berserker", (0.34, 0.16, 0.07, 1)),
    ("Panda", "MartialArtist", (0.82, 0.82, 0.76, 1)), ("Whale", "Guardian", (0.18, 0.55, 0.75, 1)),
    ("Mole", "Bomber", (0.25, 0.14, 0.08, 1)), ("Rabbit", "Alchemist", (0.86, 0.72, 0.60, 1)),
    ("Monkey", "Summoner", (0.54, 0.25, 0.08, 1)), ("Sheep", "Priest", (0.88, 0.86, 0.70, 1)),
    ("Penguin", "IceWizard", (0.05, 0.10, 0.16, 1)), ("Bat", "NightMage", (0.22, 0.08, 0.30, 1)),
    ("Chameleon", "Archer", (0.16, 0.62, 0.18, 1)), ("Cat", "TreasureHunter", (0.78, 0.36, 0.12, 1)),
]


def material(name: str, color: tuple[float, float, float, float]):
    value = bpy.data.materials.new(name)
    value.diffuse_color = color
    value.metallic = 0.05
    value.roughness = 0.72
    return value


def part(name, primitive, location, scale, mat, parent, bone=None, rotation=(0, 0, 0)):
    getattr(bpy.ops.mesh, f"primitive_{primitive}_add")(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    obj.parent = parent
    if bone:
        # Deform the whole low-poly part with one bone. Bone parenting plus an
        # inherited world transform caused Unity to apply the rest offset twice.
        obj.parent_type = 'OBJECT'
        obj.matrix_parent_inverse = parent.matrix_world.inverted()
        group = obj.vertex_groups.new(name=bone)
        group.add(range(len(obj.data.vertices)), 1.0, 'REPLACE')
        modifier = obj.modifiers.new(name="Armature", type='ARMATURE')
        modifier.object = parent
    return obj


def armature():
    data = bpy.data.armatures.new("CharacterRig")
    arm = bpy.data.objects.new("CharacterRig", data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bones = {
        "Root": ((0, 0, 0), (0, 0, .5), None), "Spine": ((0, 0, .5), (0, 0, 1.45), "Root"),
        "Head": ((0, 0, 1.45), (0, 0, 2.05), "Spine"),
        "LeftArm": ((0, 0, 1.35), (-.75, 0, .85), "Spine"), "RightArm": ((0, 0, 1.35), (.75, 0, .85), "Spine"),
        "LeftLeg": ((-.22, 0, .55), (-.22, 0, -.2), "Root"), "RightLeg": ((.22, 0, .55), (.22, 0, -.2), "Root"),
        "WeaponSocket": ((.75, 0, .85), (.78, 0, .35), "RightArm"),
    }
    made = {}
    for name, (head, tail, parent) in bones.items():
        bone = data.edit_bones.new(name); bone.head = head; bone.tail = tail
        if parent: bone.parent = made[parent]
        made[name] = bone
    bpy.ops.object.mode_set(mode='POSE')
    for bone in arm.pose.bones: bone.rotation_mode = 'XYZ'
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def key(action_name, arm, frames):
    action = bpy.data.actions.new(action_name)
    arm.animation_data_create(); arm.animation_data.action = action
    for frame, values in frames.items():
        for bone_name, rotation in values.items():
            bone = arm.pose.bones[bone_name]
            bone.rotation_euler = rotation
            bone.keyframe_insert("rotation_euler", frame=frame, group=bone_name)
    action.frame_start, action.frame_end = min(frames), max(frames)
    action.use_fake_user = True


def animations(arm):
    key("Idle", arm, {1: {"Spine": (0, 0, -.025)}, 30: {"Spine": (0, 0, .025)}, 60: {"Spine": (0, 0, -.025)}})
    key("Run", arm, {1: {"LeftArm": (.55, 0, 0), "RightArm": (-.55, 0, 0), "LeftLeg": (-.65, 0, 0), "RightLeg": (.65, 0, 0)}, 12: {"LeftArm": (-.55, 0, 0), "RightArm": (.55, 0, 0), "LeftLeg": (.65, 0, 0), "RightLeg": (-.65, 0, 0)}, 24: {"LeftArm": (.55, 0, 0), "RightArm": (-.55, 0, 0), "LeftLeg": (-.65, 0, 0), "RightLeg": (.65, 0, 0)}})
    key("Attack", arm, {1: {"RightArm": (-.3, 0, -.2)}, 8: {"RightArm": (-1.6, 0, -.65)}, 16: {"RightArm": (.55, 0, .45)}, 24: {"RightArm": (-.3, 0, -.2)}})
    arm.animation_data.action = None


def create_character(name, class_name, color, index):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = armature()
    fur = material(f"{name}_Fur", color); dark = material(f"{name}_Dark", tuple(max(0, c * .35) for c in color[:3]) + (1,))
    light = material(f"{name}_Light", tuple(min(1, c + .22) for c in color[:3]) + (1,)); armor = material(f"{name}_Armor", (.10 + index % 3 * .08, .20, .32 + index % 4 * .05, 1))
    part("Body", "uv_sphere", (0, 0, 1.0), (.52, .36, .72), fur, arm, "Spine")
    part("Armor", "uv_sphere", (0, -.02, 1.05), (.57, .39, .45), armor, arm, "Spine")
    part("Head", "uv_sphere", (0, 0, 1.72), (.48, .42, .44), fur, arm, "Head")
    part("Muzzle", "uv_sphere", (0, -.38, 1.62), (.25, .18, .18), light, arm, "Head")
    for side in (-1, 1):
        part(f"Eye_{side}", "uv_sphere", (.18 * side, -.39, 1.78), (.07, .05, .09), dark, arm, "Head")
        part(f"Arm_{side}", "uv_sphere", (.52 * side, 0, .98), (.16, .16, .48), fur, arm, "LeftArm" if side < 0 else "RightArm")
        part(f"Leg_{side}", "uv_sphere", (.22 * side, 0, .35), (.18, .2, .42), fur, arm, "LeftLeg" if side < 0 else "RightLeg")
    if name in {"Rabbit", "Bat"}: height = .62
    else: height = .30
    for side in (-1, 1): part(f"Ear_{side}", "cone", (.25 * side, 0, 2.18), (.16, .16, height), fur, arm, "Head")
    if name == "Bison":
        for side in (-1, 1): part(f"Horn_{side}", "cone", (.48 * side, 0, 1.95), (.14, .14, .35), light, arm, "Head", (0, math.radians(70 * side), 0))
    if name == "Bat":
        for side in (-1, 1): part(f"Wing_{side}", "cone", (.68 * side, .05, 1.15), (.55, .12, .55), dark, arm, "LeftArm" if side < 0 else "RightArm", (0, math.radians(90), 0))
    weapon_type = "cylinder" if class_name in {"Priest", "IceWizard", "NightMage", "Alchemist"} else "cube"
    weapon_scale = (.08, .08, .72) if weapon_type == "cylinder" else (.09, .08, .75)
    part(f"Weapon_{class_name}", weapon_type, (.82, -.05, .55), weapon_scale, dark, arm, "WeaponSocket", (0, 0, math.radians(-18)))
    animations(arm)
    SOURCE.mkdir(parents=True, exist_ok=True); EXPORT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / f"{name}.blend"))
    bpy.ops.export_scene.fbx(filepath=str(EXPORT / f"{name}.fbx"), use_selection=False, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, apply_scale_options='FBX_SCALE_UNITS')


for roster_index, definition in enumerate(ROSTER):
    create_character(*definition, roster_index)
print(f"Generated {len(ROSTER)} Blender sources and FBX exports")
