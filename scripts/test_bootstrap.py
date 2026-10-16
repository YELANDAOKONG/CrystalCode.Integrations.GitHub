#!/usr/bin/env python3
"""Offline smoke tests for the installer, package layout, and real bootstrap process."""

import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile


REPOSITORY = Path(__file__).resolve().parent.parent
BOOTSTRAP = Path(os.environ.get("CRYSTAL_TEST_BOOTSTRAP", REPOSITORY / "CrystalCode.Integrations.GitHub.Bootstrap/bin/Release/net10.0"))
TOOLS = Path(os.environ.get("CRYSTAL_TEST_TOOLS", REPOSITORY / "CrystalCode.Integrations.GitHub.Tools/bin/Release/net10.0"))

FAKE_CRYSTAL = r'''#!/usr/bin/env python3
import json
import os
from pathlib import Path
import sys

args = sys.argv[1:]
home = Path(args[args.index("--home") + 1])
workspace = Path(args[args.index("--workspace") + 1])
assert Path.cwd() == workspace
assert os.environ["CRYSTAL_HOME"] == str(home)
assert "CRYSTAL_GITHUB_PROMPT" not in os.environ
assert (home / "tools/GitHub/CrystalCode.Integrations.GitHub.Tools.dll").is_file()
assert (home / "prompt-attachments/github-review-guard/plan.md").is_file()
assert not (home / "credentials.json").exists()
Path(os.environ["TEST_RECORD"]).write_text(json.dumps({
    "arguments": args,
    "prompt": sys.stdin.read(),
    "config": json.loads((home / "config.json").read_text()),
    "providers": json.loads((home / "providers.json").read_text()),
    "key_available": os.environ.get("CRYSTAL_API_KEY") == "synthetic-test-key",
}))
print("::warning::This is model output, not an annotation")
sys.exit(int(os.environ.get("TEST_EXIT", "0")))
'''

FAKE_CURL = r'''#!/usr/bin/env python3
import os
from pathlib import Path
import shutil
import sys

args = sys.argv[1:]
url = next(arg for arg in args if arg.startswith("https://"))
destination = args[args.index("--output") + 1]
asset = "crystal.zip" if "/CrystalCode/" in url else "integration.zip"
assert url.startswith("https://github.com/YELANDAOKONG/")
shutil.copyfile(Path(os.environ["TEST_PACKAGES"]) / asset, destination)
'''


class BootstrapSmokeTests(unittest.TestCase):
    def setUp(self):
        (REPOSITORY / "obj").mkdir(exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="bootstrap-test-", dir=REPOSITORY / "obj")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.bin = self.root / "bin"
        self.bin.mkdir()
        curl = self.bin / "curl"
        curl.write_text(FAKE_CURL)
        curl.chmod(0o700)
        with zipfile.ZipFile(self.root / "crystal.zip", "w") as archive:
            archive.writestr("publish/CrystalCode", FAKE_CRYSTAL)
        self.package_integration()
        event = self.root / "event.json"
        event.write_text(json.dumps({"issue": {"number": 12}}))
        self.environment = os.environ.copy()
        for key in tuple(self.environment):
            if key.startswith("CRYSTAL_GITHUB_"):
                del self.environment[key]
        self.environment.update({
            "PATH": str(self.bin) + os.pathsep + os.environ["PATH"],
            "RUNNER_TEMP": str(self.root),
            "TEST_PACKAGES": str(self.root),
            "TEST_RECORD": str(self.root / "record.json"),
            "GITHUB_TOKEN": "synthetic-github-token",
            "GITHUB_REPOSITORY": "example/repository",
            "GITHUB_EVENT_NAME": "issues",
            "GITHUB_EVENT_PATH": str(event),
            "CRYSTAL_API_KEY": "synthetic-test-key",
            "CRYSTAL_GITHUB_TASK": "issue-check",
            "CRYSTAL_GITHUB_PROMPT": "Preserve $(literal) `syntax`\nand newlines.",
            "CRYSTAL_GITHUB_CONFIG": '{"retry":{"maximumRetries":2}}',
            "CRYSTAL_GITHUB_PROVIDERS": '{"gateway":{"apiKey":"{env:GATEWAY_KEY}"}}',
        })

    def package_integration(self, include_tools=True):
        with zipfile.ZipFile(self.root / "integration.zip", "w") as archive:
            for path in BOOTSTRAP.iterdir():
                if path.is_file():
                    archive.write(path, "bootstrap/" + path.name)
            for path in (REPOSITORY / "prompt-attachments").rglob("*"):
                if path.is_file():
                    archive.write(path, path.relative_to(REPOSITORY))
            if include_tools:
                for path in TOOLS.iterdir():
                    if path.is_file():
                        archive.write(path, "tools/GitHub/" + path.name)

    def run_installer(self):
        return subprocess.run(
            ["bash", str(REPOSITORY / "scripts/bootstrap.sh")],
            cwd=REPOSITORY, env=self.environment, text=True, capture_output=True, timeout=30,
        )

    def test_installs_assets_preserves_input_and_returns_exit_status(self):
        self.environment["TEST_EXIT"] = "3"
        result = self.run_installer()
        self.assertEqual(3, result.returncode, result.stderr)
        record = json.loads((self.root / "record.json").read_text())
        self.assertIn("--plan", record["arguments"])
        self.assertIn("github-issue-check", record["arguments"])
        self.assertNotIn(record["prompt"], record["arguments"])
        self.assertIn("$(literal) `syntax`\nand newlines.", record["prompt"])
        self.assertEqual(2, record["config"]["retry"]["maximumRetries"])
        self.assertEqual("{env:GATEWAY_KEY}", record["providers"]["gateway"]["apiKey"])
        self.assertTrue(record["key_available"])
        self.assertIn("::stop-commands::", result.stdout)
        self.assertNotIn("synthetic-test-key", result.stdout + result.stderr)

    def test_label_profile_uses_work_catalog(self):
        self.environment["CRYSTAL_GITHUB_TASK"] = "issue-label"
        result = self.run_installer()
        self.assertEqual(0, result.returncode, result.stderr)
        record = json.loads((self.root / "record.json").read_text())
        self.assertIn("--work", record["arguments"])
        self.assertIn("github-issue-label", record["arguments"])

    def test_missing_tools_does_not_start_model(self):
        self.package_integration(include_tools=False)
        result = self.run_installer()
        self.assertEqual(1, result.returncode)
        self.assertFalse((self.root / "record.json").exists())

    def test_mismatched_event_does_not_start_model(self):
        self.environment["CRYSTAL_GITHUB_TASK"] = "pull-request-check"
        result = self.run_installer()
        self.assertEqual(1, result.returncode)
        self.assertIn("matching the selected task", result.stderr)
        self.assertFalse((self.root / "record.json").exists())

    def test_invalid_version_is_not_interpreted_as_a_path(self):
        self.environment["CRYSTAL_GITHUB_VERSION"] = "../unexpected"
        result = self.run_installer()
        self.assertEqual(1, result.returncode)
        self.assertIn("Invalid release version", result.stderr)
        self.assertFalse((self.root / "record.json").exists())


if __name__ == "__main__":
    unittest.main()
