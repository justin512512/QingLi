from io import BytesIO
from pathlib import Path
import struct

from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src/QingLi.Windows/Assets/Brand/qingli-app-icon-source.png"
OUTPUT = ROOT / "src/QingLi.Windows/Assets/Brand/QingLi.ico"
SIZES = [256, 128, 64, 48, 40, 32, 24, 20, 16]


def main() -> None:
    with Image.open(SOURCE) as source_image:
        source = source_image.convert("RGBA")

    frames = []
    for size in SIZES:
        frame = source.resize((size, size), Image.Resampling.LANCZOS)
        if size < 48:
            frame = frame.filter(
                ImageFilter.UnsharpMask(radius=0.6, percent=120, threshold=2)
            )
        frames.append(frame)

    encoded_frames = []
    for frame in frames:
        buffer = BytesIO()
        frame.save(buffer, format="PNG")
        encoded_frames.append(buffer.getvalue())

    directory_size = 6 + (16 * len(encoded_frames))
    offset = directory_size
    with OUTPUT.open("wb") as icon_file:
        icon_file.write(struct.pack("<HHH", 0, 1, len(encoded_frames)))
        for size, encoded_frame in zip(SIZES, encoded_frames, strict=True):
            # System.Drawing's ICO selector compares the raw directory byte and
            # treats the spec's 0 (= 256) sentinel as zero. Advertising the
            # 256px PNG as 255px makes it the closest match for a 256px request;
            # the selected image still reports its true 256x256 payload size.
            directory_dimension = 255 if size == 256 else size
            icon_file.write(
                struct.pack(
                    "<BBBBHHII",
                    directory_dimension,
                    directory_dimension,
                    0,
                    0,
                    1,
                    32,
                    len(encoded_frame),
                    offset,
                )
            )
            offset += len(encoded_frame)

        for encoded_frame in encoded_frames:
            icon_file.write(encoded_frame)


if __name__ == "__main__":
    main()
