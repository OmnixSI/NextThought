#!/usr/bin/env python3
"""Stage and validate the assets uploaded for a NextThought release."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import pathlib
import shutil
import subprocess
from collections.abc import Iterable


ROOT = pathlib.Path(__file__).resolve().parents[2]


def sha256(path: pathlib.Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_checksum_manifest(files: Iterable[pathlib.Path], destination: pathlib.Path) -> None:
    """Write checksums for precisely the files that will be uploaded."""
    entries = sorted(files, key=lambda path: path.name)
    destination.write_text("".join(f"{sha256(path)}  {path.name}\n" for path in entries))


def stage_release_assets(root: pathlib.Path, tag: str, commit: str) -> list[pathlib.Path]:
    dist = root / "dist"
    version = tag.removeprefix("v")

    for source_name, extension in (("NextThought.dmg", "dmg"), ("NextThought-macOS.zip", "zip")):
        source = dist / source_name
        target = dist / f"NextThought-{version}-macOS-arm64.{extension}"
        if source.exists():
            shutil.copyfile(source, target)
        if target.stat().st_size <= 1_000_000:
            raise ValueError(f"Release asset is unexpectedly small: {target.name}")

    manifest_path = root / "Resources/DocumentRuntime/runtime-manifest.json"
    manifest = json.loads(manifest_path.read_text())
    runtime_asset = dist / manifest["asset"]
    if sha256(runtime_asset) != manifest["sha256"]:
        raise ValueError(f"Document runtime checksum mismatch: {runtime_asset.name}")
    if runtime_asset.stat().st_size != manifest["downloadBytes"]:
        raise ValueError(f"Document runtime size mismatch: {runtime_asset.name}")
    staged_manifest = dist / "runtime-manifest.json"
    shutil.copyfile(manifest_path, staged_manifest)

    zip_path = dist / f"NextThought-{version}-macOS-arm64.zip"
    plist = dist / "NextThought.app/Contents/Info.plist"
    bundle_version = subprocess.check_output(
        ["/usr/libexec/PlistBuddy", "-c", "Print :CFBundleShortVersionString", str(plist)],
        text=True,
    ).strip()
    if bundle_version != version:
        raise ValueError(f"Bundle version {bundle_version} does not match tag {version}")
    build = subprocess.check_output(
        ["/usr/libexec/PlistBuddy", "-c", "Print :CFBundleVersion", str(plist)],
        text=True,
    ).strip()

    update = {
        "tag": tag,
        "version": version,
        "build": int(build),
        "zip": zip_path.name,
        "sha256": sha256(zip_path),
        "commit": commit,
        "publishedAt": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    update_path = dist / "update.json"
    update_path.write_text(json.dumps(update, indent=2) + "\n")

    # The runtime archive is verified above, but it is hosted by the original
    # v1.7.0 release and is not uploaded to each app release. Keep it out of
    # this release's SHA256SUMS.txt so re-download verification matches the
    # assets that are actually attached to this release.
    dmg_path = dist / f"NextThought-{version}-macOS-arm64.dmg"
    published_assets = [dmg_path, zip_path, staged_manifest, update_path]
    checksums_path = dist / "SHA256SUMS.txt"
    write_checksum_manifest(published_assets, checksums_path)
    upload_list_path = dist / "release-assets.txt"
    upload_list_path.write_text("".join(f"{path.name}\n" for path in published_assets))
    return [*published_assets, checksums_path, upload_list_path]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tag", required=True)
    parser.add_argument("--commit", required=True)
    args = parser.parse_args()
    staged = stage_release_assets(ROOT, args.tag, args.commit)
    print("Validated:", ", ".join(path.name for path in staged))


if __name__ == "__main__":
    main()
