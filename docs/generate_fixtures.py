# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
"""Generate account-free classifier fixtures and a schematic atlas (stdlib only)."""
from pathlib import Path
import binascii
import html
import json
import math
import struct
import zlib

ROOT = Path(__file__).resolve().parent
WIDTH, HEIGHT = 2340, 1080


def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', binascii.crc32(kind + data) & 0xffffffff)


def fixture(driving, dimmed=False):
    # The retained templates contain only four tiny arrow luminance grids.
    # Paint them against a plain background; there is no screenshot or account UI.
    pixels = bytearray([42 if not dimmed else 24]) * (WIDTH * HEIGHT)
    if driving:
        features = json.loads((ROOT.parent / 'assets/reference-driving-hud.json').read_text(encoding='utf-8'))
        for feature in features:
            left, top = feature['X'] * WIDTH, feature['Y'] * HEIGHT
            width, height = feature['Width'] * WIDTH, feature['Height'] * HEIGHT
            for y in range(math.floor(top), math.ceil(top + height)):
                for x in range(math.floor(left), math.ceil(left + width)):
                    gx = min(23, max(0, int((x - left) * 24 / width)))
                    gy = min(23, max(0, int((y - top) * 24 / height)))
                    value = feature['Values'][gy * 24 + gx]
                    pixels[y * WIDTH + x] = round(value * .55 if dimmed else value)
    # Grayscale PNG with no EXIF, text, timestamps or identifying metadata.
    scanlines = b''.join(b'\0' + pixels[y * WIDTH:(y + 1) * WIDTH] for y in range(HEIGHT))
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', WIDTH, HEIGHT, 8, 0, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(scanlines, 9)) + chunk(b'IEND', b''))


def outputs():
    result = {}
    for name, driving, dimmed in [('driving', True, False), ('dimmed-driving', True, True),
                                  ('walking', False, False), ('blocked', False, True)]:
        result[f'fixtures/{name}.png'] = fixture(driving, dimmed)
    hud = json.loads((ROOT / 'data/hud.json').read_text(encoding='utf-8'))
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {WIDTH} {HEIGHT}" role="img" aria-label="Schematic touch atlas; no game screenshot">',
           '<rect width="2340" height="1080" fill="#263b32"/>',
           '<path d="M0 540H2340M1170 0V1080" stroke="#ffffff" opacity=".08"/>',
           '<text x="1170" y="420" text-anchor="middle" fill="#c4d4bf" font-family="sans-serif" font-size="42">SCHEMATIC TOUCH ATLAS</text>',
           '<text x="1170" y="480" text-anchor="middle" fill="#c4d4bf" font-family="sans-serif" font-size="26">Illustrative coordinates; calibrate against your own HUD</text>']
    for entry in hud['entries']:
        x, y = round(entry['x'] * WIDTH), round(entry['y'] * HEIGHT)
        svg.append(f'<circle cx="{x}" cy="{y}" r="32" fill="none" stroke="#88a078"><title>{html.escape(entry["name"])}</title></circle>')
    svg.append('</svg>\n')
    result['images/hud-schematic.svg'] = '\n'.join(svg).encode()
    return result


def main():
    for name, content in outputs().items():
        path = ROOT / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)
    print('Generated four synthetic fixtures and the schematic atlas.')


if __name__ == '__main__':
    main()
