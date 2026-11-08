"""
Renders the three parallax background layers (far, mid, near) as horizontally tileable PNGs.

Every layer is a set of flat silhouettes (mountains, pine forest, bare trees and reeds).
Shapes are generated from periodic functions and wrapped at the edges, so the left and right
borders of each image line up and the layer can repeat forever. A vertical gradient makes the
silhouettes paler near the ground, as if standing in fog.

Run:  blender -b --factory-startup --python tools/blender/parallax.py
"""
import math
import os
import random
import sys

import bmesh
import bpy

sys.path.append(os.path.dirname(__file__))
from common import asset_path, emission_material, new_scene, ortho_camera, render, srgb  # noqa: E402

WIDTH_PX, HEIGHT_PX = 2048, 1024
PX_PER_UNIT = 100
W = WIDTH_PX / PX_PER_UNIT      # 20.48 units: one full tile
H = HEIGHT_PX / PX_PER_UNIT     # 10.24 units


def fog_material(name, top_hex, bottom_hex, fog_height):
    """Emission colour that blends from bottom_hex (at z=0) to top_hex (at z=fog_height)."""
    material = emission_material(name, srgb(top_hex))
    tree = material.node_tree
    nodes, links = tree.nodes, tree.links
    emission = next(n for n in nodes if n.type == "EMISSION")

    geometry = nodes.new("ShaderNodeNewGeometry")
    split = nodes.new("ShaderNodeSeparateXYZ")
    remap = nodes.new("ShaderNodeMapRange")
    remap.inputs["From Min"].default_value = 0
    remap.inputs["From Max"].default_value = fog_height
    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs["A"].default_value = srgb(bottom_hex)
    mix.inputs["B"].default_value = srgb(top_hex)

    links.new(geometry.outputs["Position"], split.inputs[0])
    links.new(split.outputs["Z"], remap.inputs["Value"])
    links.new(remap.outputs["Result"], mix.inputs["Factor"])
    links.new(mix.outputs["Result"], emission.inputs["Color"])
    return material


def periodic(x, waves):
    """Sum of sine waves whose periods divide the tile width, so the result tiles seamlessly."""
    return sum(a * math.sin(2 * math.pi * k * x / W + phase) for a, k, phase in waves)


class Silhouette:
    """Collects flat polygons in the XZ plane and turns them into one mesh object."""

    def __init__(self):
        self.bm = bmesh.new()

    def polygon(self, points):
        verts = [self.bm.verts.new((x, 0, z)) for x, z in points]
        self.bm.faces.new(verts)

    def wrapped(self, draw, x, reach):
        """Draw a shape at x, and again one tile to the left/right if it crosses an edge."""
        draw(x)
        if x - reach < 0:
            draw(x + W)
        if x + reach > W:
            draw(x - W)

    def build(self, scene, name, material):
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(name, mesh)
        obj.data.materials.append(material)
        scene.collection.objects.link(obj)
        return obj


def ridge(sil, base, waves, steps=512, extra=None):
    """A continuous skyline across the whole tile (plus a small overlap on both sides)."""
    points = []
    for i in range(steps + 1):
        x = -0.2 + (W + 0.4) * i / steps
        z = base + periodic(x, waves) + (extra(x) if extra else 0)
        points.append((x, z))
    sil.polygon([(-0.2, -0.1)] + points + [(W + 0.2, -0.1)])


def pine(sil, x, ground, height, width):
    tiers = 4
    for t in range(tiers):
        bottom = ground + height * (0.04 + 0.2 * t)
        top = bottom + height * 0.42
        half = width * (1 - t / (tiers + 0.6)) / 2
        sil.polygon([(x - half, bottom), (x + half, bottom), (x, top)])
    sil.polygon([(x - width * 0.05, ground - 0.1), (x + width * 0.05, ground - 0.1),
                 (x + width * 0.05, ground + height * 0.3), (x - width * 0.05, ground + height * 0.3)])


