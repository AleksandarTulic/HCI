"""
Renders the game icon and the README banner.

* src/WhereAmI/Assets/icon.png and icon.ico  a lit lantern on a dark rounded tile (window and app icon)
* docs/images/banner.png                      the parallax layers, a lantern and the title

Needs the parallax layers from parallax.py. Run:
    blender -b --factory-startup --python tools/blender/branding.py
"""
import io
import math
import os
import struct
import sys

import bmesh
import bpy

sys.path.append(os.path.dirname(__file__))
from common import (  # noqa: E402
    ROOT, area_light, asset_path, emission_material, new_scene, ortho_camera, point_light, render, srgb,
)
from props import build_lantern, principled_material, text_object  # noqa: E402

AMBER = srgb("#FFB547")


def rounded_tile(scene, size, radius, color_hex, y=0.5, center_z=0.0):
    bm = bmesh.new()
    half = size / 2
    points = []
    for cx, cz, start in ((half - radius, half - radius, 0), (-half + radius, half - radius, 90),
                          (-half + radius, -half + radius, 180), (half - radius, -half + radius, 270)):
        for i in range(9):
            a = math.radians(start + i * 90 / 8)
            points.append((cx + math.cos(a) * radius, y, center_z + cz + math.sin(a) * radius))
    bm.faces.new([bm.verts.new(p) for p in points])
    mesh = bpy.data.meshes.new("Tile")
    bm.to_mesh(mesh)
    bm.free()
    tile = bpy.data.objects.new("Tile", mesh)
    tile.data.materials.append(emission_material("TileColor", srgb(color_hex)))
    scene.collection.objects.link(tile)
    return tile


def glow_disc(scene, radius, center, strength=1.0, y=0.4):
    """A soft round glow made of a disc whose emission fades towards the edge."""
    material = bpy.data.materials.new("Glow")
    material.use_nodes = True
    material.blend_method = "BLEND"
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = AMBER
    emission.inputs["Strength"].default_value = strength
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    mix = nodes.new("ShaderNodeMixShader")
    coords = nodes.new("ShaderNodeTexCoord")
    gradient = nodes.new("ShaderNodeTexGradient")
    gradient.gradient_type = "SPHERICAL"
    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (1 / radius, 1 / radius, 1 / radius)
    power = nodes.new("ShaderNodeMath")
    power.operation = "POWER"
    power.inputs[1].default_value = 2.2
    links.new(coords.outputs["Object"], mapping.inputs["Vector"])
    links.new(mapping.outputs["Vector"], gradient.inputs["Vector"])
    links.new(gradient.outputs["Fac"], power.inputs[0])
    links.new(power.outputs[0], mix.inputs["Fac"])
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(emission.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], out.inputs[0])

    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=64, radius=radius)
    mesh = bpy.data.meshes.new("Glow")
    bm.to_mesh(mesh)
    bm.free()
    disc = bpy.data.objects.new("Glow", mesh)
    disc.data.materials.append(material)
    disc.rotation_euler = (math.radians(90), 0, 0)
    disc.location = (center[0], y, center[1])
    scene.collection.objects.link(disc)
    return disc


def image_plane(scene, path, width, height, x, z, y, brightness=1.0):
    """A flat picture (with transparency) placed in the scene."""
    material = bpy.data.materials.new(os.path.basename(path))
    material.use_nodes = True
    material.blend_method = "BLEND"
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(path)
    texture.extension = "REPEAT"
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = brightness
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    mix = nodes.new("ShaderNodeMixShader")
    links.new(texture.outputs["Color"], emission.inputs["Color"])
    links.new(texture.outputs["Alpha"], mix.inputs["Fac"])
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(emission.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], out.inputs[0])

    bpy.ops.mesh.primitive_plane_add(size=1)
    plane = bpy.context.active_object
    plane.scale = (width, height, 1)
    plane.rotation_euler = (math.radians(90), 0, 0)
    plane.location = (x, y, z)
    plane.data.materials.append(material)
    return plane


