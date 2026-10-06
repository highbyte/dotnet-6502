"""Protect the app files served by automatic Pages deployments."""

import io
import json
import shutil
import subprocess
import tarfile
import tempfile
import unittest
import zipfile
from contextlib import redirect_stdout
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

    def test_preserves_the_complete_deployed_site(self):
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
        pages.restore_site(self.archive, self.site)
        for name, data in files.items():
            self.assertEqual((self.site / name).read_bytes(), data)

    def test_production_only_baseline_does_not_invent_test_apps(self):
        self.write_archive({"app/index.html": b"one", "app2/index.html": b"two"})
        pages.restore_site(self.archive, self.site)
        self.assertFalse((self.site / "app-test").exists())

    def test_missing_or_empty_app_stops_before_staging(self):
        for files in ({"app/index.html": b"one"},
                      {"app/index.html": b"one", "app2/index.html": b""}):
            with self.subTest(files=files):
                self.write_archive(files)
                with self.assertRaisesRegex(ValueError, "no usable app2"):
                    pages.restore_site(self.archive, self.site)
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
                    pages.restore_site(self.archive, self.site)
                self.assertFalse(self.site.exists())
        self.assertFalse((self.root / "outside").exists())

    def test_refuses_to_overwrite_existing_staging_files(self):
        self.site.mkdir()
        existing = self.site / "keep.txt"
        existing.write_bytes(b"keep")
        with self.assertRaisesRegex(ValueError, "must not already exist"):
            pages.restore_site(self.archive, self.site)
        self.assertEqual(existing.read_bytes(), b"keep")


