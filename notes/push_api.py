#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""用 GitHub REST API 推送仓库（绕过本机对 github.com:443 的封锁，api.github.com 可直连）。

用法:
  python push_api.py --token ghp_xxx --repo Smallgrass233/Wzj13610087142
  可选 --branch master --message "..." --private

做的事:
  1) 仓库不存在就创建（private 由 --private 控制，默认 private）
  2) 把当前 git HEAD 里的所有文件逐个上传成 blob
  3) 组装 tree → commit → 更新/创建分支 ref
"""
import argparse, base64, json, subprocess, sys, urllib.request, urllib.error, mimetypes

API = "https://api.github.com"


def api(method, path, token, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Authorization", "Bearer " + token)
    req.add_header("Accept", "application/vnd.github+json")
    if data:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            raw = r.read()
            return json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:
        body_text = e.read().decode("utf-8", "replace")
        if e.code == 404 and method == "GET":
            return None
        raise SystemExit(f"GitHub API {method} {path} -> {e.code}\n{body_text}")


def git(*args):
    return subprocess.run(["git", *args], cwd=args_cwd, capture_output=True, check=True).stdout


def main():
    global args_cwd
    ap = argparse.ArgumentParser()
    ap.add_argument("--token", required=True)
    ap.add_argument("--repo", required=True, help="owner/name")
    ap.add_argument("--branch", default="master")
    ap.add_argument("--message", default="push from local")
    ap.add_argument("--private", action="store_true", default=True)
    ap.add_argument("--cwd", default=r"D:\re\zhongsan")
    a = ap.parse_args()
    args_cwd = a.cwd

    owner, name = a.repo.split("/", 1)
    repo = api("GET", f"/repos/{owner}/{name}", a.token)
    if repo is None:
        print(f"仓库不存在，创建 {a.repo} …")
        repo = api("POST", "/user/repos", a.token, {
            "name": name, "private": True, "description": "中华三国志 数据整理与工具集",
            "auto_init": False,
        })
        print("created:", repo["html_url"])
    else:
        print("仓库已存在:", repo["html_url"])

    files = git("ls-files", "-z").split(b"\0")
    files = [f.decode("utf-8") for f in files if f]
    tree_items = []
    for i, path in enumerate(files, 1):
        blob = git("show", f"HEAD:{path}")
        b64 = base64.b64encode(blob).decode()
        r = api("POST", f"/repos/{owner}/{name}/git/blobs", a.token,
                {"content": b64, "encoding": "base64"})
        tree_items.append({"path": path, "mode": "100644", "type": "blob", "sha": r["sha"]})
        print(f"  [{i}/{len(files)}] {path} ({len(blob)} B)")

    tree = api("POST", f"/repos/{owner}/{name}/git/trees", a.token, {"tree": tree_items})
    ref = api("GET", f"/repos/{owner}/{name}/git/ref/heads/{a.branch}", a.token)
    parents = [ref["object"]["sha"]] if ref else []
    commit = api("POST", f"/repos/{owner}/{name}/git/commits", a.token,
                 {"message": a.message, "tree": tree["sha"], "parents": parents})
    if ref:
        api("PATCH", f"/repos/{owner}/{name}/git/refs/heads/{a.branch}", a.token, {"sha": commit["sha"]})
    else:
        api("POST", f"/repos/{owner}/{name}/git/refs", a.token,
            {"ref": f"refs/heads/{a.branch}", "sha": commit["sha"]})
    print("pushed:", f"{repo['html_url']}/tree/{a.branch}", "commit", commit["sha"][:8])


if __name__ == "__main__":
    args_cwd = "."
    main()
