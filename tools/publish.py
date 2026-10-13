#!/usr/bin/env python3
"""Publish a release of the package zips (thunderstore/build.sh) everywhere they go:
a GitHub release on the version's tag (both zips), Thunderstore and Hexium (the Thunderstore zip)
and Nexus Mods (<zip> with -nexus: unzipped into the game folder, as Nexus users install).

Usage: tools/publish.py <zip> <version>     (needs Python 3.10+ and httpx)

Every site is optional: one whose token is not set is skipped with a note. A site that already
has this version is skipped too, so a release that failed half-way can simply be run again.
All sites are tried even when one fails; the script fails at the end if any did.

Environment (the CircleCI context mod-token, shared by the mods' projects):
  GITHUB_TOKEN         a token that can create releases on this repository
  THUNDERSTORE_TOKEN   a service account token of the Thunderstore team
  HEXIUM_TOKEN         a team API token of the Hexium team
  NEXUS_API_KEY        a Nexus Mods personal API key
  NEXUS_MOD_ID         the mod's number on Nexus Mods (the page is made on the website first)
Uploads go to the same sites as the MCP servers in _src/*-mcp do, through the same API calls.
"""
import base64
import hashlib
import json
import os
import re
import sys
import time
import zipfile
from pathlib import Path

import httpx

REPO = "Shuiei/valheim-moder-warehouse-plus"
TEAM = "Tie"
COMMUNITY = "valheim"
THUNDERSTORE_CATEGORIES = ["server-side", "utility"]
HEXIUM_CATEGORIES = ["Open Source", "Quality of Life", "Storage"]
HEXIUM_INSTALL_LOCATION = "server"
NEXUS_GAME = "valheim"
USER_AGENT = "ModerWarehousePlus-publish/1.0"


class Skip(Exception):
    """Nothing to do on that site (no token, or the version is already there)."""


