#!/usr/bin/env python3
"""Generate the Windows Phone 8.1 (.scale-240) assets from a source logo.

Default source is the remove.bg Jellyfin logo. The logo is trimmed to its alpha
bounding box and centred on each target canvas.

Usage:
    python tools/generate_assets.py [source.png] [output_dir]
"""
import os
import sys

from PIL import Image

BRAND_BG = (16, 21, 23, 255)  # #101517, matches Package.appxmanifest BackgroundColor

TARGETS = [
    ("Logo.scale-240.png", 360, 360, "transparent"),
    ("SmallLogo.scale-240.png", 106, 106, "transparent"),
    ("Square71x71Logo.scale-240.png", 170, 170, "transparent"),
    ("WideLogo.scale-240.png", 744, 360, "transparent"),
    ("StoreLogo.scale-240.png", 120, 120, "transparent"),
    ("SplashScreen.scale-240.png", 1152, 1920, "brand"),
    # Inline UI mark shown next to the app title. Unqualified on purpose: an
    # unqualified asset always resolves, a .scale-* only one falls back to a
    # missing candidate on a device with another scale factor.
    ("BrandMark.png", 144, 144, "ui"),
]

DEFAULT_SOURCE = os.path.expanduser("~/Downloads/images_1_-removebg-preview.png")
DEFAULT_OUT = os.path.join("JellyfinPlayer", "JellyfinPlayer", "Assets")


def trim(image):
    if image.mode == "RGBA":
        box = image.getchannel("A").getbbox()
    else:
        box = image.getbbox()
    return image.crop(box) if box else image


def fit(logo, target_w, target_h, fraction):
    """Scale the logo so its longer edge is `fraction` of the shorter target edge."""
    limit = int(min(target_w, target_h) * fraction)
    w, h = logo.size
    scale = min(limit / w, limit / h)
    return logo.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.LANCZOS)


def place(canvas, logo):
    x = (canvas.size[0] - logo.size[0]) // 2
    y = (canvas.size[1] - logo.size[1]) // 2
    canvas.alpha_composite(logo, (x, y))


def main():
    source = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SOURCE
    out_dir = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_OUT
    if not os.path.exists(source):
        raise SystemExit("source logo not found: %s" % source)

    logo = trim(Image.open(source).convert("RGBA"))
    os.makedirs(out_dir, exist_ok=True)

    for name, w, h, background in TARGETS:
        if background == "brand":
            canvas = Image.new("RGBA", (w, h), BRAND_BG)
            place(canvas, fit(logo, w, h, 0.40))
        elif background == "ui":
            # Fill the canvas: no tile-style padding for an inline mark.
            canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
            place(canvas, fit(logo, w, h, 0.92))
        else:
            canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
            place(canvas, fit(logo, w, h, 0.72))
        path = os.path.join(out_dir, name)
        canvas.save(path)
        print("wrote %s %s" % (path, canvas.size))


if __name__ == "__main__":
    main()