def branch(sil, rng, x, z, angle, length, thickness, depth):
    """Bare, twisting branches: a tapered quad that splits into two or three smaller ones."""
    end_x = x + math.cos(angle) * length
    end_z = z + math.sin(angle) * length
    nx, nz = -math.sin(angle), math.cos(angle)
    tip = thickness * 0.62
    sil.polygon([(x - nx * thickness, z - nz * thickness), (x + nx * thickness, z + nz * thickness),
                 (end_x + nx * tip, end_z + nz * tip), (end_x - nx * tip, end_z - nz * tip)])
    if depth == 0:
        return
    for _ in range(rng.choice((2, 2, 3))):
        branch(sil, rng, end_x, end_z, angle + rng.uniform(-0.75, 0.75),
               length * rng.uniform(0.55, 0.8), tip, depth - 1)


def reeds(sil, rng, x, ground):
    for _ in range(rng.randint(4, 8)):
        rx = x + rng.uniform(-0.25, 0.25)
        height = rng.uniform(0.5, 1.6)
        lean = rng.uniform(-0.25, 0.25)
        sil.polygon([(rx - 0.02, ground - 0.1), (rx + 0.02, ground - 0.1), (rx + lean, ground + height)])


def far_layer(scene, rng):
    sil = Silhouette()
    # Folded |sin| curves give sharp peaks and soft valleys, like distant mountains.
    def peaks(x):
        return sum(a * (1 - abs(math.sin(math.pi * k * x / W + phase))) ** 1.6
                   for a, k, phase in ((1.6, 3, 0.4), (0.9, 5, 1.3), (0.35, 11, 0.2)))

    ridge(sil, 3.2, [(0.5, 1, 0.3), (0.12, 19, 2.2)], extra=peaks)
    sil.build(scene, "Mountains", fog_material("Far", "#C6CBD1", "#E6E8EA", 6.5))


def mid_layer(scene, rng):
    sil = Silhouette()
    waves = [(0.45, 2, 1.2), (0.25, 5, 0.4), (0.1, 13, 2.9)]
    ridge(sil, 2.2, waves)
    x = 0.0
    while x < W:
        ground = 2.2 + periodic(x, waves) - 0.35
        height = rng.uniform(1.2, 2.6)
        width = height * rng.uniform(0.35, 0.5)
        sil.wrapped(lambda px, g=ground, h=height, w=width: pine(sil, px, g, h, w), x, width)
        x += rng.uniform(0.18, 0.75)
    sil.build(scene, "Forest", fog_material("Mid", "#AEB4BB", "#DADDE0", 4.8))


def near_layer(scene, rng):
    sil = Silhouette()
    waves = [(0.2, 3, 0.7), (0.08, 11, 2.2)]
    ridge(sil, 0.9, waves)

    trees = sorted(rng.uniform(0, W) for _ in range(6))
    for x in trees:
        ground = 0.9 + periodic(x, waves)
        seed = rng.random()
        sil.wrapped(lambda px, g=ground, s=seed: branch(
            sil, random.Random(s), px, g - 0.1, math.radians(90 + random.Random(s).uniform(-8, 8)),
            random.Random(s).uniform(1.6, 2.4), 0.16, 5), x, 4.5)

    x = 0.0
    while x < W:
        ground = 0.9 + periodic(x, waves)
        seed = rng.random()
        sil.wrapped(lambda px, g=ground, s=seed: reeds(sil, random.Random(s), px, g), x, 0.6)
        x += rng.uniform(0.3, 1.2)
    sil.build(scene, "Trees", fog_material("Near", "#979EA6", "#CBCFD3", 4.0))


def main():
    for name, build, seed in (("far", far_layer, 11), ("mid", mid_layer, 23), ("near", near_layer, 37)):
        scene = new_scene(WIDTH_PX, HEIGHT_PX, samples=8)
        ortho_camera(scene, W / 2, H / 2, W)
        build(scene, random.Random(seed))
        render(scene, asset_path("backgrounds", "parallax", f"{name}.png"))


main()