def changelog_section(version: str) -> str:
    """The text under '## <version>' in thunderstore/CHANGELOG.md."""
    text = (Path(__file__).resolve().parent.parent / "thunderstore" / "CHANGELOG.md").read_text(encoding="utf-8")
    m = re.search(rf"^## {re.escape(version)}\s*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
    if not m:
        raise SystemExit(f"CHANGELOG.md has no '## {version}' section.")
    return m.group(1).strip()


def check(res: httpx.Response, what: str) -> httpx.Response:
    if res.status_code >= 400:
        raise RuntimeError(f"{what}: {res.status_code} {res.text[:400]}")
    return res


def token(name: str) -> str:
    value = os.environ.get(name)
    if not value:
        raise Skip(f"{name} is not set")
    return value


# ---- GitHub: a release on tag v<version>, with the zip attached.

def nexus_zip(zip_path: Path) -> Path:
    return zip_path.with_name(zip_path.stem + "-nexus.zip")


def github(zip_path: Path, version: str, notes: str) -> str:
    auth = {"Authorization": f"Bearer {token('GITHUB_TOKEN')}", "Accept": "application/vnd.github+json", "User-Agent": USER_AGENT}
    tag = f"v{version}"
    with httpx.Client(base_url="https://api.github.com", headers=auth, timeout=60) as gh:
        res = gh.get(f"/repos/{REPO}/releases/tags/{tag}")
        if res.status_code == 200:
            release = res.json()
        elif res.status_code != 404:
            raise RuntimeError(f"Looking up the release: {res.status_code} {res.text[:400]}")
        else:
            release = check(gh.post(f"/repos/{REPO}/releases", json={"tag_name": tag, "name": f"Moder Warehouse Plus {version}", "body": notes}),
                            "Creating the release").json()
        attached = {a["name"] for a in release.get("assets", [])}
        missing = [p for p in (zip_path, nexus_zip(zip_path)) if p.exists() and p.name not in attached]
        if not missing:
            raise Skip(f"release {tag} already has its zips")
        upload = release["upload_url"].split("{")[0]
        for path in missing:
            check(gh.post(upload, params={"name": path.name}, content=path.read_bytes(),
                          headers={"Content-Type": "application/zip"}, timeout=300), f"Uploading {path.name} to the release")
        return release["html_url"]


# ---- Thunderstore and Hexium: the same API (Hexium is Thunderstore-compatible).

def thunderstore_like(site: str, base_url: str, token_name: str, zip_path: Path, version: str,
                      categories: list[str]) -> httpx.Client:
    """Uploads and submits the zip; returns the client for any follow-up call."""
    auth = {"Authorization": f"Bearer {token(token_name)}"}
    name = json.loads(zipfile.ZipFile(zip_path).read("manifest.json"))["name"]
    client = httpx.Client(base_url=base_url, headers={"User-Agent": USER_AGENT}, timeout=60, follow_redirects=True)
    res = client.get(f"/api/experimental/package/{TEAM}/{name}/{version}/")
    if res.status_code == 200:
        raise Skip(f"{TEAM}-{name}-{version} is already on {site}")
    # Upload in the parts the site asks for, then submit.
    init = check(client.post("/api/experimental/usermedia/initiate-upload/", headers=auth,
                             json={"filename": zip_path.name, "file_size_bytes": zip_path.stat().st_size}),
                 f"{site}: starting the upload").json()
    uuid = init["user_media"]["uuid"]
    try:
        parts = []
        with zip_path.open("rb") as f:
            for part in init["upload_urls"]:
                f.seek(part["offset"])
                data = f.read(part["length"])
                # Pre-signed storage URLs get no token; the site's own upload endpoint does.
                same_site = httpx.URL(part["url"]).host in ("", httpx.URL(base_url).host)
                res = check(client.put(part["url"], content=data, timeout=300, headers=auth if same_site else {}),
                            f"{site}: uploading part {part['part_number']}")
                parts.append({"ETag": res.headers.get("ETag", ""), "PartNumber": part["part_number"]})
        check(client.post(f"/api/experimental/usermedia/{uuid}/finish-upload/", headers=auth, json={"parts": parts}),
              f"{site}: finishing the upload")
    except BaseException:
        client.post(f"/api/experimental/usermedia/{uuid}/abort-upload/", headers=auth, json={})
        raise
    body = {"upload_uuid": uuid, "author_name": TEAM, "communities": [COMMUNITY],
            "community_categories": {COMMUNITY: categories}, "has_nsfw_content": False}
    check(client.post("/api/experimental/submission/submit/", headers=auth, json=body, timeout=600), f"{site}: submitting")
    return client


def thunderstore(zip_path: Path, version: str, notes: str) -> str:
    thunderstore_like("Thunderstore", "https://thunderstore.io", "THUNDERSTORE_TOKEN", zip_path, version, THUNDERSTORE_CATEGORIES).close()
    return f"https://thunderstore.io/c/{COMMUNITY}/p/{TEAM}/ModerWarehousePlus/"


def hexium(zip_path: Path, version: str, notes: str) -> str:
    with thunderstore_like("Hexium", "https://valheim.hexium.gg", "HEXIUM_TOKEN", zip_path, version, HEXIUM_CATEGORIES) as client:
        # Hexium shows where a mod installs as its own setting.
        check(client.post(f"/api/mod/{TEAM}/ModerWarehousePlus/set-metadata", headers={"Authorization": f"Bearer {os.environ['HEXIUM_TOKEN']}"},
                          json={"install_location": HEXIUM_INSTALL_LOCATION, "categories": HEXIUM_CATEGORIES}),
              "Hexium: setting the install location")
    return f"https://valheim.hexium.gg/mods/{TEAM}/ModerWarehousePlus"


# ---- Nexus Mods: a new version of the mod's main file (or its first file).

def nexus(zip_path: Path, version: str, notes: str) -> str:
    key = token("NEXUS_API_KEY")
    mod_id = token("NEXUS_MOD_ID")
    zip_path = nexus_zip(zip_path)
    headers = {"apikey": key, "User-Agent": USER_AGENT, "Application-Name": "ModerWarehousePlus-publish", "Application-Version": "1.0"}
    with httpx.Client(base_url="https://api.nexusmods.com/v3", headers=headers, timeout=60, follow_redirects=True) as nx:
        def v3(method: str, path: str, what: str, **kw):
            out = check(nx.request(method, path, **kw), f"Nexus Mods: {what}").json()
            return out.get("data", out) if isinstance(out, dict) else out

        uid = v3("GET", f"/games/{NEXUS_GAME}/mods/{mod_id}", "finding the mod")["id"]
        files = [f for f in v3("GET", f"/mods/{uid}/files", "listing the files")["mod_files"] if f.get("is_active", True)]
        if len(files) > 1:
            raise RuntimeError("Nexus Mods: the mod has several files; this script updates a mod with one.")
        if files:
            versions = v3("GET", f"/mod-files/{files[0]['id']}/versions", "listing the versions")["versions"]
            if any(v["version"] == version and v["category"] not in ("removed", "archived") for v in versions):
                raise Skip(f"version {version} is already on Nexus Mods")
        # Upload (one part: the zip is far below Nexus' 100 MB single-part limit), then wait.
        data = zip_path.read_bytes()
        digest = hashlib.md5(data).digest()
        up = v3("POST", "/uploads", "starting the upload", json={"filename": zip_path.name, "size_bytes": len(data), "md5": digest.hex()})
        check(httpx.put(up["presigned_url"], content=data, timeout=600, headers={
            "Content-Type": "application/octet-stream",
            "Content-Disposition": f'attachment; filename="{zip_path.name}"',
            "Content-MD5": base64.b64encode(digest).decode("ascii")}), "Nexus Mods: uploading")
        v3("POST", f"/uploads/{up['id']}/finalise", "finishing the upload")
        for _ in range(120):
            if v3("GET", f"/uploads/{up['id']}", "checking the upload")["state"] == "available":
                break
            time.sleep(5)
        else:
            raise RuntimeError("Nexus Mods was still processing the upload after 10 minutes.")
        body = {"upload_id": up["id"], "name": "Moder Warehouse Plus", "version": version, "file_category": "main",
                "update_mod_version": True,
                "description": "The server plugin: unzip into the dedicated server's folder (BepInEx/plugins/ModerWarehousePlus/). Players need nothing."}
        if files:
            v3("POST", f"/mod-files/{files[0]['id']}/versions", "adding the version", json={**body, "archive_existing_file": True})
        else:
            v3("POST", "/mod-files", "adding the file", json={**body, "mod_id": uid, "allow_mod_manager_download": True})
        v3("POST", f"/mods/{uid}/changelogs", "adding the changelog", json={"version": version, "changelog": notes})
    return f"https://www.nexusmods.com/{NEXUS_GAME}/mods/{mod_id}?tab=files"


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2
    zip_path, version = Path(sys.argv[1]), sys.argv[2]
    manifest = json.loads(zipfile.ZipFile(zip_path).read("manifest.json"))
    if manifest["version_number"] != version:
        raise SystemExit(f"The zip is version {manifest['version_number']}, not {version}.")
    notes = changelog_section(version)
    failed = False
    for site, publish in (("GitHub", github), ("Thunderstore", thunderstore), ("Hexium", hexium), ("Nexus Mods", nexus)):
        try:
            print(f"{site}: published, {publish(zip_path, version, notes)}")
        except Skip as e:
            print(f"{site}: skipped ({e})")
        except Exception as e:  # noqa: BLE001 - every site is tried; the run fails at the end
            failed = True
            print(f"{site}: FAILED: {e}", file=sys.stderr)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
