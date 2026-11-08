"""
The player character: a small, round, ink-black chicken with big glowing eyes.

Renders every animation as 128x128 frames into WhereAmI/Assets/sprites/player/<animation>/NN.png:

* idle     12 frames, loop: breathing, a little head tilt and one blink
* walk     12 frames, loop: stepping legs, body bob, nodding head
* jump      4 frames: 01 take-off, 02 rising, 03 top of the jump, 04 falling
* flutter   6 frames, loop: wings beating hard to slow the fall
* throw     6 frames, once: lean back, then swing the wing forward to throw an egg

The body parts hang on "joints" (empties) that are rotated per frame, so no armature is needed.
The frame shows 1.2 x 1.2 units; the feet stand 0.05 units above the bottom edge.

Run:  blender -b --factory-startup --python tools/blender/character.py
      blender -b --factory-startup --python tools/blender/character.py -- --preview <folder>
"""
import math
import os
import sys

import bmesh
import bpy

sys.path.append(os.path.dirname(__file__))
from common import (  # noqa: E402
    area_light, asset_path, emission_material, new_scene, ortho_camera, principled_material, render, srgb,
)

INK = "#151518"


def ink_material(name="Ink"):
    """Near-black, slightly soft surface so the rim light draws a thin edge around the silhouette."""
    return principled_material(name, srgb(INK), roughness=0.55)


def sphere(scene, name, radius, location, scale=(1, 1, 1), material=None, parent=None, segments=48):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=segments // 2, radius=radius)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    obj.scale = scale
    if material:
        obj.data.materials.append(material)
    scene.collection.objects.link(obj)
    if parent:
        obj.parent = parent
    return obj


def cone(scene, name, radius, depth, location, rotation, material, parent=None, segments=24):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=0.0, depth=depth)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    obj.rotation_euler = tuple(math.radians(a) for a in rotation)
    obj.data.materials.append(material)
    scene.collection.objects.link(obj)
    if parent:
        obj.parent = parent
    return obj


def pivot(scene, name, location, parent=None):
    """An empty used as a joint: rotate it to move everything attached."""
    empty = bpy.data.objects.new(name, None)
    empty.location = location
    scene.collection.objects.link(empty)
    if parent:
        empty.parent = parent
    return empty


