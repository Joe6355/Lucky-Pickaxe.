"""Validate the exact WebGL archive shipped to the Yandex draft."""
import gzip
import hashlib
import json
import re
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parent.parent
archive = root / "Builds/LuckyPickaxe-YandexGames.zip"
expected = {"index.html", "platform.js", "favicon.svg", "Build/WebGL.loader.js",
            "Build/WebGL.data.unityweb", "Build/WebGL.framework.js.unityweb", "Build/WebGL.wasm.unityweb"}
version = re.search(r"^  bundleVersion: (.+)$", (root / "ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8"), re.M).group(1)
with zipfile.ZipFile(archive) as package:
    assert set(package.namelist()) == expected, "Unexpected archive entries"
    assert package.testzip() is None, "ZIP checksum failure"
    assert all(name.isascii() and " " not in name and ".." not in name for name in package.namelist())
    index = package.read("index.html").decode("utf-8")
    assert f'productVersion: "{version}"' in index, "Wrong built version"
    assert "{{{" not in index, "Unexpanded Unity template"
    assert package.read("platform.js") == (root / "Assets/WebGLTemplates/LuckyPickaxe/platform.js").read_bytes(), "Outdated SDK bridge"
    expanded = 0
    for name in expected:
        payload = package.read(name)
        if name.endswith(".unityweb"):
            payload = gzip.decompress(payload)
        if ".wasm." in name:
            assert payload.startswith(b"\x00asm"), "Invalid Wasm"
        expanded += len(payload)
    assert expanded < 100_000_000, "Expanded bundle exceeds 100 MB"
    report = dict(result="PASS", version=version, entries=len(expected), bytes=archive.stat().st_size,
                  expandedBytes=expanded, sha256=hashlib.sha256(archive.read_bytes()).hexdigest())
(root / "Logs/archive-check.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report))
