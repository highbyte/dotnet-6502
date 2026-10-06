"""Prepare Pages publishing by comparing inputs with the successfully deployed site."""

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
STATE_NAME = ".publish-state.json"
APP_PATHS = (
    "src/", "Directory.Packages.props", "Directory.Build.props", "Directory.Build.targets",
    "global.json", "NuGet.Config", WORKFLOW_PATH,
    ".github/scripts/restore_pages_apps.py",
)
DOCS_PATHS = (
    "docs/", "includes/", "mkdocs.yml", "requirements-docs.in", "requirements-docs.txt",
    ".pip-tools.toml", WORKFLOW_PATH,
    ".github/scripts/restore_pages_apps.py",
)


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
    jobs = github_json(f"{prefix}/actions/runs/{run_id}/jobs?per_page=100")["jobs"]
    steps = [step for job in jobs for step in job["steps"]
             if step["name"] == "Deploy to GitHub Pages"]
    if len(steps) != 1:
        raise ValueError("Cannot verify the Pages deployment step")
    if steps[0]["conclusion"] == "skipped":
        return None
    if steps[0]["conclusion"] != "success":
        raise ValueError("Pages deployment step did not succeed")
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
            "run again with Force publish enabled"
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
            return None
        for deployment in deployments:
            run_id = deployed_run(repository, deployment)
            if run_id is not None:
                return pages_artifact(repository, run_id), run_id
        page += 1


def restore_site(archive_path, destination):
    """Retain every published file until its section is explicitly rebuilt."""
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
            archive.extractall(staged, members=members, filter="data")
        for app in ("app", "app2"):
            index = staged / app / "index.html"
            if not index.is_file() or index.stat().st_size == 0:
                raise ValueError(f"Deployed artifact has no usable {app}/index.html")
        shutil.copytree(staged, destination)


def restore_deployed_site(repository, destination):
    deployed = latest_deployed_artifact(repository)
    if deployed is None:
        return False
    artifact_id, run_id = deployed
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
        restore_site(archive_path, destination)
    print(f"Restored site from Pages workflow run {run_id}")
    return True


def source_commit(source):
    result = subprocess.run(
        ["git", "-C", source, "rev-parse", "HEAD"],
        check=True, capture_output=True, text=True,
    )
    return result.stdout.strip()


def inputs_changed(source, previous, current, paths):
    if previous is None:
        return True
    for commit in (previous, current):
        if not isinstance(commit, str) or not re.fullmatch(r"[0-9a-f]{40}", commit):
            raise ValueError("Invalid published commit; run again with Force publish enabled")
    result = subprocess.run(
        ["git", "-C", source, "diff", "--quiet", previous, current, "--", *paths],
        check=False, capture_output=True,
    )
    if result.returncode not in (0, 1):
        raise ValueError("Cannot compare published commits; run again with Force publish enabled")
    return result.returncode == 1


def published_state(destination):
    state_path = destination / STATE_NAME
    if not state_path.exists():
        return {"commits": {}}
    try:
        state = json.loads(state_path.read_text())
    except json.JSONDecodeError as error:
        raise ValueError("Invalid publish state; run again with Force publish enabled") from error
    if not isinstance(state, dict) or state.get("schema") != 1 or not isinstance(state.get("commits"), dict):
        raise ValueError("Invalid publish state; run again with Force publish enabled")
    return state


def replace_changed_sections(destination, previous, current, apps_changed, docs_changed):
    commits = previous.copy()
    sections = []
    if apps_changed:
        sections.extend(("app", "app2"))
        if "test" in current:
            sections.extend(("app-test", "app2-test"))
        commits.update({name: commit for name, commit in current.items() if name != "docs"})
    if docs_changed:
        sections.append("docs")
        commits["docs"] = current["docs"]
    for section in sections:
        path = destination / section
        if path.exists():
            shutil.rmtree(path)
    return commits


def prepare_publish(repository, destination, sources, production_version, force=False, release=False):
    destination = Path(destination)
    if destination.exists():
        raise ValueError("Pages staging directory must not already exist")
    current = {name: source_commit(source) for name, source in sources.items()}
    if not force:
        restore_deployed_site(repository, destination)
    destination.mkdir(exist_ok=True)
    state = published_state(destination)
    previous = state["commits"]
    apps_changed = force or release or state.get("production_version") != production_version or any(
        inputs_changed(source, previous.get(name), current[name], APP_PATHS)
        for name, source in sources.items() if name != "docs"
    )
    docs_changed = force or inputs_changed(
        sources["docs"], previous.get("docs"), current["docs"], DOCS_PATHS
    )
    commits = replace_changed_sections(destination, previous, current, apps_changed, docs_changed)
    (destination / STATE_NAME).write_text(json.dumps({
        "schema": 1, "commits": commits, "production_version": production_version
    }, indent=2))
    return {"publish_apps": apps_changed, "publish_docs": docs_changed,
            "should_deploy": apps_changed or docs_changed}


def main():
    repository = os.environ.get("GITHUB_REPOSITORY")
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository or ""):
        raise ValueError("GITHUB_REPOSITORY must be an owner/repository name")
    sources = {"docs": os.environ["DOCS_SOURCE_DIR"],
               "production": os.environ["PRODUCTION_SOURCE_DIR"]}
    if os.environ.get("PUBLISH_TARGET") in {"test", "all"}:
        sources["test"] = os.environ["TEST_SOURCE_DIR"]
    outputs = prepare_publish(
        repository, os.environ["PAGES_SITE_DIR"], sources, os.environ["PRODUCTION_VERSION"],
        force=os.environ.get("FORCE_PUBLISH") == "true",
        release=os.environ.get("IS_RELEASE") == "true",
    )
    report = "".join(f"{name}={str(value).lower()}\n" for name, value in outputs.items())
    print(report, end="")
    with Path(os.environ["GITHUB_OUTPUT"]).open("a") as output:
        output.write(report)


if __name__ == "__main__":
    main()