def build_chicken(scene):
    """
    Builds the chicken standing on z=0, facing +X (right on screen), about 1.0 unit tall.
    Returns the joints that animations rotate: root, body, head, wing, tail, leg_front, leg_back.
    """
    ink = ink_material()
    beak_mat = principled_material("Beak", srgb("#C9822F"), roughness=0.5)
    eye_white = emission_material("EyeWhite", srgb("#FFF2CF"), strength=1.4)
    pupil = emission_material("Pupil", srgb("#0B0B0C"), strength=0.0)
    comb_mat = principled_material("Comb", srgb("#3A1E18"), roughness=0.6)

    root = pivot(scene, "Root", (0, 0, 0))
    body = pivot(scene, "Body", (0, 0, 0.4), root)
    body.rotation_euler = (0, math.radians(-12), 0)   # chest up, tail down: a proud little chicken

    # A round, slightly egg-shaped body.
    sphere(scene, "Torso", 0.3, (0, 0, 0), (1.15, 0.9, 0.95), ink, body)
    # Scruffy feathers along the back and belly break up the smooth outline.
    for i, (x, z, angle) in enumerate(((-0.2, 0.2, 140), (-0.3, 0.08, 165), (-0.05, 0.27, 120),
                                       (0.05, -0.27, -100), (-0.15, -0.25, -120), (0.22, -0.2, -60))):
        cone(scene, f"Ruffle{i}", 0.05, 0.13, (x, 0, z), (0, -angle + 90, 0), ink, body, segments=10)

    head = pivot(scene, "Head", (0.22, 0, 0.26), body)
    sphere(scene, "Skull", 0.16, (0.02, 0, 0.06), (1.0, 0.92, 1.0), ink, head)
    # A few messy feathers sticking out of the back of the head.
    for i, (x, z, angle) in enumerate(((-0.13, 0.13, 150), (-0.15, 0.04, 175), (-0.1, 0.2, 130))):
        cone(scene, f"Scruff{i}", 0.03, 0.12, (x, 0, z), (0, -angle + 90, 0), ink, head, segments=8)

    # Messy comb: three dark feather tufts on top.
    for i, (x, tilt, size) in enumerate(((-0.05, -25, 0.06), (0.03, -5, 0.08), (0.1, 18, 0.055))):
        cone(scene, f"Comb{i}", 0.035, size * 2.2, (x, 0, 0.22 + size * 0.6), (0, tilt, 0), comb_mat, head)

    # Big glowing eye with a pupil looking forward, and a small warm beak.
    sphere(scene, "Eye", 0.062, (0.1, -0.12, 0.1), (1.0, 0.5, 1.15), eye_white, head)
    sphere(scene, "Pupil", 0.03, (0.135, -0.15, 0.09), (1.0, 0.5, 1.2), pupil, head)
    cone(scene, "Beak", 0.04, 0.13, (0.21, 0, 0.03), (0, 90, 0), beak_mat, head)

    # Wing: a flattened teardrop on the near side, hinged at the shoulder.
    wing = pivot(scene, "Wing", (0.05, -0.28, 0.06), body)
    sphere(scene, "WingShape", 0.15, (-0.1, 0, -0.06), (1.3, 0.3, 0.75), ink, wing)
    for i, (x, z) in enumerate(((-0.27, -0.12), (-0.24, -0.18), (-0.18, -0.21))):
        cone(scene, f"Primary{i}", 0.035, 0.16, (x, 0, z), (0, -110 - i * 12, 0), ink, wing, segments=10)

    # Tail: three feathers fanned up and back.
    tail = pivot(scene, "Tail", (-0.3, 0, 0.06), body)
    for i, angle in enumerate((-60, -40, -20)):
        cone(scene, f"Tail{i}", 0.045, 0.28, (-0.06, 0, 0.1), (0, angle - 90, 0), ink, tail, segments=12)

    # Thin legs with three-toed feet.
    legs = []
    for name, x, y in (("LegFront", 0.07, -0.06), ("LegBack", -0.07, 0.06)):
        leg = pivot(scene, name, (x, y, 0.4 - 0.24), root)
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.022, radius2=0.018, depth=0.17)
        mesh = bpy.data.meshes.new(name + "Mesh")
        bm.to_mesh(mesh)
        bm.free()
        shin = bpy.data.objects.new(name + "Shin", mesh)
        shin.location = (0, 0, -0.08)
        shin.data.materials.append(beak_mat)
        scene.collection.objects.link(shin)
        shin.parent = leg
        for j, toe in enumerate((-30, 0, 30)):
            cone(scene, f"{name}Toe{j}", 0.016, 0.1, (0.04 * math.cos(math.radians(toe)), 0, -0.16),
                 (0, 90 + toe * 0.3, 0), beak_mat, leg, segments=8)
        legs.append(leg)

    eyes = [bpy.data.objects["Eye"], bpy.data.objects["Pupil"]]
    return {"root": root, "body": body, "head": head, "wing": wing, "tail": tail,
            "leg_front": legs[0], "leg_back": legs[1], "eyes": eyes}


def light_scene(scene):
    # Soft cold rim from behind, gentle warm key from the front: the silhouette stays dark
    # but its edges and the round shapes read clearly.
    area_light(scene, "RimLeft", (-1.4, 1.2, 1.2), (-55, 0, 230), 1.5, 160, (0.75, 0.85, 1.0))
    area_light(scene, "RimRight", (1.4, 1.2, 0.9), (-60, 0, 130), 1.5, 90, (1.0, 0.8, 0.55))
    area_light(scene, "Key", (1.2, -2.0, 1.2), (65, 0, 30), 1.5, 20, (1.0, 0.85, 0.7))
    world = scene.world.node_tree.nodes["Background"]
    world.inputs["Color"].default_value = srgb("#3A4048")
    world.inputs["Strength"].default_value = 0.35


def preview(folder):
    scene = new_scene(512, 512, samples=96)
    ortho_camera(scene, 0, 0.55, 1.2)
    light_scene(scene)
    build_chicken(scene)
    render(scene, os.path.join(folder, "character-preview.png"))


# ─────────────────────────────── Animation ───────────────────────────────

REST = {}


