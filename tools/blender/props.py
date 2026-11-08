"""
Renders the game's 3D props as sprites:

* sprites/coin/spin/01..16.png  a gold coin with a "?" turning half a revolution (64x64 each)
* sprites/coin/popped.png       the coin facing the camera, for the HUD and coins popping out of boxes
* sprites/reward-box/full.png   a dark stone box with a glowing "!"
* sprites/reward-box/empty.png  the same box after its last coin, with the "!" gone dark
* sprites/goal-gate.png         a stone archway full of warm light (goal of levels without a painted background)
* sprites/checkpoint/lit.png and unlit.png   a lantern on a post

Run:  blender -b --factory-startup --python tools/blender/props.py
"""
import math
import os
import sys

import bmesh
import bpy

sys.path.append(os.path.dirname(__file__))
from common import (  # noqa: E402
    area_light, asset_path, new_scene, ortho_camera, point_light, principled_material, render, srgb,
)

FONTS = "C:/Windows/Fonts"
AMBER = srgb("#FFB547")
WARM_LIGHT = (1.0, 0.72, 0.35)


def ambient(scene, color_hex, strength):
    """Soft environment light so metal and stone are not pitch black."""
    background = scene.world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = srgb(color_hex)
    background.inputs["Strength"].default_value = strength


