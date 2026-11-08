"""
Renders the enemy and projectile sprites as dark storybook silhouettes with a cold rim light
and small warm-glowing details. Every creature faces right (the game mirrors it when it walks left).

* sprites/enemies/moth/fly/01..08.png      ember moth flapping its wings, a glowing lantern belly (128x128)
* sprites/enemies/owl/idle/01..06.png      wind owl on a branch, breathing and blinking once (160x160)
* sprites/enemies/owl/flap/01..08.png      the same owl fanning its wings to blow wind (160x160)
* sprites/enemies/burr/walk/01..08.png     thorn burr, a hedgehog-like walker with amber-tipped thorns (128x96)
* sprites/enemies/toad/idle/01..04.png     bog toad, throat pulsing (128x112)
* sprites/enemies/toad/jump/01..04.png     crouch, rising, falling, landing (128x112)
* sprites/projectiles/ember/01..04.png     a flickering ball of amber fire with a dark core (64x64)
* sprites/projectiles/egg.png              the egg the player throws (64x80)
* sprites/projectiles/eggshell/01..03.png  three shell fragments (32x32)

Creatures are built from primitives; body parts hang off empties and are posed in Python per frame.
Every sprite uses the same scale, 1 Blender unit = 100 px. Walkers stand on the bottom edge of the
frame, flying things are centred.

Run:  blender -b --factory-startup --python tools/blender/enemies.py
      blender -b --factory-startup --python tools/blender/enemies.py -- moth owl   (only some assets)
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.append(os.path.dirname(__file__))
from common import (  # noqa: E402
    area_light, asset_path, new_scene, ortho_camera, point_light, principled_material, render, srgb,
)
from props import ambient  # noqa: E402

PX = 100                           # pixels per Blender unit
AMBER = srgb("#FFB547")
WARM_LIGHT = (1.0, 0.72, 0.35)
COLD_LIGHT = (0.74, 0.84, 1.0)


# ─────────────────────────────── Scene, lights, materials ───────────────────────────────

def sprite_scene(width_px, height_px, standing, samples=48):
    """Transparent sprite scene. Standing sprites have z = 0 on the bottom edge, others are centred on 0."""
    scene = new_scene(width_px, height_px, samples)
    center_z = height_px / 2 / PX if standing else 0.0
    ortho_camera(scene, 0, center_z, max(width_px, height_px) / PX)
    ambient(scene, "#9AA3AD", 0.25)
    return scene


def aim(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def silhouette_lights(scene, center, reach, rim=1.0):
    """A dim key from the front and two cold rim lights behind the subject that outline its edges."""
    cx, cz = center
    key = area_light(scene, "Key", (cx - 1.5 * reach, -3 * reach, cz + 2 * reach), (0, 0, 0), 2 * reach, 40 * reach ** 2)
    left = area_light(scene, "RimL", (cx - 1.6 * reach, 2.2 * reach, cz + 1.4 * reach), (0, 0, 0), 1.2 * reach,
                      260 * rim * reach ** 2, COLD_LIGHT)
    right = area_light(scene, "RimR", (cx + 1.8 * reach, 2.2 * reach, cz + 0.6 * reach), (0, 0, 0), 1.2 * reach,
                       180 * rim * reach ** 2, COLD_LIGHT)
    for light in (key, left, right):
        aim(light, (cx, 0, cz))


def ink(name="Ink", hex_color="#141416", roughness=0.42):
    """The dark, slightly glossy material every creature is made of."""
    return principled_material(name, srgb(hex_color), roughness=roughness)


def glow(name, hex_color, strength=1.0):
    """Self-lit detail (eyes, belly, thorn tips) that keeps its exact colour."""
    color = srgb(hex_color)
    return principled_material(name, color, roughness=0.6, emission=color, emission_strength=strength)


def soft_glow(name, hex_color, strength, softness=0.45):
    """Emission that fades to transparent towards the silhouette edge: a soft ball of light."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = srgb(hex_color)
    emission.inputs["Strength"].default_value = strength
    clear = nodes.new("ShaderNodeBsdfTransparent")
    facing = nodes.new("ShaderNodeLayerWeight")
    facing.inputs["Blend"].default_value = softness
    mix = nodes.new("ShaderNodeMixShader")
    links.new(facing.outputs["Facing"], mix.inputs["Fac"])
    links.new(emission.outputs[0], mix.inputs[1])
    links.new(clear.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], out.inputs[0])
    return material


