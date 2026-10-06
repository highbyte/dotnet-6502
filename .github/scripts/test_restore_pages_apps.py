"""Protect the app files served by docs-only Pages deployments."""

import io
import json
import tarfile
import tempfile
import unittest
import zipfile
from contextlib import redirect_stdout, redirect_stderr
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import restore_pages_apps as pages


class RestoreAppsTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.archive = self.root / "artifact.tar"
        self.site = self.root / "site"

    def write_archive(self, files, extra=None):
        with tarfile.open(self.archive, "w") as archive:
            for name, data in files.items():
                member = tarfile.TarInfo(name)
                member.size = len(data)
                archive.addfile(member, io.BytesIO(data))
            if extra is not None:
                archive.addfile(extra)

    def test_preserves_all_apps_and_site_files_but_discards_old_docs(self):
        files = {
            "./app/index.html": b"Blazor production",
            "./app/_framework/dotnet.wasm": b"\x00\x01\xffproduction",
            "./app2/index.html": b"Avalonia 0.47.2-alpha",
            "./app-test/index.html": b"Blazor test",
            "./app2-test/_framework/dotnet.wasm": b"\x00\x02\xfftest",
            "./app2-test/index.html": b"Avalonia test version",
            "./.nojekyll": b"",
            "./CNAME": b"highbyte.se",
            "./docs/index.html": b"old docs",
            "./docs/assets/search.js": b"old search",
        }
        self.write_archive(files)
        pages.restore_apps(self.archive, self.site)
        for name, data in files.items():
            if not name.startswith("./docs/"):
                self.assertEqual((self.site / name).read_bytes(), data)
        self.assertFalse((self.site / "docs").exists())

    def test_production_only_baseline_does_not_invent_test_apps(self):
        self.write_archive({"app/index.html": b"one", "app2/index.html": b"two"})
        pages.restore_apps(self.archive, self.site)
        self.assertFalse((self.site / "app-test").exists())

    def test_missing_or_empty_app_stops_before_staging(self):
        for files in ({"app/index.html": b"one"},
                      {"app/index.html": b"one", "app2/index.html": b""}):
            with self.subTest(files=files):
                self.write_archive(files)
                with self.assertRaisesRegex(ValueError, "no usable app2"):
                    pages.restore_apps(self.archive, self.site)
                self.assertFalse(self.site.exists())

    def test_rejects_traversal_absolute_paths_and_links(self):
        unsafe = [tarfile.TarInfo("../outside"), tarfile.TarInfo("/absolute")]
        for kind in (tarfile.SYMTYPE, tarfile.LNKTYPE):
            member = tarfile.TarInfo("app/link")
            member.type = kind
            member.linkname = "../../outside"
            unsafe.append(member)
        for member in unsafe:
            with self.subTest(name=member.name, kind=member.type):
                self.write_archive(
                    {"app/index.html": b"one", "app2/index.html": b"two"}, member
                )
                with self.assertRaises(ValueError):
                    pages.restore_apps(self.archive, self.site)
                self.assertFalse(self.site.exists())
        self.assertFalse((self.root / "outside").exists())

    def test_refuses_to_overwrite_existing_staging_files(self):
        self.site.mkdir()
        existing = self.site / "keep.txt"
        existing.write_bytes(b"keep")
        with self.assertRaisesRegex(ValueError, "must not already exist"):
            pages.restore_apps(self.archive, self.site)
        self.assertEqual(existing.read_bytes(), b"keep")