class DeploymentSelectionTests(unittest.TestCase):
    def setUp(self):
        self.repository = "owner/repo"
        self.prefix = "repos/owner/repo"
        self.responses = {
            self.prefix: {"id": 123},
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
            f"{self.prefix}/actions/runs/80/jobs?per_page=100": {
                "jobs": [{"steps": [{"name": "Deploy to GitHub Pages", "conclusion": "success"}]}]
            },
            f"{self.prefix}/actions/runs/80/artifacts?per_page=100&page=1": {
                "artifacts": [{"id": 8000, "name": "github-pages", "expired": False}]
            },
        }
        mock = patch.object(pages, "github_json", side_effect=self.responses.__getitem__)
        self.github = mock.start()
        self.addCleanup(mock.stop)
        server = patch.dict("os.environ", {
            "GITHUB_SERVER_URL": "https://github.com", "GITHUB_REPOSITORY": self.repository
        })
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

    def test_missing_deployment_has_no_baseline(self):
        endpoint = f"{self.prefix}/deployments?environment=github-pages&per_page=100&page=1"
        self.responses[endpoint] = []
        self.assertIsNone(pages.latest_deployed_artifact(self.repository))
        with tempfile.TemporaryDirectory() as temporary:
            site = Path(temporary) / "site"
            self.assertFalse(pages.restore_deployed_site(self.repository, site))
            self.assertFalse(site.exists())

    def test_unchanged_runs_do_not_replace_the_actual_deployed_baseline(self):
        self.responses[f"{self.prefix}/actions/runs/80/jobs?per_page=100"]["jobs"][0]["steps"][0]["conclusion"] = "skipped"
        endpoint = f"{self.prefix}/deployments?environment=github-pages&per_page=100&page=1"
        self.responses[endpoint].append({"id": 7, "sha": "baseline"})
        self.responses[f"{self.prefix}/deployments/7/statuses?per_page=100"] = [{
            "state": "success", "log_url": "https://github.com/owner/repo/actions/runs/70/job/700"
        }]
        self.responses[f"{self.prefix}/actions/runs/70"] = (
            self.responses[f"{self.prefix}/actions/runs/80"] | {"head_sha": "baseline"}
        )
        self.responses[f"{self.prefix}/actions/runs/70/jobs?per_page=100"] = {
            "jobs": [{"steps": [{"name": "Deploy to GitHub Pages", "conclusion": "success"}]}]
        }
        self.responses[f"{self.prefix}/actions/runs/70/artifacts?per_page=100&page=1"] = {
            "artifacts": [{"id": 7000, "name": "github-pages", "expired": False}]
        }
        self.assertEqual(pages.latest_deployed_artifact(self.repository), (7000, 70))

    def test_ambiguous_or_failed_deploy_step_is_never_an_unchanged_run(self):
        endpoint = f"{self.prefix}/actions/runs/80/jobs?per_page=100"
        for steps in ([], [{"name": "Deploy to GitHub Pages", "conclusion": "failure"}]):
            with self.subTest(steps=steps):
                self.responses[endpoint] = {"jobs": [{"steps": steps}]}
                with self.assertRaises(ValueError):
                    pages.latest_deployed_artifact(self.repository)

    def test_rejects_invalid_repository_before_any_api_call(self):
        for repository in ("owner/repo?other=1", "owner/repo/extra", "--hostname=example.com"):
            with self.subTest(repository=repository), self.assertRaisesRegex(
                ValueError, "Invalid GitHub repository"
            ):
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

    def test_downloads_and_restores_the_complete_deployed_site(self):
        with tempfile.TemporaryDirectory() as temporary:
            site = Path(temporary) / "site"
            arguments = ["restore_pages_apps.py", "--site", str(site)]
            downloaded = self.artifact_zip(pages.ARCHIVE_NAME)
            with patch("sys.argv", arguments), patch.object(pages.subprocess, "run") as command:
                command.side_effect = lambda *args, **kwargs: kwargs["stdout"].write(downloaded)
                output = io.StringIO()
                with redirect_stdout(output):
                    self.assertTrue(pages.restore_deployed_site(self.repository, site))
                self.assertEqual((site / "app/index.html").read_bytes(), b"app/index.html")
                self.assertEqual((site / "app2/index.html").read_bytes(), b"app2/index.html")
                self.assertEqual((site / "docs/index.html").read_bytes(), b"docs/index.html")
                self.assertIn("workflow run 80", output.getvalue())
                self.assertEqual(command.call_args.args[0], [
                    "gh", "api", "--", "repositories/123/actions/artifacts/8000/zip"
                ])

    def test_rejects_unexpected_zip_contents_before_staging(self):
        with tempfile.TemporaryDirectory() as temporary:
            site = Path(temporary) / "site"
            arguments = ["restore_pages_apps.py", "--site", str(site)]
            downloaded = self.artifact_zip("unexpected.tar")
            with patch("sys.argv", arguments), patch.object(pages.subprocess, "run") as command:
                command.side_effect = lambda *args, **kwargs: kwargs["stdout"].write(downloaded)
                with self.assertRaisesRegex(ValueError, "Unexpected Pages artifact format"):
                    pages.restore_deployed_site(self.repository, site)
            self.assertFalse(site.exists())

    def test_rejects_invalid_repository_context_without_downloading(self):
        for repository in ("owner/repo?other=1", "owner/repo/extra", "", "--hostname=example.com"):
            with self.subTest(repository=repository):
                with patch.dict(
                    "os.environ", {"GITHUB_REPOSITORY": repository}
                ), self.assertRaisesRegex(ValueError, "GITHUB_REPOSITORY"):
                    pages.main()
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


class ChangeDetectionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        self.source.mkdir()
        self.git("init", "--quiet")
        self.commit_file("src/app.txt", "original app")
        self.base = self.commit_file("docs/page.md", "original docs")
        self.sources = {"docs": str(self.source), "production": str(self.source)}
        self.site = self.root / "site"
        self.baseline = {"schema": 1, "commits": {"docs": self.base, "production": self.base},
                         "production_version": "1.0.0"}
        self.files = {
            "app/index.html": b"app v1", "app/old.js": b"old asset", "app2/index.html": b"app2 v1",
            "app-test/index.html": b"test app", "app2-test/index.html": b"test app2",
            "docs/index.html": b"published docs", "CNAME": b"example.com", ".nojekyll": b"",
        }
        self.restore = patch.object(pages, "restore_deployed_site", side_effect=self.restore_baseline)
        self.restore_mock = self.restore.start()
        self.addCleanup(self.restore.stop)

    def git(self, *arguments):
        return subprocess.run(
            ["git", "-C", str(self.source), *arguments],
            check=True, capture_output=True, text=True,
        ).stdout.strip()

    def commit_file(self, name, content):
        path = self.source / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)
        self.git("add", "--", name)
        # These commits exist only in a temporary fixture, never in the project repository.
        self.git("-c", "user.name=Test fixture", "-c", "user.email=fixture@example.invalid",
                 "commit", "--quiet", "-m", "Update fixture")
        return pages.source_commit(str(self.source))

    def restore_baseline(self, repository, destination):
        destination = Path(destination)
        for name, content in self.files.items():
            path = destination / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
        if self.baseline is not None:
            (destination / pages.STATE_NAME).write_text(json.dumps(self.baseline))
        return True

    def prepare(self, **options):
        return pages.prepare_publish("owner/repo", self.site, self.sources, "1.0.0", **options)

    def read_state(self):
        return json.loads((self.site / pages.STATE_NAME).read_text())

    def test_unchanged_inputs_skip_deployment_and_preserve_all_sections(self):
        self.commit_file("README.md", "unrelated change")
        self.assertEqual(self.prepare(), {
            "publish_apps": False, "publish_docs": False, "should_deploy": False
        })
        for name, content in self.files.items():
            self.assertEqual((self.site / name).read_bytes(), content)
        self.assertEqual(self.read_state(), self.baseline)

    def test_docs_only_update_keeps_the_published_app_commit_and_bytes(self):
        current = self.commit_file("docs/page.md", "new docs")
        self.assertEqual(self.prepare(), {
            "publish_apps": False, "publish_docs": True, "should_deploy": True
        })
        self.assertEqual((self.site / "app/old.js").read_bytes(), b"old asset")
        self.assertFalse((self.site / "docs").exists())
        self.assertEqual(self.read_state()["commits"], {"production": self.base, "docs": current})

    def test_app_change_after_docs_only_publish_uses_the_older_app_baseline(self):
        docs_commit = self.commit_file("docs/page.md", "new docs")
        self.prepare()
        docs = self.site / "docs/index.html"
        docs.parent.mkdir()
        docs.write_bytes(b"new published docs")
        snapshot = self.root / "published"
        shutil.copytree(self.site, snapshot)
        self.restore_mock.side_effect = lambda repository, site: shutil.copytree(snapshot, site)
        self.site = self.root / "next-site"
        app_commit = self.commit_file("src/app.txt", "new app")
        result = self.prepare()
        self.assertTrue(result["publish_apps"])
        self.assertFalse(result["publish_docs"])
        self.assertEqual((self.site / "docs/index.html").read_bytes(), b"new published docs")
        self.assertEqual(self.read_state()["commits"], {
            "docs": docs_commit, "production": app_commit
        })

    def test_app_update_keeps_docs_and_unselected_test_apps_and_removes_stale_assets(self):
        current = self.commit_file("src/app.txt", "new app")
        self.assertEqual(self.prepare(), {
            "publish_apps": True, "publish_docs": False, "should_deploy": True
        })
        self.assertFalse((self.site / "app").exists())
        self.assertFalse((self.site / "app2").exists())
        self.assertEqual((self.site / "docs/index.html").read_bytes(), b"published docs")
        self.assertEqual((self.site / "app-test/index.html").read_bytes(), b"test app")
        self.assertEqual(self.read_state()["commits"], {"production": current, "docs": self.base})

    def test_dependency_and_workflow_changes_trigger_the_right_groups(self):
        cases = (("Directory.Packages.props", True, False), ("global.json", True, False),
                 ("requirements-docs.txt", False, True), ("mkdocs.yml", False, True),
                 ("includes/snippet.md", False, True),
                 (pages.WORKFLOW_PATH, True, True),
                 (".github/scripts/restore_pages_apps.py", True, True))
        for index, (name, apps, docs) in enumerate(cases):
            with self.subTest(name=name):
                self.base = pages.source_commit(str(self.source))
                self.baseline["commits"] = {"docs": self.base, "production": self.base}
                self.site = self.root / f"site-{index}"
                self.commit_file(name, "changed")
                result = self.prepare()
                self.assertEqual((result["publish_apps"], result["publish_docs"]), (apps, docs))

    def test_first_deployment_and_old_artifacts_without_metadata_rebuild_everything(self):
        self.baseline = None
        self.assertTrue(all(self.prepare().values()))
        self.assertEqual(self.read_state()["commits"], {"docs": self.base, "production": self.base})
        self.site = self.root / "first-site"
        self.restore_mock.side_effect = None
        self.restore_mock.return_value = False
        self.assertTrue(all(self.prepare().values()))

    def test_force_publish_bypasses_deployment_history_and_all_comparisons(self):
        self.restore_mock.side_effect = ValueError("Expired artifact or broken history")
        with patch.object(pages, "inputs_changed", side_effect=ValueError("Broken comparison")) as compare:
            self.assertTrue(all(self.prepare(force=True).values()))
            compare.assert_not_called()
        self.restore_mock.assert_not_called()
        self.assertEqual(self.read_state()["commits"], {"docs": self.base, "production": self.base})

    def test_release_and_version_change_rebuild_apps_even_when_source_is_unchanged(self):
        self.assertEqual(self.prepare(release=True), {
            "publish_apps": True, "publish_docs": False, "should_deploy": True
        })
        self.site = self.root / "new-version"
        result = pages.prepare_publish("owner/repo", self.site, self.sources, "1.1.0")
        self.assertTrue(result["publish_apps"])
        self.assertFalse(result["publish_docs"])
        self.assertEqual(self.read_state()["production_version"], "1.1.0")

    def test_selected_test_apps_get_a_separate_baseline(self):
        self.sources["test"] = str(self.source)
        self.assertTrue(self.prepare()["publish_apps"])
        self.assertFalse((self.site / "app-test").exists())
        self.assertFalse((self.site / "app2-test").exists())
        self.assertEqual(self.read_state()["commits"]["test"], self.base)

    def test_invalid_commit_or_failed_comparison_stops_instead_of_skipping(self):
        with self.assertRaisesRegex(ValueError, "Invalid published commit"):
            pages.inputs_changed(str(self.source), "--output=/tmp/file", self.base, pages.APP_PATHS)
        with self.assertRaisesRegex(ValueError, "Cannot compare"):
            pages.inputs_changed(str(self.source), "f" * 40, self.base, pages.APP_PATHS)

    def test_bad_metadata_stops_and_force_publish_can_recover(self):
        self.baseline = {"schema": 99, "commits": []}
        with self.assertRaisesRegex(ValueError, "Invalid publish state"):
            self.prepare()
        self.site = self.root / "recovery-site"
        self.assertTrue(all(self.prepare(force=True).values()))
        (self.site / pages.STATE_NAME).write_text("{invalid json")
        with self.assertRaisesRegex(ValueError, "Force publish"):
            pages.published_state(self.site)

    def test_existing_directory_is_never_overwritten(self):
        self.site.mkdir()
        keep = self.site / "keep.txt"
        keep.write_bytes(b"keep")
        with self.assertRaisesRegex(ValueError, "must not already exist"):
            self.prepare(force=True)
        self.assertEqual(keep.read_bytes(), b"keep")
        self.restore_mock.assert_not_called()

    def test_cli_force_and_target_write_boolean_workflow_outputs(self):
        output = self.root / "outputs"
        env = {"GITHUB_REPOSITORY": "owner/repo", "GITHUB_OUTPUT": str(output),
               "DOCS_SOURCE_DIR": str(self.source), "PRODUCTION_SOURCE_DIR": str(self.source),
               "TEST_SOURCE_DIR": str(self.source), "PRODUCTION_VERSION": "1.0.0",
               "PUBLISH_TARGET": "all", "FORCE_PUBLISH": "true", "IS_RELEASE": "false",
               "PAGES_SITE_DIR": str(self.site)}
        with patch.dict("os.environ", env), redirect_stdout(io.StringIO()):
            pages.main()
        self.assertEqual(output.read_text(), "publish_apps=true\npublish_docs=true\nshould_deploy=true\n")
        self.assertEqual(self.read_state()["commits"]["test"], self.base)
        self.restore_mock.assert_not_called()


if __name__ == "__main__":
    unittest.main()