def icon():
    scene = new_scene(256, 256, samples=96)
    ortho_camera(scene, 0, 0.5, 1.0)
    area_light(scene, "Key", (-1, -2, 1.5), (60, 0, -25), 1.5, 25)
    rounded_tile(scene, 1.0, 0.2, "#141920", y=0.6, center_z=0.5)
    glow_disc(scene, 0.42, (0, 0.7), strength=1.2, y=0.5)
    lantern_root = bpy.data.objects.new("LanternRoot", None)
    scene.collection.objects.link(lantern_root)
    before = set(scene.collection.objects)
    build_lantern(scene, lit=True)
    for obj in set(scene.collection.objects) - before:
        obj.parent = lantern_root
    lantern_root.scale = (0.82, 0.82, 0.82)
    lantern_root.location = (0, 0, 0.1)
    path = asset_path("icon.png")
    render(scene, path)
    write_ico(path, asset_path("icon.ico"))


def write_ico(png_path, ico_path, sizes=(256, 64, 48, 32, 16)):
    """Windows icon file holding PNG images in several sizes."""
    entries = []
    for size in sizes:
        image = bpy.data.images.load(png_path)
        image.scale(size, size)
        temp = os.path.join(bpy.app.tempdir, f"icon-{size}.png")
        image.filepath_raw = temp
        image.file_format = "PNG"
        image.save()
        with open(temp, "rb") as f:
            entries.append((size, f.read()))

    header = struct.pack("<HHH", 0, 1, len(entries))
    offset = 6 + 16 * len(entries)
    directory, blobs = io.BytesIO(), io.BytesIO()
    for size, data in entries:
        directory.write(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(data), offset))
        blobs.write(data)
        offset += len(data)
    with open(ico_path, "wb") as f:
        f.write(header + directory.getvalue() + blobs.getvalue())
    print("WROTE", os.path.relpath(ico_path, ROOT))


def banner():
    # 1600 x 480 px, 1 unit = 100 px.
    scene = new_scene(1600, 480, samples=64)
    scene.render.film_transparent = False
    background = scene.world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = srgb("#0E1116")
    background.inputs["Strength"].default_value = 1.0
    ortho_camera(scene, 8, 2.4, 16)
    area_light(scene, "Key", (10, -4, 4), (60, 0, 20), 4, 200)

    layers = os.path.join(ROOT, "src", "WhereAmI", "Assets", "backgrounds", "parallax")
    # The same layers as in the game, dimmed to a night-time fog. Two tiles each, so they cover the width.
    for name, y, x_shift, drop, light in (("far", 3.0, -3.0, 1.2, 0.16), ("mid", 2.0, -6.0, 2.4, 0.11),
                                          ("near", 1.0, -9.5, 3.2, 0.07)):
        for tile in (0, 1):
            image_plane(scene, os.path.join(layers, f"{name}.png"), 20.48, 10.24,
                        8 + x_shift + tile * 20.48, 5.12 - drop - 1.2, y, brightness=light)

    # Dark ground strip in front, like the levels.
    ground = bpy.data.objects.new("Ground", bpy.data.meshes.new("Ground"))
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=(17, 0.2, 0.5), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(8, 0.5, 0.05), verts=bm.verts)
    bm.to_mesh(ground.data)
    bm.free()
    ground.data.materials.append(emission_material("Ink", srgb("#161617")))
    scene.collection.objects.link(ground)

    # A lantern on the right with its glow.
    glow_disc(scene, 2.2, (12.6, 2.4), strength=1.0, y=0.6)
    lantern_root = bpy.data.objects.new("LanternRoot", None)
    scene.collection.objects.link(lantern_root)
    before = set(scene.collection.objects)
    build_lantern(scene, lit=True)
    for obj in set(scene.collection.objects) - before:
        obj.parent = lantern_root
    lantern_root.scale = (3.4, 3.4, 3.4)
    lantern_root.location = (12.6, 0, 0.25)
    point_light(scene, "LanternGlow", (12.6, -1.0, 2.9), 120, (1, 0.72, 0.35), radius=0.3)

    title_material = principled_material("Title", srgb("#FFD58A"), emission=srgb("#FFC46A"), emission_strength=1.4)
    title = text_object(scene, "Where Am I?", "Gabriola.ttf", 2.6, 0.0, title_material)
    title.rotation_euler = (math.radians(90), 0, 0)
    title.location = (6.0, 0, 2.75)

    subtitle = text_object(scene, "A small light in a very big fog.", "constani.ttf", 0.42, 0.0,
                           emission_material("Subtitle", srgb("#9AA3AE")))
    subtitle.rotation_euler = (math.radians(90), 0, 0)
    subtitle.location = (6.0, 0, 1.45)

    render(scene, os.path.join(ROOT, "docs", "images", "banner.png"))


icon()
banner()
