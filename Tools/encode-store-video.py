"""Encode native game captures at their recorded pace, without overlays or fabricated frames."""
import argparse
import csv
import json
import re
import subprocess
from pathlib import Path

from PIL import Image


def run(command):
    result = subprocess.run(command, capture_output=True, text=True,
                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    if result.returncode:
        raise RuntimeError(result.stderr)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("frames", type=Path)
    parser.add_argument("--ffmpeg", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    folder = args.frames.resolve()
    rows = list(csv.DictReader((folder / "times.csv").open(encoding="utf-8")))
    if len(rows) < 2:
        raise ValueError("Need at least two recorded game frames")
    timestamps = [float(row["seconds"]) for row in rows]
    if any(b <= a for a, b in zip(timestamps, timestamps[1:])):
        raise ValueError("Frame timestamps must increase")
    duration = timestamps[-1] - timestamps[0] + 1 / 30
    if not 1 <= duration <= 28:
        raise ValueError("Recording must last between 1 and 28 seconds")
    lines = ["ffconcat version 1.0"]
    with Image.open(folder / f"frame-{int(rows[0]['frame']):04d}.jpg") as picture:
        width, height = picture.size
    if (width,height) not in ((1280,720),(720,1280)):
        raise ValueError("Expected desktop 16:9 or mobile 9:16 recording")
    for i, row in enumerate(rows):
        frame = folder / f"frame-{int(row['frame']):04d}.jpg"
        with Image.open(frame) as picture:
            if picture.size != (width, height):
                raise ValueError(f"Unexpected frame dimensions: {frame.name}")
        frame_duration = timestamps[i + 1] - timestamps[i] if i + 1 < len(rows) else 1 / 30
        lines += [f"file '{frame.name}'", f"duration {frame_duration:.6f}"]
    lines.append(f"file 'frame-{int(rows[-1]['frame']):04d}.jpg'")
    timeline = folder / "recording.ffconcat"
    timeline.write_text("\n".join(lines) + "\n", encoding="utf-8")
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    run([str(args.ffmpeg.resolve()), "-hide_banner", "-loglevel", "error", "-y",
         "-safe", "1", "-f", "concat", "-i", str(timeline), "-t", f"{duration:.6f}",
         "-vf", "fps=30,scale=in_range=full:out_range=tv:out_color_matrix=bt709,format=yuv420p,setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709",
         "-c:v", "libx264", "-pix_fmt", "yuv420p", "-color_range", "tv",
         "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-preset", "medium",
         "-crf", "18", "-movflags", "+faststart", "-an", str(output)])
    decoded = run([str(args.ffmpeg.resolve()), "-hide_banner", "-i", str(output), "-f", "null", "-"])
    if not re.search(rf"Video: h264.*yuv420p.*{width}x{height}.*30 fps", decoded.stderr):
        raise ValueError("Encoded video must be H.264 yuv420p at 30 fps with its native dimensions")
    if output.stat().st_size > 100_000_000:
        raise ValueError("Video exceeds 100 MB")
    report = dict(result="PASS", width=width, height=height, fps=30, seconds=round(duration, 3),
                  frames=len(rows), bytes=output.stat().st_size, audio=False,
                  source="native game render with recorded frame timing", output=str(output))
    (folder.parent / ("store-video-vertical-check.json" if height>width else "store-video-check.json")).write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