# ─────────────────────────────── Building blocks ───────────────────────────────

def empty(scene, name, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    return obj


def mesh_object(scene, name, bm, material, parent=None, location=(0, 0, 0), rotation=(0, 0, 0),
                scale=(1, 1, 1), smooth=True):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    if smooth:
        mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    obj = bpy.data.objects.new(name, mesh)
    obj.data.materials.append(material)
    scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.rotation_euler = tuple(math.radians(a) for a in rotation)
    obj.scale = scale
    return obj


def sphere(scene, name, radius, material, parent=None, location=(0, 0, 0), scale=(1, 1, 1), rotation=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=radius)
    return mesh_object(scene, name, bm, material, parent, location, rotation, scale)


def cone_mesh(radius1, radius2, length):
    """Cone/cylinder along +Z from z = 0 to z = length."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=radius1, radius2=radius2, depth=length)
    bmesh.ops.translate(bm, vec=(0, 0, length / 2), verts=bm.verts)
    return bm


def point_along(obj, start, end):
    """Place an object built along +Z so it starts at `start` and points to `end` (parent space)."""
    start, end = Vector(start), Vector(end)
    obj.location = start
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = (end - start).to_track_quat("Z", "Y")


def spike(scene, name, start, end, radius1, radius2, material, parent=None):
    length = (Vector(end) - Vector(start)).length
    obj = mesh_object(scene, name, cone_mesh(radius1, radius2, length), material, parent)
    point_along(obj, start, end)
    return obj


def flat_shape(scene, name, points, material, parent=None, plane="XY", thickness=0.012, location=(0, 0, 0)):
    """A thin plate with the given 2D outline, in the XY or XZ plane of its parent."""
    bm = bmesh.new()
    if plane == "XY":
        verts = [bm.verts.new((u, v, 0)) for u, v in points]
    else:
        verts = [bm.verts.new((u, 0, v)) for u, v in points]
    bm.faces.new(verts)
    obj = mesh_object(scene, name, bm, material, parent, location, smooth=False)
    solid = obj.modifiers.new("Thick", "SOLIDIFY")
    solid.thickness = thickness
    solid.offset = 0
    return obj


def ellipse(cx, cy, ax, ay, rotation_deg=0, steps=28):
    r = math.radians(rotation_deg)
    pts = []
    for i in range(steps):
        a = 2 * math.pi * i / steps
        x, y = ax * math.cos(a), ay * math.sin(a)
        pts.append((cx + x * math.cos(r) - y * math.sin(r), cy + x * math.sin(r) + y * math.cos(r)))
    return pts


class Limb:
    """A chain of rounded rods through a list of joints, re-posed each frame with set()."""

    def __init__(self, scene, name, radii, material, parent=None):
        self.rods, self.joints, self.radii = [], [], radii
        unit = cone_mesh(1, 1, 1)
        rod_mesh = bpy.data.meshes.new(name + "Rod")
        unit.to_mesh(rod_mesh)
        unit.free()
        rod_mesh.polygons.foreach_set("use_smooth", [True] * len(rod_mesh.polygons))
        rod_mesh.materials.append(material)
        for i in range(len(radii) - 1):
            rod = bpy.data.objects.new(f"{name}Rod{i}", rod_mesh)
            scene.collection.objects.link(rod)
            rod.parent = parent
            self.rods.append(rod)
        for i, r in enumerate(radii):
            self.joints.append(sphere(scene, f"{name}Joint{i}", r, material, parent))

    def set(self, points):
        for joint, p in zip(self.joints, points):
            joint.location = p
        for i, rod in enumerate(self.rods):
            a, b = Vector(points[i]), Vector(points[i + 1])
            point_along(rod, a, b)
            r = (self.radii[i] + self.radii[i + 1]) / 2
            rod.scale = (r, r, (b - a).length)


def frames(scene, folder, count, pose):
    """Call pose(i, t) for every frame (t runs 0..1 over the loop) and render folder/01.png ..."""
    for i in range(count):
        pose(i, i / count)
        render(scene, asset_path(*folder, f"{i + 1:02d}.png"))


# ─────────────────────────────── Ember moth ───────────────────────────────

def ember_moth():
    scene = sprite_scene(128, 128, standing=False)
    silhouette_lights(scene, (0, 0), 1.0)
    body_ink, wing_ink = ink(), ink("WingInk", "#18181B", 0.55)
    belly = soft_glow("Belly", "#FFB547", 1.0, 0.5)
    eye = glow("MothEye", "#E8F1FF", 1.0)
    spot = glow("WingSpot", "#FFB547", 0.6)

    root = empty(scene, "Moth")
    root.rotation_euler = (0, 0, math.radians(-22))
    root.scale = (1.2, 1.2, 1.2)   # head turned a little towards the camera
    sphere(scene, "Thorax", 0.1, body_ink, root, (0, 0, 0), (1.15, 1, 1))
    sphere(scene, "Head", 0.072, body_ink, root, (0.14, 0, 0.03))
    for side in (-1, 1):
        sphere(scene, "Eye", 0.028, eye, root, (0.19, side * 0.04, 0.05))

    # Lantern belly: a soft round orb of amber light held under the body, with a hotter centre.
    orb = (-0.13, -0.02, -0.1)
    sphere(scene, "Belly", 0.085, belly, root, orb)
    sphere(scene, "BellyHeart", 0.05, soft_glow("BellyHeart", "#FFE2A0", 1.2, 0.6), root,
           (orb[0] + 0.01, orb[1] - 0.06, orb[2] + 0.01))
    sphere(scene, "BellyHalo", 0.125, soft_glow("BellyHalo", "#FFB547", 0.8, 0.3), root,
           (orb[0], orb[1] + 0.05, orb[2]))
    point_light(scene, "BellyGlow", (-0.15, -0.25, -0.2), 2.0, WARM_LIGHT, radius=0.05)

    # Feathery antennae: a curved stalk with barbs that get shorter towards the tip.
    for side in (-1, 1):
        base = Vector((0.17, side * 0.03, 0.08))
        pts = [base + Vector((0.2 * t - 0.02 * t * t, 0, 0.17 * t - 0.05 * t * t)) for t in (i / 6 for i in range(7))]
        for i in range(6):
            spike(scene, "Stalk", pts[i], pts[i + 1], 0.008, 0.006, body_ink, root)
            along = (pts[i + 1] - pts[i]).normalized()
            across = Vector((along.z, 0, -along.x))
            barb = 0.04 * (1 - i / 7)
            for sign in (-1, 1):
                spike(scene, "Barb", pts[i + 1], pts[i + 1] + across * sign * barb - along * barb * 0.5,
                      0.007, 0.003, body_ink, root)

    # Thin legs tucked under the thorax.
    for x in (0.06, 0.0, -0.06):
        spike(scene, "Leg", (x, -0.03, -0.06), (x + 0.05, -0.05, -0.17), 0.009, 0.005, body_ink, root)

    # Wings hinge on top of the thorax and rotate about the body axis.
    wings = []
    for side in (-1, 1):
        hinge = empty(scene, "Hinge", (0.0, side * 0.02, 0.06), root)
        fore = ellipse(-0.01, side * 0.27, 0.14, 0.27, side * 18, steps=40)
        hind = ellipse(-0.11, side * 0.19, 0.13, 0.18, side * 40, steps=40)
        flat_shape(scene, "Forewing", fore, wing_ink, hinge)
        flat_shape(scene, "Hindwing", hind, wing_ink, hinge, location=(0, 0, -0.004))
        for z in (0.008, -0.008):
            flat_shape(scene, "Spot", ellipse(-0.04, side * 0.36, 0.026, 0.026), spot, hinge, thickness=0.002,
                       location=(0, 0, z))
        wings.append((hinge, side))

    def pose(i, t):
        phase = 2 * math.pi * t
        lift = 32 + 56 * math.cos(phase)            # wings up at frame 1, down at frame 5
        for hinge, side in wings:
            hinge.rotation_euler = (math.radians(side * lift), 0, 0)
        root.location = (0, 0, -0.12 - 0.03 * math.cos(phase))

    frames(scene, ("sprites", "enemies", "moth", "fly"), 8, pose)


# ─────────────────────────────── Wind owl ───────────────────────────────

def owl_feather_wing(scene, name, material, parent, length=0.4):
    """Fan-shaped open wing in the XZ plane, pointing up (+Z) from the shoulder, with scalloped tips."""
    pts = [(-0.06, 0.0)]
    steps = 40
    for i in range(steps + 1):
        u = i / steps
        a = math.radians(-30 + 60 * u)
        r = length + 0.06 * abs(math.sin(5 * math.pi * u)) + 0.05 * math.sin(math.pi * u)
        pts.append((r * math.sin(a), r * math.cos(a)))
    pts.append((0.06, 0.0))
    return flat_shape(scene, name, pts, material, parent, plane="XZ", thickness=0.02)


def wind_owl():
    scene = sprite_scene(160, 160, standing=True)
    silhouette_lights(scene, (0, 0.7), 1.2)
    feathers = ink("Feathers", "#141416", 0.5)
    bark = ink("Bark", "#19161A", 0.7)
    eye_glow = glow("OwlEye", "#FFE38A", 1.1)

    # Branch stub, cut off on both ends, with a little twig.
    spike(scene, "Branch", (-0.62, 0, 0.075), (0.6, 0, 0.09), 0.07, 0.055, bark)
    sphere(scene, "Cut", 0.07, bark, location=(-0.62, 0, 0.075), scale=(0.3, 1, 1))
    spike(scene, "Twig", (0.38, 0, 0.1), (0.52, -0.02, 0.26), 0.025, 0.008, bark)

    owl = empty(scene, "Owl", (0, 0, 0.14))
    body = empty(scene, "Body", (0, 0, 0), owl)
    sphere(scene, "Torso", 1, feathers, body, (0, 0, 0.37), (0.33, 0.3, 0.38))
    spike(scene, "Tail", (-0.2, 0, 0.12), (-0.36, 0, -0.02), 0.09, 0.02, feathers, body)
    for y in (-0.09, 0.09):
        for dx in (-0.03, 0.0, 0.03):
            spike(scene, "Talon", (0.04, y, 0.02), (0.1 + dx, y + dx, -0.04), 0.022, 0.006, feathers, owl)

    folded = []
    for side in (-1, 1):
        folded.append(sphere(scene, "FoldedWing", 1, feathers, body, (-0.07, side * 0.21, 0.36),
                             (0.24, 0.1, 0.3)))
        folded[-1].rotation_euler = (math.radians(side * -6), math.radians(-28), 0)

    # Head turned towards the viewer so both big eyes show; the body faces right.
    neck = empty(scene, "Neck", (0.02, 0, 0.76), body)
    head = empty(scene, "Head", (0, 0, 0), neck)
    head.rotation_euler = (0, 0, math.radians(-62))
    radius = 0.27
    sphere(scene, "Skull", radius, feathers, head, (0, 0, 0.0), (1, 1, 0.92))
    eyes = []
    for side in (-1, 1):
        a = math.radians(side * 36)
        d = Vector((math.cos(a), math.sin(a), 0))
        holder = empty(scene, "EyeHolder", d * radius * 0.9 + Vector((0, 0, 0.03)), head)
        holder.rotation_euler = (0, 0, a)
        eye = empty(scene, "Eye", (0, 0, 0), holder)
        sphere(scene, "Iris", 0.105, eye_glow, eye, (0, 0, 0), (0.3, 1, 1))
        sphere(scene, "Pupil", 0.045, feathers, eye, (0.03, 0, 0), (0.3, 1, 1))
        eyes.append(eye)
        # Ear tufts lean outwards.
        tuft_base = Vector((math.cos(a) * 0.12, math.sin(a) * 0.17, 0.17))
        spike(scene, "Tuft", tuft_base, tuft_base + Vector((-0.04, side * 0.05, 0.18)), 0.06, 0.008, feathers, head)
    spike(scene, "Beak", (radius * 0.9, 0, -0.03), (radius + 0.06, 0, -0.1), 0.035, 0.004, feathers, head)
    # A soft V-shaped brow between the eyes gives the face a stern little look.
    for side in (-1, 1):
        a = math.radians(side * 20)
        start = Vector((math.cos(a), math.sin(a), 0)) * radius * 0.98 + Vector((0, 0, 0.05))
        end = Vector((math.cos(a * 3), math.sin(a * 3), 0)) * radius * 0.92 + Vector((0, 0, 0.13))
        spike(scene, "Brow", start, end, 0.03, 0.02, feathers, head)

    # Open wings for the flap animation, not turned with the body so they read clearly.
    open_wings = []
    for side in (-1, 1):
        shoulder = empty(scene, "Shoulder", (side * 0.2, 0.08, 0.6), body)
        owl_feather_wing(scene, "Wing", feathers, shoulder)
        open_wings.append((shoulder, side))

    def show(flapping):
        for obj in folded:
            obj.hide_render = flapping
        for shoulder, _ in open_wings:
            for child in shoulder.children:
                child.hide_render = not flapping

    def idle(i, t):
        breath = math.sin(2 * math.pi * t)
        body.scale = (1 + 0.012 * breath, 1, 1 + 0.025 * breath)
        neck.location = (0.02, 0, 0.76 + 0.015 * breath)
        blink = {3: 0.12, 4: 0.55}.get(i, 1.0)
        for eye in eyes:
            eye.scale = (1, 1, blink)

    def flap(i, t):
        phase = 2 * math.pi * t
        beat = 72 - 52 * math.cos(phase)            # raised high at frame 1, swept down at frame 5
        for shoulder, side in open_wings:
            shoulder.rotation_euler = (0, math.radians(side * beat), 0)
        body.rotation_euler = (0, math.radians(3 - 3 * math.cos(phase)), 0)
        body.scale = (1, 1, 1 - 0.03 * math.cos(phase))
        neck.location = (0.02, 0, 0.76)
        for eye in eyes:
            eye.scale = (1, 1, 1)

    show(False)
    frames(scene, ("sprites", "enemies", "owl", "idle"), 6, idle)
    show(True)
    body.scale = (1, 1, 1)
    frames(scene, ("sprites", "enemies", "owl", "flap"), 8, flap)


# ─────────────────────────────── Thorn burr ───────────────────────────────

def thorn_burr():
    scene = sprite_scene(128, 96, standing=True)
    silhouette_lights(scene, (0, 0.4), 0.9)
    hide = ink("Hide", "#141416", 0.5)
    tip = glow("ThornTip", "#FFB547", 0.7)
    eye_glow = glow("BurrEye", "#FFC766", 1.2)

    root = empty(scene, "Burr")
    body = empty(scene, "Body", (0, 0, 0), root)
    a, b, c = 0.3, 0.22, 0.2
    center = Vector((-0.02, 0, 0.3))
    sphere(scene, "Back", 1, hide, body, center, (a, b, c))
    spike(scene, "Snout", (0.18, 0, 0.29), (0.42, 0, 0.22), 0.12, 0.03, hide, body)
    sphere(scene, "Nose", 0.03, hide, body, (0.42, 0, 0.22))
    sphere(scene, "Eye", 0.026, eye_glow, body, (0.27, -0.09, 0.32))
    sphere(scene, "EyeFar", 0.026, eye_glow, body, (0.27, 0.09, 0.32))

    # Thorns spread over the back (Fibonacci points), swept backwards, with faintly glowing tips.
    thorn_mesh = bpy.data.meshes.new("Thorn")
    bm = cone_mesh(0.034, 0.011, 0.13)
    bm.to_mesh(thorn_mesh)
    bm.free()
    thorn_mesh.materials.append(hide)
    tip_mesh = bpy.data.meshes.new("Tip")
    bm = cone_mesh(0.011, 0.0, 0.05)
    bm.to_mesh(tip_mesh)
    bm.free()
    tip_mesh.materials.append(tip)
    count = 140
    golden = math.pi * (3 - math.sqrt(5))
    for k in range(count):
        z = 1 - 2 * (k + 0.5) / count
        r = math.sqrt(1 - z * z)
        x, y = r * math.cos(golden * k), r * math.sin(golden * k)
        if z < -0.05 or x > 0.45:
            continue
        surface = center + Vector((x * a, y * b, z * c)) * 0.9
        normal = Vector((x / a, y / b, z / c)).normalized()
        direction = (normal + Vector((-0.75, 0, 0.1))).normalized()
        length = 0.13 * (0.85 + 0.3 * ((k * 7) % 5) / 4)
        for mesh, offset, scale in ((thorn_mesh, 0, length / 0.13), (tip_mesh, length, 1)):
            obj = bpy.data.objects.new("Thorn", mesh)
            scene.collection.objects.link(obj)
            obj.parent = body
            start = surface + direction * offset
            point_along(obj, start, start + direction)
            obj.scale = (1, 1, scale)

    legs = []
    for x, side, offset in ((0.17, -1, 0.0), (-0.17, 1, 0.0), (0.17, 1, 0.5), (-0.17, -1, 0.5)):
        legs.append((Limb(scene, "Leg", (0.04, 0.035, 0.03), hide, root), Vector((x, side * 0.12, 0.17)), offset))

    def pose(i, t):
        body.location = (0, 0, 0.012 * math.cos(4 * math.pi * t))
        body.rotation_euler = (0, math.radians(2 * math.sin(2 * math.pi * t)), 0)
        for limb, hip, offset in legs:
            phase = 2 * math.pi * (t + offset)
            lift = max(0.0, math.cos(phase)) * 0.05
            foot = Vector((hip.x + 0.08 * math.sin(phase), hip.y, 0.03 + lift))
            knee = (hip + foot) / 2 + Vector((0.02, 0, 0))
            limb.set([hip + Vector((0, 0, body.location.z)), knee, foot])

    frames(scene, ("sprites", "enemies", "burr", "walk"), 8, pose)


# ─────────────────────────────── Bog toad ───────────────────────────────

# Poses: body location (x, z), pitch in degrees (nose up > 0), scale (x, z),
# hind leg hip/knee/ankle/toe and front leg shoulder/elbow/hand as (x, z) points.
TOAD_POSES = {
    "sit":     dict(body=(0, 0.24), pitch=0, scale=(1, 1),
                    hind=((-0.2, 0.18), (0.02, 0.16), (-0.17, 0.05), (0.02, 0.03)),
                    front=((0.17, 0.16), (0.22, 0.09), (0.25, 0.03))),
    "crouch":  dict(body=(0, 0.19), pitch=6, scale=(1.08, 0.86),
                    hind=((-0.2, 0.13), (0.03, 0.12), (-0.17, 0.04), (0.03, 0.03)),
                    front=((0.18, 0.12), (0.25, 0.07), (0.28, 0.03))),
    "rising":  dict(body=(0.06, 0.56), pitch=30, scale=(0.92, 1.12),
                    hind=((-0.17, 0.42), (-0.32, 0.27), (-0.42, 0.13), (-0.54, 0.04)),
                    front=((0.12, 0.45), (0.08, 0.36), (0.02, 0.33))),
    "falling": dict(body=(0, 0.5), pitch=-12, scale=(1, 1),
                    hind=((-0.19, 0.42), (0.02, 0.38), (-0.16, 0.3), (0.04, 0.28)),
                    front=((0.16, 0.4), (0.27, 0.33), (0.33, 0.24))),
    "landing": dict(body=(0, 0.17), pitch=-3, scale=(1.18, 0.74),
                    hind=((-0.24, 0.12), (-0.02, 0.09), (-0.25, 0.04), (0.0, 0.03)),
                    front=((0.21, 0.12), (0.3, 0.07), (0.36, 0.03))),
}


def bog_toad():
    scene = sprite_scene(128, 112, standing=True)
    silhouette_lights(scene, (0, 0.4), 0.9)
    skin = ink("Skin", "#141416", 0.38)
    eye_glow = glow("ToadEye", "#FFB547", 1.1)

    root = empty(scene, "Toad")
    root.rotation_euler = (0, 0, math.radians(-14))   # a little towards the camera so both eyes show
    body = empty(scene, "Body", (0, 0, 0.24), root)
    sphere(scene, "Back", 1, skin, body, (-0.04, 0, 0), (0.3, 0.25, 0.19))
    sphere(scene, "HeadMass", 1, skin, body, (0.17, 0, 0.03), (0.19, 0.2, 0.14))
    for x, y, z in ((-0.12, -0.1, 0.15), (-0.2, 0.02, 0.15), (-0.03, -0.14, 0.15), (-0.1, 0.12, 0.15)):
        sphere(scene, "Wart", 0.035, skin, body, (x, y, z))
    throat = sphere(scene, "Throat", 0.09, skin, body, (0.22, 0, -0.08), (1, 1.3, 1))
    for side in (-1, 1):
        sphere(scene, "EyeDome", 0.068, skin, body, (0.2, side * 0.11, 0.15))
        sphere(scene, "Eye", 0.048, eye_glow, body, (0.235, side * 0.13, 0.17))
        sphere(scene, "Pupil", 0.022, skin, body, (0.276, side * 0.13, 0.172), (0.4, 1.4, 0.55))

    limbs = []
    for side in (-1, 1):
        hind = Limb(scene, "Hind", (0.07, 0.05, 0.035, 0.03), skin, root)
        front = Limb(scene, "Front", (0.04, 0.032, 0.03), skin, root)
        limbs.append((side * 0.2, hind, front))

    def set_pose(name, throat_scale=1.0, breath=0.0):
        p = TOAD_POSES[name]
        body.location = (p["body"][0], 0, p["body"][1] + breath)
        body.rotation_euler = (0, math.radians(-p["pitch"]), 0)
        body.scale = (p["scale"][0], 1, p["scale"][1])
        throat.scale = (throat_scale, 1.3 * throat_scale, throat_scale)
        for y, hind, front in limbs:
            hind.set([(x, y, z) for x, z in p["hind"]])
            front.set([(x, y * 0.8, z) for x, z in p["front"]])

    pulse = (0.85, 1.05, 1.25, 1.05)
    frames(scene, ("sprites", "enemies", "toad", "idle"), 4,
           lambda i, t: set_pose("sit", pulse[i], 0.006 * math.sin(2 * math.pi * t)))
    jump = ("crouch", "rising", "falling", "landing")
    frames(scene, ("sprites", "enemies", "toad", "jump"), 4, lambda i, t: set_pose(jump[i]))


# ─────────────────────────────── Ember projectile ───────────────────────────────

def ember():
    """A small teardrop fireball flying towards +X: hot pale-yellow head, amber body, tongues trailing left."""
    scene = sprite_scene(64, 64, standing=False, samples=64)
    halo = soft_glow("Halo", "#FF9A2E", 0.9, 0.3)
    body = soft_glow("Body", "#FFB547", 1.3, 0.5)
    hot = soft_glow("Hot", "#FFF0BE", 1.4, 0.65)

    def teardrop(name, width, length, material, x, y):
        bm = egg_bmesh(width, length)
        obj = mesh_object(scene, name, bm, material, location=(x, y, 0), rotation=(0, -90, 0))
        return obj   # the narrow end of the egg points backwards (-X)

    outer = teardrop("Halo", 0.3, 0.48, halo, -0.04, 0.3)
    middle = teardrop("Body", 0.22, 0.36, body, -0.01, 0.1)
    teardrop("Hot", 0.13, 0.2, hot, 0.05, -0.1)

    # Flame tongues lick backwards (-X); each frame stretches and sways them differently.
    tongue_mat = soft_glow("Tongue", "#FFA53A", 1.1, 0.4)
    tongues = [sphere(scene, "Tongue", 1, tongue_mat, location=(0, 0.2 + 0.02 * k, 0)) for k in range(4)]
    angles = (160, 180, 200, 145)
    flicker = ((1.0, 0.8, 1.1, 0.7), (0.8, 1.1, 0.9, 1.0), (1.1, 0.9, 0.75, 0.9), (0.9, 1.05, 1.0, 0.8))

    def pose(i, t):
        for k, tongue in enumerate(tongues):
            a = math.radians(angles[k] + 7 * math.sin(2 * math.pi * t + k))
            length = 0.12 * flicker[i][k]
            d = Vector((math.cos(a), 0, math.sin(a)))
            tongue.location = Vector((-0.06, tongue.location.y, 0)) + d * (0.08 + length * 0.5)
            tongue.rotation_euler = (0, -a, 0)
            tongue.scale = (length, 0.04, 0.032)
        s = 1 + 0.05 * math.sin(2 * math.pi * t * 2)
        middle.scale = (s, s, s)
        outer.scale = (2 - s, 2 - s, 2 - s)

    frames(scene, ("sprites", "projectiles", "ember"), 4, pose)


# ─────────────────────────────── Egg and shell ───────────────────────────────

def egg_bmesh(width, height, segments=48, rings=24):
    """An egg: a sphere that narrows towards the top."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1)
    for v in bm.verts:
        narrow = 1 - 0.16 * v.co.z
        v.co.x *= width / 2 * narrow
        v.co.y *= width / 2 * narrow
        v.co.z *= height / 2
    return bm


def egg_lights(scene, reach):
    ambient(scene, "#B9C2CC", 0.5)
    key = area_light(scene, "Key", (-1.2 * reach, -2.5 * reach, 1.6 * reach), (0, 0, 0), 1.2 * reach,
                     90 * reach ** 2, (1.0, 0.95, 0.86))
    rim = area_light(scene, "Rim", (1.5 * reach, 2 * reach, 1.0 * reach), (0, 0, 0), reach,
                     110 * reach ** 2, COLD_LIGHT)
    for light in (key, rim):
        aim(light, (0, 0, 0))


def egg():
    scene = sprite_scene(64, 80, standing=False, samples=64)
    egg_lights(scene, 1.0)
    shell = principled_material("Shell", srgb("#F2E7CF"), roughness=0.55)
    obj = mesh_object(scene, "Egg", egg_bmesh(0.46, 0.64), shell)
    obj.rotation_euler = (0, math.radians(8), 0)
    render(scene, asset_path("sprites", "projectiles", "egg.png"))


def eggshell():
    # (centre of the piece on the egg as yaw/pitch, angular radius, jaggedness, teeth, view tilt);
    # the third piece is turned around so its pale inside shows.
    pieces = ((0, 10, 26, 6, 5, (25, 0, 15)), (40, -20, 23, 6, 4, (-35, 0, 140)), (-30, 30, 21, 5, 6, (150, 10, 30)))
    for index, (yaw, pitch, size, jag, teeth, view) in enumerate(pieces):
        scene = sprite_scene(32, 32, standing=False, samples=64)
        egg_lights(scene, 0.4)
        outer = principled_material("Shell", srgb("#F2E7CF"), roughness=0.55)
        inner = principled_material("ShellInside", srgb("#D8C8A8"), roughness=0.8)

        bm = egg_bmesh(0.46, 0.64, segments=128, rings=64)
        y, p = math.radians(yaw), math.radians(pitch)
        centre = Vector((math.cos(p) * math.cos(y), math.cos(p) * math.sin(y), math.sin(p)))
        side = centre.cross(Vector((0, 0, 1))).normalized()
        up = side.cross(centre)
        doomed = []
        for face in bm.faces:
            d = face.calc_center_median().normalized()
            angle = math.degrees(centre.angle(d))
            around = math.atan2(d.dot(up), d.dot(side))
            zigzag = abs(((around * teeth / math.pi) % 2) - 1) * 2 - 1     # triangle wave -1..1
            if angle > size + jag * zigzag:
                doomed.append(face)
        bmesh.ops.delete(bm, geom=doomed, context="FACES")
        # Centre the piece and turn its outside towards the camera (-Y).
        bmesh.ops.translate(bm, vec=-sum((v.co for v in bm.verts), Vector()) / len(bm.verts), verts=bm.verts)
        turn = centre.rotation_difference(Vector((0, -1, 0))).to_matrix()
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=turn, verts=bm.verts)
        obj = mesh_object(scene, "Shard", bm, outer)
        obj.data.materials.append(inner)
        solid = obj.modifiers.new("Thick", "SOLIDIFY")
        solid.thickness = 0.012
        solid.material_offset = 1
        obj.rotation_euler = tuple(math.radians(v) for v in view)
        render(scene, asset_path("sprites", "projectiles", "eggshell", f"{index + 1:02d}.png"))


ASSETS = {
    "moth": ember_moth, "owl": wind_owl, "burr": thorn_burr, "toad": bog_toad,
    "ember": ember, "egg": egg, "eggshell": eggshell,
}

if __name__ == "__main__":
    wanted = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(ASSETS)
    for key in wanted:
        ASSETS[key]()
