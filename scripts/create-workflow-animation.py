"""Generate an explicitly illustrated workflow using synthetic content only.

Requires Pillow. This draws a diagram; it does not record or impersonate a UI test.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs/images/capture-to-codex.gif"
FONT_ROOT = Path("C:/Windows/Fonts")


def font(size, mono=False):
    return ImageFont.truetype(str(FONT_ROOT / ("consola.ttf" if mono else "segoeui.ttf")), size)


def text(draw, xy, value, size=20, fill="#dce4ed", mono=False):
    draw.text(xy, value, font=font(size, mono), fill=fill)


def snippet(draw, x, y):
    for index, line in enumerate(("SYNTHETIC EXAMPLE", "Error: missing configuration", "Set DEMO_MODE=true and retry.")):
        text(draw, (x, y + index * 28), line, 19, "#e5edf6", True)


frames = []
steps = ("Press Print Screen", "Select the region", "Release to copy", "Paste into Codex")
for frame_index in range(96):
    phase = frame_index // 24
    progress = (frame_index % 24) / 23
    image = Image.new("RGB", (960, 540), "#10151c")
    draw = ImageDraw.Draw(image)
    text(draw, (38, 20), "SNIPPYGRAB  /  CAPTURE TO CODEX", 16, "#9eafc2")
    text(draw, (38, 51), steps[phase], 30, "#f5f8fc")
    text(draw, (38, 98), "Illustrated workflow  •  Synthetic content  •  Not a screen recording", 15, "#92a4b8")
    draw.rounded_rectangle((38, 146, 548, 432), 12, fill="#1c2633", outline="#3c4c60", width=2)
    text(draw, (59, 162), "Example source", 17, "#9eafc2")
    draw.line((39, 198, 547, 198), fill="#3c4c60")
    snippet(draw, 77, 242)
    draw.rounded_rectangle((584, 146, 922, 432), 12, fill="#19222c", outline="#3c4c60", width=2)
    text(draw, (606, 162), "Codex draft", 17, "#9eafc2")
    draw.line((585, 198, 921, 198), fill="#3c4c60")
    text(draw, (606, 219), "Ask about this error…", 16, "#7f93aa")
    if phase == 0:
        draw.rounded_rectangle((313, 358, 518, 411), 8, fill="#2a394b", outline="#617d9d")
        text(draw, (338, 372), "Print Screen", 21)
    if phase >= 1:
        extent = progress if phase == 1 else 1
        right, bottom = int(65 + 446 * extent), int(227 + 105 * extent)
        draw.rectangle((65, 227, right, bottom), outline="#6ce3bf", width=3)
        draw.ellipse((right - 5, bottom - 5, right + 5, bottom + 5), fill="#6ce3bf")
    if phase >= 2:
        draw.rounded_rectangle((493, 367, 567, 444), 8, fill="#dce4ed", outline="#6ce3bf", width=3)
        draw.rectangle((502, 379, 558, 424), fill="#1c2633")
        text(draw, (515, 431), "1", 10, "#10151c")
        text(draw, (59, 365), "Copied · ready to paste", 17, "#6ce3bf")
    if phase == 3:
        draw.rounded_rectangle((606, 254, 900, 358), 8, fill="#1c2633", outline="#6ce3bf", width=2)
        text(draw, (622, 269), "SYNTHETIC EXAMPLE", 13, "#e5edf6", True)
        text(draw, (622, 294), "Error: missing configuration", 13, "#e5edf6", True)
        text(draw, (622, 319), "Set DEMO_MODE=true and retry.", 13, "#e5edf6", True)
        text(draw, (606, 380), "Ctrl + V", 20, "#6ce3bf")
    for index, label in enumerate(("Invoke", "Select", "Copy", "Paste")):
        x = 38 + index * 228
        draw.line((x, 474, x + 199, 474), fill="#6ce3bf" if index <= phase else "#364455", width=4)
        text(draw, (x, 487), f"0{index + 1}  {label}", 17, "#dce4ed" if index <= phase else "#718298")
    frames.append(image)

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=100, loop=0, optimize=True)
with Image.open(OUTPUT) as verified:
    assert verified.size == (960, 540)
    assert verified.n_frames >= 4
    for index in range(verified.n_frames):
        verified.seek(index)
        verified.load()
    print(f"PASS: decoded {verified.n_frames} frames, {OUTPUT.stat().st_size} bytes")
