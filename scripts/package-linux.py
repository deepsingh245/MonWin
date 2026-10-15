#!/usr/bin/env python3
"""Packages the published Linux build as dist/MonWin-<version>-<rid>.tar.gz (+ .sha256).

Run scripts/publish-linux.sh first. Python 3.8+ standard library only.

The archive is written with Python's tarfile rather than the tar command so every file gets an
explicit, correct mode (monwin and install.sh executable) even when this runs on Windows, whose
file system has no Unix permission bits. Layout:

    MonWin-<version>-linux-x64/
        monwin              the app (renamed from the MonWin.Linux apphost)
        MonWin.Linux.dll ... the app and the bundled .NET runtime
        install.sh          per-user installer (no root)
        monwin.desktop      menu entry template used by install.sh
        monwin.png          icon
        README.txt  LICENSE
"""
import argparse
import hashlib
import re
import sys
import tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
APPHOST = "MonWin.Linux"      # name produced by dotnet publish (AssemblyName)
EXECUTABLE = "monwin"         # name users run
EXECUTABLE_MODE = 0o755
FILE_MODE = 0o644
SUPPORTED_RIDS = ("linux-x64",)   # only architectures that are actually built and tested are packaged


def read_version() -> str:
    csproj = (ROOT / "src" / "MonWin.Linux" / "MonWin.Linux.csproj").read_text(encoding="utf-8")
    match = re.search(r"<Version>\s*([^<\s]+)\s*</Version>", csproj)
    if not match:
        sys.exit("error: <Version> not found in MonWin.Linux.csproj")
    return match.group(1)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--rid", default="linux-x64")
    parser.add_argument("--version", default=None, help="defaults to <Version> in MonWin.Linux.csproj")
    args = parser.parse_args()

    if args.rid not in SUPPORTED_RIDS:
        sys.exit(f"error: only {', '.join(SUPPORTED_RIDS)} is packaged (other architectures are not tested)")

    version = args.version or read_version()
    publish_dir = ROOT / "publish-linux" / args.rid
    if not (publish_dir / APPHOST).is_file():
        sys.exit(f"error: {publish_dir / APPHOST} not found - run scripts/publish-linux.sh first")

    packaging = ROOT / "packaging" / "linux"
    extras = {
        "install.sh": (packaging / "install.sh", EXECUTABLE_MODE),
        "monwin.desktop": (packaging / "monwin.desktop", FILE_MODE),
        "monwin.png": (packaging / "monwin.png", FILE_MODE),
        "README.txt": (packaging / "README.txt", FILE_MODE),
        "LICENSE": (ROOT / "LICENSE", FILE_MODE),
    }
    for name, (path, _) in extras.items():
        if not path.is_file():
            sys.exit(f"error: missing {path}")

    folder = f"MonWin-{version}-{args.rid}"
    dist = ROOT / "dist"
    dist.mkdir(exist_ok=True)
    archive = dist / f"{folder}.tar.gz"
    if archive.exists():
        archive.unlink()

    def add(tar: tarfile.TarFile, source: Path, arcname: str, mode: int) -> None:
        info = tar.gettarinfo(str(source), arcname=f"{folder}/{arcname}")
        info.mode = mode
        info.uid = info.gid = 0
        info.uname = info.gname = "root"
        with source.open("rb") as handle:
            tar.addfile(info, handle)

    count = 0
    with tarfile.open(archive, "w:gz", compresslevel=9) as tar:
        for path in sorted(p for p in publish_dir.rglob("*") if p.is_file()):
            relative = path.relative_to(publish_dir).as_posix()
            if relative == APPHOST:
                add(tar, path, EXECUTABLE, EXECUTABLE_MODE)
            elif path.suffix == ".so" or ".so." in path.name:
                add(tar, path, relative, EXECUTABLE_MODE)
            else:
                add(tar, path, relative, FILE_MODE)
            count += 1
        for name, (path, mode) in extras.items():
            add(tar, path, name, mode)
            count += 1

    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    (dist / f"{archive.name}.sha256").write_text(f"{digest}  {archive.name}\n", encoding="utf-8")
    print(f"Packaged {count} files: {archive}  ({archive.stat().st_size / 1_048_576:.1f} MB)")
    print(f"sha256: {digest}")


if __name__ == "__main__":
    main()
