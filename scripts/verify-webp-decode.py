"""Independent Pillow decode of actual app diagnostic outputs; never ships in app."""
import argparse
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("report", type=Path, help="editor-layout JSON report prefix")
args = parser.parse_args()
prefix = str(args.report)
expected = Path(prefix + ".expected-bgra").read_bytes()
with Image.open(prefix + ".lossless.webp") as image:
    assert image.size == (128, 64), image.size
    assert image.convert("RGBA").tobytes("raw", "BGRA") == expected, "Lossless pixels/alpha differ"
for quality in (20, 100):
    with Image.open(prefix + f".lossy{quality}.webp") as image:
        assert image.size == (128, 64)
        assert image.convert("RGBA").getchannel("A").tobytes() == expected[3::4]
assert Path(prefix + ".lossy20.webp").stat().st_size < Path(prefix + ".lossy100.webp").stat().st_size
print("PASS: independent Pillow decode, exact lossless BGRA including hidden RGB, lossy alpha/dimensions and quality sizes.")
