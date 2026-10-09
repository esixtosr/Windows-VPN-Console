"""Regenerate the original vector app icon. Requires librsvg's rsvg-convert.

No third-party artwork or Python packages are used. Each ICO entry contains a
lossless PNG at its actual size, including small taskbar and high-DPI sizes.
"""
from pathlib import Path
import struct
import subprocess

assets = Path(__file__).resolve().parents[1] / "src/CNIT455.VPN.App/Assets"
sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
frames = [subprocess.check_output([
    "rsvg-convert", "--width", str(size), "--height", str(size),
    str(assets / "app-icon.svg"),
]) for size in sizes]
offset = 6 + len(sizes) * 16
entries = []
for size, frame in zip(sizes, frames):
    entries.append(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0,
                               1, 32, len(frame), offset))
    offset += len(frame)
(assets / "app.ico").write_bytes(struct.pack("<HHH", 0, 1, len(sizes))
                                 + b"".join(entries) + b"".join(frames))
(assets / "app-icon.png").write_bytes(frames[-1])
print(f"Generated app.ico: {len(frames)} sizes; app-icon.png: 256 x 256")