def remember_rest(parts):
    for name, obj in parts.items():
        if name != "eyes":
            REST[name] = (tuple(obj.location), tuple(obj.rotation_euler))


def pose(parts, body_y=0.0, body_z=0.0, body_tilt=0.0, head=0.0, head_dx=0.0, wing=0.0,
         tail=0.0, leg_front=0.0, leg_back=0.0, legs_up=0.0, squash=0.0, blink=False):
    """Sets one pose. Angles are in degrees; a positive wing angle lifts the wing tip up behind the back."""
    def place(name, dx=0.0, dz=0.0, angle=0.0):
        (lx, ly, lz), (rx, ry, rz) = REST[name]
        obj = parts[name]
        obj.location = (lx + dx, ly, lz + dz)
        obj.rotation_euler = (rx, ry + math.radians(angle), rz)

    place("body", dz=body_z, angle=body_tilt)
    parts["body"].scale = (1 + squash, 1, 1 - squash)
    place("head", dx=head_dx, angle=head)
    place("wing", angle=wing)
    place("tail", angle=tail)
    place("leg_front", dz=body_z * 0.3 + legs_up, angle=leg_front)
    place("leg_back", dz=body_z * 0.3 + legs_up, angle=leg_back)
    for eye in parts["eyes"]:
        eye.scale = (1, 1, 0.12) if blink else (1, 1, 1)


def wave(i, count, phase=0.0):
    return math.sin(2 * math.pi * (i / count) + phase)


def render_animation(scene, parts, name, frames):
    for i, values in enumerate(frames):
        pose(parts, **values)
        render(scene, asset_path("sprites", "player", name, f"{i + 1:02d}.png"))


def animations():
    scene = new_scene(128, 128, samples=64)
    ortho_camera(scene, 0, 0.55, 1.2)
    light_scene(scene)
    parts = build_chicken(scene)
    remember_rest(parts)

    n = 12
    render_animation(scene, parts, "idle", [dict(
        body_z=0.012 * wave(i, n), squash=0.02 * wave(i, n), head=3 * wave(i, n, 1.0),
        tail=6 * wave(i, n, 0.5), wing=3 * wave(i, n), blink=i in (8, 9)) for i in range(n)])

    render_animation(scene, parts, "walk", [dict(
        body_z=0.025 * abs(wave(i, n)), body_tilt=-3 + 3 * wave(i, n), head_dx=0.02 * wave(i, n),
        head=-4 * wave(i, n), leg_front=32 * wave(i, n), leg_back=-32 * wave(i, n),
        wing=8 * wave(i, n, 1.5), tail=8 * wave(i, n, 0.8)) for i in range(n)])

    render_animation(scene, parts, "jump", [
        dict(squash=0.12, body_z=-0.03, wing=10, leg_front=10, leg_back=-10),             # take-off
        dict(squash=-0.08, body_tilt=-10, wing=75, tail=-10, legs_up=0.05, leg_front=25, leg_back=15),
        dict(body_tilt=-4, wing=40, tail=-5, legs_up=0.04, leg_front=15, leg_back=5),       # top
        dict(squash=-0.04, body_tilt=6, wing=95, tail=12, legs_up=0.02, leg_front=-10, leg_back=-25),
    ])

    m = 6
    render_animation(scene, parts, "flutter", [dict(
        body_z=0.015 * wave(i, m), body_tilt=4, wing=60 + 50 * wave(i, m), tail=15 + 6 * wave(i, m),
        legs_up=0.03, leg_front=-20, leg_back=-30, head=-6) for i in range(m)])

    render_animation(scene, parts, "throw", [
        dict(body_tilt=-10, head=-8, wing=70, tail=-6),                  # wind up: wing lifted back
        dict(body_tilt=-16, head=-12, wing=110, tail=-10, squash=0.03),
        dict(body_tilt=10, head=10, wing=-35, tail=12, squash=-0.04),    # release: wing swings down
        dict(body_tilt=8, head=8, wing=-25, tail=8),
        dict(body_tilt=3, head=3, wing=-10, tail=3),
        dict(body_tilt=0, head=0, wing=5, tail=0),
    ])


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args[:1] == ["--preview"]:
        preview(os.path.abspath(args[1]) if len(args) > 1 else os.getcwd())
    else:
        animations()
