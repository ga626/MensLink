"""检查本项目文档、阶段、依赖方向与修改前基线；只用 Python 标准库。"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
from urllib.parse import unquote
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SKIP = {"local", ".git", ".codex", "bin", "obj", "node_modules", "__pycache__"}


def inventory() -> list[Path]:
    files = []
    for directory, folders, names in os.walk(ROOT):
        folders[:] = [name for name in folders if name not in SKIP]
        files.extend(Path(directory) / name for name in names)
    return sorted(files)


def validate() -> dict:
    errors, warnings = [], []
    files = inventory()
    for file in files:
        if file.suffix == ".json":
            try:
                json.loads(file.read_text(encoding="utf-8-sig"))
            except (ValueError, UnicodeError) as error:
                errors.append(f"Invalid JSON: {file.relative_to(ROOT)}: {error}")
    manifest = json.loads((ROOT / "config/project-structure.manifest.json").read_text(encoding="utf-8"))
    for row in manifest["entries"]:
        path = ROOT / row["path"]
        if not path.exists() or (row["kind"] == "directory") != path.is_dir():
            errors.append(f"Missing structure entry: {row['path']}")
    legacy = set(manifest["inherited_documents"])
    for file in files:
        if file.suffix != ".md" or file.relative_to(ROOT).as_posix() in legacy:
            continue
        text = file.read_text(encoding="utf-8-sig")
        text = re.sub(r"```.*?```", "", text, flags=re.S)
        for target in re.findall(r"\]\((?:<([^>]+)>|([^\s)]+))\)", text):
            url = target[0] or target[1]
            if url.startswith(("https:", "http:", "mailto:", "#", "codex:")):
                continue
            if re.match(r"[A-Za-z]:", url):
                errors.append(f"New doc uses nonportable link: {file.relative_to(ROOT)}: {url}")
                continue
            path = unquote(url.split("#", 1)[0])
            if path and not (file.parent / path).exists():
                errors.append(f"Broken link: {file.relative_to(ROOT)}: {url}")
    plan_path = ROOT / "config/phase-1.json"
    plan = json.loads(plan_path.read_text(encoding="utf-8")) if plan_path.exists() else {"tasks": [], "status": "absent"}
    if not plan_path.exists():
        warnings.append("Local historical stage ledger absent; no historical acceptance claim made")
    tasks = {task["id"]: task for task in plan["tasks"]}
    if len(tasks) != len(plan["tasks"]):
        errors.append("Duplicate task IDs")
    visited, active = set(), set()

    def walk(task_id):
        if task_id in active:
            errors.append(f"Task dependency cycle: {task_id}")
            return
        if task_id not in tasks:
            errors.append(f"Unknown dependency: {task_id}")
            return
        if task_id in visited:
            return
        active.add(task_id)
        for parent in tasks[task_id]["depends_on"]:
            walk(parent)
        active.remove(task_id)
        visited.add(task_id)

    for task_id, task in tasks.items():
        walk(task_id)
        if task["status"] == "passed" and not task["evidence"]:
            errors.append(f"Passed task has no evidence: {task_id}")
        for evidence in task["evidence"]:
            if not (ROOT / evidence).is_file():
                errors.append(f"Missing task evidence: {evidence}")
    if plan["status"] == "passed" and any(t["status"] != "passed" for t in tasks.values()):
        errors.append("Stage passed with incomplete tasks")
    sdk = json.loads((ROOT / "global.json").read_text())["sdk"]["version"]
    if sdk != json.loads((ROOT / "config/toolchain.json").read_text())["sdk_version"]:
        errors.append("SDK pin mismatch")
    for project in [p for p in files if p.suffix == ".csproj"]:
        xml = ET.parse(project).getroot()
        for ref in xml.iter("ProjectReference"):
            target = (project.parent / ref.attrib["Include"]).resolve()
            if not target.is_file():
                errors.append(f"Missing project reference: {project}")
            if "MensLink.Core" in str(project) and target != project:
                errors.append("Core cannot depend on entry points or adapters")
        for package in xml.iter("PackageReference"):
            version = package.attrib.get("Version", "")
            if not version or any(char in version for char in "*[]()"):
                errors.append(f"Unpinned package: {project}")
        if list(xml.iter("PackageReference")) and not project.with_name("packages.lock.json").exists():
            errors.append(f"Missing package lock: {project.relative_to(ROOT)}")
    baseline_path = ROOT / "local/foundation/2026-10-20/baseline.json"
    baseline_count = None
    if baseline_path.exists():
        baseline = json.loads(baseline_path.read_text(encoding="utf-8-sig"))
        baseline_count = len(baseline["files"])
        for row in baseline["files"]:
            source = ROOT / row["path"]
            if not source.is_file() or hashlib.sha256(source.read_bytes()).hexdigest() != row["sha256"]:
                errors.append(f"Original file changed or missing: {row['path']}")
    else:
        warnings.append("Original local baseline absent on this checkout; no preservation claim made")
    return {"schema": "menslink-project-check/v1", "result": "passed" if not errors else "failed",
            "files_checked": len(files), "baseline_files_checked": baseline_count,
            "errors": errors, "warnings": warnings,
            "limits": ["只检查本机结构与声明，不验收产品能力", "继承报告原链接保留，不将其历史路径当新文档合同"]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--receipt", type=Path)
    args = parser.parse_args()
    result = validate()
    output = json.dumps(result, ensure_ascii=False, indent=2)
    if args.receipt:
        args.receipt.parent.mkdir(parents=True, exist_ok=True)
        args.receipt.write_text(output + "\n", encoding="utf-8")
    print(output)
    sys.exit(0 if result["result"] == "passed" else 1)
