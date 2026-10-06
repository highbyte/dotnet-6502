"""Restore unchanged app files from the site's latest successful Pages deployment."""

import argparse
import json
import os
import re
import shutil
import subprocess
import tarfile
import tempfile
import zipfile
from pathlib import Path, PurePosixPath


WORKFLOW_PATH = ".github/workflows/pages-publish.yml"
ARCHIVE_NAME = "artifact.tar"


def github_json(endpoint):
    result = subprocess.run(
        ["gh", "api", "--", endpoint], check=True, capture_output=True, text=True
    )
    return json.loads(result.stdout)


def deployed_run(repository, deployment):
    prefix = f"repos/{repository}"
    statuses = github_json(
        f"{prefix}/deployments/{deployment['id']}/statuses?per_page=100"
    )
    success = next((s for s in statuses if s["state"] == "success"), None)
    if success is None:
        return None
    server = os.environ.get("GITHUB_SERVER_URL", "https://github.com")
    match = re.fullmatch(
        re.escape(f"{server}/{repository}/actions/runs/") + r"(\d+)/job/\d+",
        success.get("log_url", ""),
    )
    if match is None:
        raise ValueError("Cannot identify the latest successful Pages workflow run")
    run_id = int(match[1])
    run = github_json(f"{prefix}/actions/runs/{run_id}")
    if (
        run["path"] != WORKFLOW_PATH
        or run["event"] not in {"push", "workflow_dispatch"}
        or run["head_sha"] != deployment["sha"]
        or run["status"] != "completed"
        or run["conclusion"] != "success"
        or run["head_repository"]["full_name"] != repository
    ):
        raise ValueError("Latest deployment is not from a successful Pages publish run")
    return run_id


def pages_artifact(repository, run_id):
    artifacts = []
    page = 1
    while True:
        batch = github_json(
            f"repos/{repository}/actions/runs/{run_id}/artifacts?per_page=100&page={page}"
        )["artifacts"]
        artifacts.extend(a for a in batch if a["name"] == "github-pages")
        if len(batch) < 100:
            break
        page += 1
    if len(artifacts) != 1 or artifacts[0]["expired"]:
        raise ValueError(
            "The latest deployed Pages artifact is missing or expired; "
            "run again with Publish apps enabled"
        )
    return int(artifacts[0]["id"])


def latest_deployed_artifact(repository):
    """Use the deployed site, never an older artifact or an un-deployed build."""
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise ValueError("Invalid GitHub repository name")
    page = 1
    while True:
        deployments = github_json(
            f"repos/{repository}/deployments?environment=github-pages&per_page=100&page={page}"
        )
        if not deployments:
            raise ValueError("No successful Pages deployment exists; publish apps first")
        for deployment in deployments:
            run_id = deployed_run(repository, deployment)
            if run_id is not None:
                return pages_artifact(repository, run_id), run_id
        page += 1


def restore_apps(archive_path, destination):
    """Replace docs later, retaining every other published file byte-for-byte."""
    destination = Path(destination)
    if destination.exists():
        raise ValueError("Pages staging directory must not already exist")
    with tempfile.TemporaryDirectory() as temporary:
        staged = Path(temporary) / "site"
        staged.mkdir()
        with tarfile.open(archive_path) as archive:
            members = archive.getmembers()
            for member in members:
                path = PurePosixPath(member.name)
                if path.is_absolute() or ".." in path.parts:
                    raise ValueError("Unsafe path in Pages artifact")
                if not (member.isfile() or member.isdir()):
                    raise ValueError("Pages artifact must contain only files and directories")
            preserved = [
                member for member in members
                if PurePosixPath(member.name).parts[:1] != ("docs",)
            ]
            archive.extractall(staged, members=preserved, filter="data")
        for app in ("app", "app2"):
            index = staged / app / "index.html"
            if not index.is_file() or index.stat().st_size == 0:
                raise ValueError(f"Deployed artifact has no usable {app}/index.html")
        shutil.copytree(staged, destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", default=os.environ.get("GITHUB_REPOSITORY"))
    parser.add_argument("--site", required=True)
    args = parser.parse_args()
    repository = args.repository
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository or ""):
        parser.error("--repository or GITHUB_REPOSITORY must be an owner/repository name")
    artifact_id, run_id = latest_deployed_artifact(repository)
    # The download command uses only numeric IDs returned by GitHub.
    repository_id = int(github_json(f"repos/{repository}")["id"])
    with tempfile.TemporaryDirectory() as temporary:
        downloaded = Path(temporary) / "pages.zip"
        with downloaded.open("wb") as output:
            subprocess.run(
                ["gh", "api", "--", f"repositories/{repository_id}/actions/artifacts/{artifact_id}/zip"],
                stdout=output, check=True,
            )
        archive_path = Path(temporary) / ARCHIVE_NAME
        with zipfile.ZipFile(downloaded) as artifact:
            if artifact.namelist() != [ARCHIVE_NAME]:
                raise ValueError("Unexpected Pages artifact format")
            with artifact.open(ARCHIVE_NAME) as source, archive_path.open("wb") as target:
                shutil.copyfileobj(source, target)
        restore_apps(archive_path, args.site)
    print(f"Preserved app files from Pages workflow run {run_id}")


if __name__ == "__main__":
    main()
