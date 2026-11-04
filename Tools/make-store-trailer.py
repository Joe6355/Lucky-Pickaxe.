"""Edit the recorded game into a 24-second trailer with original synthesized music."""
import argparse
import array
import json
import math
import random
import re
import shutil
import subprocess
import wave
from pathlib import Path


def run(command, cwd=None):
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True,
                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    if result.returncode:
        raise RuntimeError(result.stderr)
    return result


def music(destination):
    rate = 48000
    melody = [64, 67, 71, 76, 74, 71, 67, 62, 64, 67, 72, 76, 79, 76, 72, 67]
    bass = [40, 40, 36, 38]
    samples = array.array("h")
    rng = random.Random(6355)
    for i in range(24 * rate):
        t = i / rate
        step = int(t / .25)
        age = t % .25
        note = melody[step % len(melody)]
        frequency = 440 * 2 ** ((note - 69) / 12)
        tone = math.sin(2 * math.pi * frequency * t) + .18 * math.sin(4 * math.pi * frequency * t)
        envelope = min(1, age / .012) * math.exp(-age * 11)
        low = 440 * 2 ** ((bass[int(t / 2) % 4] - 69) / 12)
        kick_age = t % .5
        kick = math.sin(2 * math.pi * (58 * kick_age + 14 * (1 - math.exp(-kick_age * 24)))) * math.exp(-kick_age * 24)
        hat = rng.uniform(-1, 1) * math.exp(-(t % .25) * 180)
        value = .12 * tone * envelope + .055 * math.sin(2 * math.pi * low * t) + .08 * kick + .024 * hat
        fade = min(1, t / .5, max(0, (24 - t) / 1.2))
        samples.append(int(max(-1, min(1, value * fade)) * 32767))
    with wave.open(str(destination), "wb") as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(rate)
        target.writeframes(samples.tobytes())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--ffmpeg", required=True, type=Path)
    parser.add_argument("--font", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    ffmpeg = str(args.ffmpeg.resolve())
    source = args.input.resolve()
    probe = run([ffmpeg, "-hide_banner", "-i", str(source), "-f", "null", "-"])
    width, height = map(int, re.search(r"Video: h264.*?, (\d{3,5})x(\d{3,5})\b", probe.stderr).groups())
    vertical = height > width
    staging = Path(__file__).resolve().parent.parent / "Logs" / ("trailer-vertical" if vertical else "trailer-horizontal")
    staging.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(args.font, staging / "font.ttf")
    music(staging / "music.wav")
    filters = []

    def text(value, size, y, start, end, color="f0eadc", x="(w-tw)/2"):
        filename = f"text-{len(filters):02d}.txt"
        (staging / filename).write_text(value, encoding="utf-8")
        filters.append(f"drawtext=fontfile=font.ttf:textfile={filename}:fontcolor=0x{color}:fontsize={size}:x={x}:y={y}:enable='between(t,{start},{end})'")

    for start, end in [(0, 2), (22, 24)]:
        filters.append(f"drawbox=x=0:y=0:w=iw:h=ih:color=0x15242e@0.88:t=fill:enable='between(t,{start},{end})'")
        text("КИРКА УДАЧИ", 62 if vertical else 76, f"h/2-90", start, end)
        text("ТВОЯ ШАХТА. ТВОИ ПРАВИЛА." if start == 0 else "ИГРАЙ В ЯНДЕКС ИГРАХ", 27 if vertical else 34, "h/2+8", start, end, "b9d48b")
    y = height - (180 if vertical else 104)
    for start, end, title in [(2.05, 3.85, "КОПАЙ РУДУ"), (4.05, 7.85, "СЛУЧАЙНЫЕ ЗАЧАРОВАНИЯ"),
                              (8.05, 12.85, "ВЫБИРАЙ КИРКИ"), (13.05, 18.85, "ДОБЕРИСЬ ДО СУНДУКОВ"),
                              (19.05, 21.85, "РАЗВИВАЙ СВОЮ ШАХТУ")]:
        filters.append(f"drawbox=x=0:y={y}:w=iw:h=84:color=0x15242e@0.94:t=fill:enable='between(t,{start},{end})'")
        text(title, 34 if vertical else 40, y + 19, start, end)
    text("6+", 22, "14", 0, 24, x="w-tw-14")
    filters.append("fade=t=in:st=0:d=0.25,fade=t=out:st=23.6:d=0.4,format=yuv420p,setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709")
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    run([ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", str(source), "-i", "music.wav",
         "-t", "24", "-vf", ",".join(filters), "-c:v", "libx264", "-crf", "18", "-preset", "medium",
         "-pix_fmt", "yuv420p", "-color_range", "tv", "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709",
         "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart", str(output)], staging)
    decoded = run([ffmpeg, "-hide_banner", "-i", str(output), "-f", "null", "-"])
    if not re.search(rf"Video: h264.*yuv420p.*{width}x{height}.*30 fps", decoded.stderr) or "Audio: aac" not in decoded.stderr:
        raise ValueError("Unexpected trailer streams")
    if output.stat().st_size > 100_000_000:
        raise ValueError("Trailer exceeds platform size limit")
    report = dict(result="PASS", width=width, height=height, seconds=24, fps=30, audio="original synthesized music, AAC",
                  bytes=output.stat().st_size, output=str(output), source=str(source))
    (staging / "check.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