def stone_material(name, color_hex, bump=0.35):
    """Rough stone: a dark base colour with a noise bump."""
    material = principled_material(name, srgb(color_hex), roughness=0.85)
    nodes, links = material.node_tree.nodes, material.node_tree.links
    bsdf = nodes["Principled BSDF"]
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 9
    noise.inputs["Detail"].default_value = 8
    bump_node = nodes.new("ShaderNodeBump")
    bump_node.inputs["Strength"].default_value = bump
    links.new(noise.outputs["Fac"], bump_node.inputs["Height"])
    links.new(bump_node.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def add_bevel(obj, width, segments=3):
    modifier = obj.modifiers.new("Bevel", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"


def text_object(scene, body, font_file, size, extrude, material):
    curve = bpy.data.curves.new(body, "FONT")
    curve.body = body
    curve.font = bpy.data.fonts.load(os.path.join(FONTS, font_file))
    curve.size = size
    curve.extrude = extrude
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    obj = bpy.data.objects.new(body, curve)
    obj.data.materials.append(material)
    scene.collection.objects.link(obj)
    return obj


def link(scene, name, mesh, material):
    obj = bpy.data.objects.new(name, mesh)
    obj.data.materials.append(material)
    scene.collection.objects.link(obj)
    return obj


def box_mesh(name, sx, sy, sz, center=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    bm.to_mesh(mesh)
    bm.free()
    return mesh


# ─────────────────────────────── Coin ───────────────────────────────

def coin():
    scene = new_scene(64, 64, samples=48)
    ambient(scene, "#D8B47A", 1.6)
    ortho_camera(scene, 0, 0, 2.3)
    area_light(scene, "Key", (-2.5, -4, 3), (55, 0, -30), 3, 700, WARM_LIGHT)
    area_light(scene, "Fill", (3, -3, -1), (100, 0, 40), 3, 250)

    gold = principled_material("Gold", srgb("#FFCC4D"), metallic=1.0, roughness=0.22)
    gold_dark = principled_material("GoldDark", srgb("#7A4A0C"), metallic=0.6, roughness=0.5)

    pivot = bpy.data.objects.new("Pivot", None)
    scene.collection.objects.link(pivot)

    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=64, radius1=1, radius2=1, depth=0.18)
    mesh = bpy.data.meshes.new("Coin")
    bm.to_mesh(mesh)
    bm.free()
    body = link(scene, "Coin", mesh, gold)
    body.rotation_euler = (math.radians(90), 0, 0)   # face the camera
    add_bevel(body, 0.06)
    body.parent = pivot

    # A raised rim and a "?" on both faces.
    for side in (-1, 1):
        rim = bpy.data.objects.new("Rim", bpy.data.meshes.new("Rim"))
        bm = bmesh.new()
        bmesh.ops.create_circle(bm, cap_ends=False, segments=64, radius=0.82)
        rim.data = bpy.data.meshes.new("RimMesh")
        bm.to_mesh(rim.data)
        bm.free()
        rim_mod = rim.modifiers.new("Skin", "WIREFRAME")
        rim_mod.thickness = 0.07
        rim.data.materials.append(gold_dark)
        rim.rotation_euler = (math.radians(90), 0, 0)
        rim.location = (0, side * 0.1, 0)
        scene.collection.objects.link(rim)
        rim.parent = pivot

        mark = text_object(scene, "?", "constanb.ttf", 1.1, 0.04, gold_dark)
        mark.rotation_euler = (math.radians(90), 0, 0 if side < 0 else math.radians(180))
        mark.location = (0, side * 0.1, 0)
        mark.parent = pivot

    # Half a turn is a full cycle: both faces look the same.
    for frame in range(16):
        pivot.rotation_euler = (0, 0, math.radians(frame * 180 / 16))
        render(scene, asset_path("sprites", "coin", "spin", f"{frame + 1:02d}.png"))

    pivot.rotation_euler = (0, 0, math.radians(-12))
    render(scene, asset_path("sprites", "coin", "popped.png"))


# ─────────────────────────────── Reward box ───────────────────────────────

def reward_box(full):
    scene = new_scene(128, 128, samples=48)
    ambient(scene, "#3A3F46", 0.5)
    ortho_camera(scene, 0, 0, 2.0)
    area_light(scene, "Key", (-2, -4, 3), (55, 0, -25), 4, 300)

    stone = stone_material("Stone", "#4A4642" if full else "#33302D")
    block = link(scene, "Box", box_mesh("Box", 1.96, 1.96, 1.96), stone)
    add_bevel(block, 0.12)

    if full:
        glow = principled_material("Glow", AMBER, emission=AMBER, emission_strength=6)
        mark = text_object(scene, "!", "constanb.ttf", 1.6, 0.05, glow)
        point_light(scene, "Inner", (0, -1.4, 0), 25, WARM_LIGHT, radius=0.4)
    else:
        dull = principled_material("Dull", srgb("#26231F"), roughness=0.9)
        mark = text_object(scene, "!", "constanb.ttf", 1.6, 0.02, dull)
    mark.rotation_euler = (math.radians(90), 0, 0)
    mark.location = (0, -1.0, 0)

    render(scene, asset_path("sprites", "reward-box", "full.png" if full else "empty.png"))


# ─────────────────────────────── Goal gate ───────────────────────────────

def goal_gate():
    # 1 unit = 50 px; the opening is 1.28 x 1.92 units = the 64 x 96 goal area in the game.
    scene = new_scene(160, 240, samples=64)
    ambient(scene, "#2E3238", 0.4)
    ortho_camera(scene, 0, 1.2, 2.4)
    area_light(scene, "Key", (-2, -4, 3), (60, 0, -25), 4, 160)

    inner_half, inner_height, thickness = 0.64, 1.92, 0.16
    radius = inner_half
    spring = inner_height - radius

    # The arch outline as a ring of quads between an inner and an outer curve.
    bm = bmesh.new()
    inner, outer = [], []
    steps = 24
    path = [(-inner_half, 0), (-inner_half, spring)]
    path += [(-math.cos(math.pi * i / steps) * radius, spring + math.sin(math.pi * i / steps) * radius)
             for i in range(1, steps)]
    path += [(inner_half, spring), (inner_half, 0)]
    for x, z in path:
        scale_x = (inner_half + thickness) / inner_half
        inner.append(bm.verts.new((x, 0, z)))
        top_boost = thickness * 1.6 if z > spring else thickness
        ox = x * scale_x
        oz = z if z <= spring else spring + (z - spring) * (radius + top_boost) / radius
        outer.append(bm.verts.new((ox, 0, oz)))
    for i in range(len(path) - 1):
        bm.faces.new((inner[i], inner[i + 1], outer[i + 1], outer[i]))
    mesh = bpy.data.meshes.new("Arch")
    bm.to_mesh(mesh)
    bm.free()
    arch = link(scene, "Arch", mesh, stone_material("GateStone", "#3B3835", bump=0.5))
    solid = arch.modifiers.new("Thick", "SOLIDIFY")
    solid.thickness = 0.3
    add_bevel(arch, 0.03, 2)

    # Light filling the doorway, brightest in the middle.
    light = bpy.data.materials.new("Portal")
    light.use_nodes = True
    nodes, links = light.node_tree.nodes, light.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = 1.6
    coords = nodes.new("ShaderNodeTexCoord")
    gradient = nodes.new("ShaderNodeTexGradient")
    gradient.gradient_type = "SPHERICAL"
    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (1.3, 1.3, 0.95)
    mapping.inputs["Location"].default_value = (0, 0, -0.95 * 0.9)   # brightest point in the middle of the doorway
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = srgb("#E07A1A")
    ramp.color_ramp.elements[1].color = srgb("#FFF3D2")
    links.new(coords.outputs["Object"], mapping.inputs["Vector"])
    links.new(mapping.outputs["Vector"], gradient.inputs["Vector"])
    links.new(gradient.outputs["Color"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs[0], out.inputs[0])

    bm = bmesh.new()
    face = [bm.verts.new((x, 0.12, z)) for x, z in path]
    bm.faces.new(face)
    portal_mesh = bpy.data.meshes.new("Portal")
    bm.to_mesh(portal_mesh)
    bm.free()
    portal = link(scene, "Portal", portal_mesh, light)
    portal.location = (0, 0, 0)

    render(scene, asset_path("sprites", "goal-gate.png"))


# ─────────────────────────────── Checkpoint lantern ───────────────────────────────

def lantern(lit):
    # 1 unit = 200 px; the sprite is 80 x 192 px = 0.4 x 0.96 units (drawn as 40 x 96 in the game).
    scene = new_scene(80, 192, samples=64)
    ambient(scene, "#30353C", 0.5)
    ortho_camera(scene, 0, 0.48, 0.96)
    area_light(scene, "Key", (-1, -2, 1.5), (60, 0, -25), 1.5, 30)
    build_lantern(scene, lit)
    render(scene, asset_path("sprites", "checkpoint", "lit.png" if lit else "unlit.png"))


def build_lantern(scene, lit):
    """Lantern on a post, standing at the origin, 0.96 units tall. Also used by branding.py."""
    iron = principled_material("Iron", srgb("#1E1E20"), metallic=0.6, roughness=0.6)
    link(scene, "Post", box_mesh("Post", 0.05, 0.05, 0.62, (0, 0, 0.31)), iron)
    link(scene, "Foot", box_mesh("Foot", 0.2, 0.12, 0.04, (0, 0, 0.02)), iron)
    link(scene, "Floor", box_mesh("Floor", 0.24, 0.24, 0.03, (0, 0, 0.635)), iron)
    link(scene, "Lid", box_mesh("Lid", 0.26, 0.26, 0.03, (0, 0, 0.885)), iron)
    for x in (-0.11, 0.11):
        link(scene, "Bar", box_mesh("Bar", 0.025, 0.025, 0.24, (x, -0.11, 0.76)), iron)

    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=0.2, radius2=0.0, depth=0.08)
    roof_mesh = bpy.data.meshes.new("Roof")
    bm.to_mesh(roof_mesh)
    bm.free()
    roof = link(scene, "Roof", roof_mesh, iron)
    roof.location = (0, 0, 0.94)
    roof.rotation_euler = (0, 0, math.radians(45))

    if lit:
        glass = principled_material("Glass", srgb("#FFB547"), emission=srgb("#FFA733"), emission_strength=2.5)
        point_light(scene, "Flame", (0, -0.2, 0.76), 3, WARM_LIGHT, radius=0.05)
    else:
        glass = principled_material("Glass", srgb("#3B4048"), roughness=0.15)
    link(scene, "Glass", box_mesh("Glass", 0.2, 0.2, 0.22, (0, 0, 0.76)), glass)


if __name__ == "__main__":
    coin()
    reward_box(full=True)
    reward_box(full=False)
    goal_gate()
    lantern(lit=True)
    lantern(lit=False)
