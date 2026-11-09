import hashlib
import importlib.util
import json
import pathlib
import tempfile
import unittest
from unittest.mock import patch


SCRIPT = pathlib.Path(__file__).resolve().parents[2] / "DocumentRuntime" / "release_assets.py"
SPEC = importlib.util.spec_from_file_location("release_assets", SCRIPT)
RELEASE_ASSETS = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(RELEASE_ASSETS)


class ReleaseChecksumManifestTests(unittest.TestCase):
    def make_staging_fixture(self, root, runtime_sha=None, runtime_size=None):
        dist = root / "dist"
        app_plist = dist / "NextThought.app/Contents/Info.plist"
        runtime = b"verified runtime archive"
        asset_name = "InterviewHacker-document-runtime-1.7.0-macOS-arm64.tar.gz"
        dist.mkdir()
        (dist / "NextThought.dmg").write_bytes(b"d" * 1_000_001)
        (dist / "NextThought-macOS.zip").write_bytes(b"z" * 1_000_001)
        (dist / asset_name).write_bytes(runtime)
        app_plist.parent.mkdir(parents=True)
        app_plist.write_text("fixture")
        manifest_path = root / "Resources/DocumentRuntime/runtime-manifest.json"
        manifest_path.parent.mkdir(parents=True)
        manifest_path.write_text(
            json.dumps({
                "asset": asset_name,
                "sha256": runtime_sha or hashlib.sha256(runtime).hexdigest(),
                "downloadBytes": runtime_size or len(runtime),
            })
        )
        return dist, dist / asset_name

    def test_manifest_lists_only_published_assets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            dmg = root / "NextThought-2.4.1-macOS-arm64.dmg"
            archive = root / "NextThought-2.4.1-macOS-arm64.zip"
            runtime = root / "InterviewHacker-document-runtime-1.7.0-macOS-arm64.tar.gz"
            destination = root / "SHA256SUMS.txt"
            dmg.write_bytes(b"disk image")
            archive.write_bytes(b"application archive")
            runtime.write_bytes(b"externally hosted runtime")

            RELEASE_ASSETS.write_checksum_manifest([archive, dmg], destination)

            lines = destination.read_text().splitlines()
            self.assertEqual(len(lines), 2)
            self.assertEqual([line.split("  ", 1)[1] for line in lines], [dmg.name, archive.name])
            self.assertNotIn(runtime.name, destination.read_text())
            expected = hashlib.sha256(dmg.read_bytes()).hexdigest()
            self.assertEqual(lines[0].split("  ", 1)[0], expected)

    def test_staging_checks_external_runtime_but_lists_only_release_assets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            dist, runtime_path = self.make_staging_fixture(root)
            asset_name = runtime_path.name

            with patch.object(RELEASE_ASSETS.subprocess, "check_output", side_effect=["2.4.1", "42"]):
                published = RELEASE_ASSETS.stage_release_assets(root, "v2.4.1", "abc123")

            self.assertEqual(
                {path.name for path in published},
                {
                    "NextThought-2.4.1-macOS-arm64.dmg",
                    "NextThought-2.4.1-macOS-arm64.zip",
                    "runtime-manifest.json",
                    "update.json",
                    "SHA256SUMS.txt",
                    "release-assets.txt",
                },
            )
            checksum_text = (dist / "SHA256SUMS.txt").read_text()
            self.assertNotIn(asset_name, checksum_text)
            self.assertNotIn(asset_name, {path.name for path in published})
            self.assertEqual(len(checksum_text.splitlines()), 4)
            checksum_names = {line.split("  ", 1)[1] for line in checksum_text.splitlines()}
            upload_names = set((dist / "release-assets.txt").read_text().splitlines())
            self.assertEqual(upload_names, checksum_names)

    def test_staging_rejects_runtime_checksum_and_size_mismatch(self):
        cases = (
            ("0" * 64, None),
            (None, 999),
        )
        for runtime_sha, runtime_size in cases:
            with self.subTest(runtime_sha=runtime_sha, runtime_size=runtime_size):
                with tempfile.TemporaryDirectory() as directory:
                    root = pathlib.Path(directory)
                    self.make_staging_fixture(root, runtime_sha, runtime_size)
                    with patch.object(RELEASE_ASSETS.subprocess, "check_output", side_effect=["2.4.1", "42"]):
                        with self.assertRaises(ValueError):
                            RELEASE_ASSETS.stage_release_assets(root, "v2.4.1", "abc123")

    def test_staging_rejects_missing_runtime_archive(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            _, runtime_path = self.make_staging_fixture(root)
            runtime_path.unlink()
            with patch.object(RELEASE_ASSETS.subprocess, "check_output", side_effect=["2.4.1", "42"]):
                with self.assertRaises(FileNotFoundError):
                    RELEASE_ASSETS.stage_release_assets(root, "v2.4.1", "abc123")


if __name__ == "__main__":
    unittest.main()
