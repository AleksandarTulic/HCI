"""
Turns frames recorded by smoke-test.ps1 (-Frames) into an animated GIF for the README.

    python tools/make-gif.py <first-frame-or-folder-prefix> <output.gif> [--width 800] [--ms 66]

Example:
    powershell -File tools/smoke-test.ps1 -ClientOnly -Out C:/tmp/run.png -Frames 90 -Keys "Right:2"
    python tools/make-gif.py C:/tmp/run docs/images/gameplay.gif

Needs Pillow (pip install pillow).
"""
import argparse
import glob

from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("prefix", help="frames are <prefix>-000.png, <prefix>-001.png, ...")
    parser.add_argument("output")
    parser.add_argument("--width", type=int, default=800)
    parser.add_argument("--ms", type=int, default=66, help="time per frame")
    args = parser.parse_args()

    files = sorted(glob.glob(f"{args.prefix}-[0-9][0-9][0-9].png"))
    if not files:
        raise SystemExit(f"No frames found for {args.prefix}-NNN.png")

    frames = []
    for path in files:
        image = Image.open(path).convert("RGB")
        height = round(image.height * args.width / image.width)
        frames.append(image.resize((args.width, height), Image.LANCZOS))

    # One shared palette keeps colours stable between frames and the file small.
    palette = frames[len(frames) // 2].quantize(colors=128, method=Image.Quantize.MEDIANCUT)
    quantized = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
    quantized[0].save(args.output, save_all=True, append_images=quantized[1:],
                      duration=args.ms, loop=0, optimize=True, disposal=1)
    print(f"Wrote {args.output}: {len(frames)} frames, {args.width}x{frames[0].height}")


if __name__ == "__main__":
    main()
