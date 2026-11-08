"""
One-off conversion of the original painted level backgrounds: the white sky becomes transparent,
so the parallax layers can show through behind the silhouettes.

The art is dark ink on white. For light pixels the alpha is recovered as if the pixel were
ink blended over white (v = ink * a + 1 * (1 - a)), which keeps soft anti-aliased edges
without white halos. Dark pixels stay fully opaque. Running it twice changes nothing more.

Run:  blender -b --factory-startup --python tools/blender/clear_painted_sky.py
"""
import os
import sys

import bpy
import numpy as np

sys.path.append(os.path.dirname(__file__))
from common import asset_path  # noqa: E402

INK = 0.09          # colour of the painted silhouettes
LIGHT_START = 0.6   # pixels lighter than this are treated as ink blended with white sky

for name in ("level-01.png", "level-02.png"):
    path = asset_path("backgrounds", name)
    image = bpy.data.images.load(path)
    width, height = image.size
    pixels = np.array(image.pixels[:], dtype=np.float32).reshape(height, width, 4)

    rgb = pixels[:, :, :3]
    lightness = rgb.mean(axis=2)
    light = lightness > LIGHT_START

    alpha = np.clip((1 - lightness) / (1 - INK), 0, 1)
    pixels[:, :, 3] = np.where(light, np.minimum(pixels[:, :, 3], alpha), pixels[:, :, 3])
    for c in range(3):
        pixels[:, :, c] = np.where(light, INK, pixels[:, :, c])

    image.pixels[:] = pixels.ravel()
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    cleared = float((pixels[:, :, 3] < 0.05).mean())
    print(f"CLEARED {name}: {cleared:.0%} transparent")