class DeploymentSelectionTests(unittest.TestCase):
    def setUp(self):
        self.repository = "owner/repo"
        self.prefix = "repos/owner/repo"
        self.responses = {
            f"{self.prefix}/deployments?environment=github-pages&per_page=100&page=1": [
                {"id": 9, "sha": "failed"},
                {"id": 8, "sha": "deployed"},
            ],
            f"{self.prefix}/deployments/9/statuses?per_page=100": [{"state": "failure"}],
            f"{self.prefix}/deployments/8/statuses?per_page=100": [{
                "state": "success",
                "log_url": "https://github.com/owner/repo/actions/runs/80/job/800",
            }],
            f"{self.prefix}/actions/runs/80": {
                "path": pages.WORKFLOW_PATH, "head_sha": "deployed",
                "event": "workflow_dispatch", "status": "completed",
                "conclusion": "success", "head_repository": {"full_name": self.repository},
            },
            f"{self.prefix}/actions/runs/80/artifacts?per_page=100&page=1": {
                "artifacts": [{"id": 8000, "name": "github-pages", "expired": False}]
            },
        }
        mock = patch.object(pages, "github_json", side_effect=self.responses.__getitem__)
        self.github = mock.start()
        self.addCleanup(mock.stop)
        server = patch.dict("os.environ", {"GITHUB_SERVER_URL": "https://github.com"})
        server.start()
        self.addCleanup(server.stop)

    def test_uses_latest_successful_deployment_not_latest_build(self):
        self.assertEqual(pages.latest_deployed_artifact(self.repository), (8000, 80))

    def test_expired_or_missing_latest_artifact_never_falls_back(self):
        endpoint = f"{self.prefix}/actions/runs/80/artifacts?per_page=100&page=1"
        for artifacts in ([], [{"id": 8000, "name": "github-pages", "expired": True}]):
            with self.subTest(artifacts=artifacts):
                self.responses[endpoint] = {"artifacts": artifacts}
                with self.assertRaisesRegex(ValueError, "missing or expired"):
                    pages.latest_deployed_artifact(self.repository)

    def test_rejects_other_workflows_forks_failed_runs_or_different_commits(self):
        endpoint = f"{self.prefix}/actions/runs/80"
        original = self.responses[endpoint].copy()
        for change in ({"path": "other.yml"}, {"event": "pull_request"},
                       {"head_sha": "other"}, {"status": "in_progress"},
                       {"conclusion": "failure"},
                       {"head_repository": {"full_name": "fork/repo"}}):
            with self.subTest(change=change):
                self.responses[endpoint] = original | change
                with self.assertRaisesRegex(ValueError, "successful Pages publish run"):
                    pages.latest_deployed_artifact(self.repository)

    def test_rejects_unrelated_log_url(self):
        endpoint = f"{self.prefix}/deployments/8/statuses?per_page=100"
        self.responses[endpoint][0]["log_url"] = "https://example.com/actions/runs/80/job/800"
        with self.assertRaisesRegex(ValueError, "Cannot identify"):
            pages.latest_deployed_artifact(self.repository)

    def test_reads_next_deployment_page(self):
        endpoint = f"{self.prefix}/deployments?environment=github-pages&per_page=100&page="
        original = self.responses[endpoint + "1"]
        self.responses[endpoint + "1"] = original[:1]
        self.responses[endpoint + "2"] = original[1:]
        self.assertEqual(pages.latest_deployed_artifact(self.repository), (8000, 80))

    def test_missing_deployment_requires_full_publish(self):
        endpoint = f"{self.prefix}/deployments?environment=github-pages&per_page=100&page=1"
        self.responses[endpoint] = []
        with self.assertRaisesRegex(ValueError, "publish apps first"):
            pages.latest_deployed_artifact(self.repository)

    def test_rejects_invalid_repository_before_any_api_call(self):
        for repository in ("owner/repo?other=1", "owner/repo/extra", "--hostname=example.com"):
            with self.subTest(repository=repository):
                with self.assertRaisesRegex(ValueError, "Invalid GitHub repository"):
                    pages.latest_deployed_artifact(repository)
        self.github.assert_not_called()

    def test_reads_next_artifact_page_and_rejects_duplicates(self):
        endpoint = f"{self.prefix}/actions/runs/80/artifacts?per_page=100&page="
        deployed = self.responses[endpoint + "1"]["artifacts"]
        self.responses[endpoint + "1"] = {"artifacts": [
            {"name": "other-artifact"} for _ in range(100)
        ]}
        self.responses[endpoint + "2"] = {"artifacts": deployed}
        self.assertEqual(pages.latest_deployed_artifact(self.repository), (8000, 80))
        self.responses[endpoint + "2"] = {"artifacts": deployed * 2}
        with self.assertRaisesRegex(ValueError, "missing or expired"):
            pages.latest_deployed_artifact(self.repository)

    def artifact_zip(self, member_name):
        archive_bytes = io.BytesIO()
        with tarfile.open(fileobj=archive_bytes, mode="w") as archive:
            for name in ("app/index.html", "app2/index.html", "docs/index.html"):
                data = name.encode()
                member = tarfile.TarInfo(name)
                member.size = len(data)
                archive.addfile(member, io.BytesIO(data))
        result = io.BytesIO()
        with zipfile.ZipFile(result, "w") as zipped:
            zipped.writestr(member_name, archive_bytes.getvalue())
        return result.getvalue()

    def test_cli_downloads_and_restores_only_the_deployed_apps(self):
        with tempfile.TemporaryDirectory() as temporary:
            site = Path(temporary) / "site"
            arguments = ["restore_pages_apps.py", "--repository", self.repository, "--site", str(site)]
            downloaded = self.artifact_zip(pages.ARCHIVE_NAME)
            with patch("sys.argv", arguments), patch.object(pages.subprocess, "run") as command:
                command.side_effect = lambda *args, **kwargs: kwargs["stdout"].write(downloaded)
                output = io.StringIO()
                with redirect_stdout(output):
                    pages.main()
                self.assertEqual((site / "app/index.html").read_bytes(), b"app/index.html")
                self.assertEqual((site / "app2/index.html").read_bytes(), b"app2/index.html")
                self.assertFalse((site / "docs").exists())
                self.assertIn("workflow run 80", output.getvalue())
                self.assertEqual(command.call_args.args[0], [
                    "gh", "api", "--", "repos/owner/repo/actions/artifacts/8000/zip"
                ])

    def test_cli_rejects_unexpected_zip_contents_before_staging(self):
        with tempfile.TemporaryDirectory() as temporary:
            site = Path(temporary) / "site"
            arguments = ["restore_pages_apps.py", "--repository", self.repository, "--site", str(site)]
            downloaded = self.artifact_zip("unexpected.tar")
            with patch("sys.argv", arguments), patch.object(pages.subprocess, "run") as command:
                command.side_effect = lambda *args, **kwargs: kwargs["stdout"].write(downloaded)
                with self.assertRaisesRegex(ValueError, "Unexpected Pages artifact format"):
                    pages.main()
            self.assertFalse(site.exists())

    def test_cli_rejects_invalid_repository_without_downloading(self):
        for repository in ("owner/repo?other=1", "owner/repo/extra", "", "--hostname=example.com"):
            with self.subTest(repository=repository):
                arguments = ["restore_pages_apps.py", f"--repository={repository}", "--site", "site"]
                with patch("sys.argv", arguments), redirect_stderr(io.StringIO()):
                    with self.assertRaises(SystemExit) as error:
                        pages.main()
                    self.assertEqual(error.exception.code, 2)
        self.github.assert_not_called()


class GitHubApiTests(unittest.TestCase):
    def test_api_passes_endpoint_as_positional_argument_and_decodes_json(self):
        response = {"id": 8000}
        with patch.object(pages.subprocess, "run", return_value=SimpleNamespace(
            stdout=json.dumps(response)
        )) as command:
            self.assertEqual(pages.github_json("repos/owner/repo/actions/artifacts/8000"), response)
            command.assert_called_once_with(
                ["gh", "api", "--", "repos/owner/repo/actions/artifacts/8000"],
                check=True, capture_output=True, text=True,
            )


if __name__ == "__main__":
    unittest.main()
