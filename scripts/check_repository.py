"""只检查受版本控制的公开文件；不扫描本机运行目录。"""
from pathlib import Path
import json
import re
import subprocess
import sys
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]


def validate() -> list[str]:
    result = subprocess.run(["git", "ls-files", "-z"], cwd=ROOT, capture_output=True, check=True)
    tracked = {name.decode("utf-8") for name in result.stdout.split(b"\0") if name}
    errors = []
    required = {"README.md", "LICENSE", "CONTRIBUTING.md", "SECURITY.md", "SUPPORT.md",
                ".github/workflows/quality.yml", ".github/pull_request_template.md"}
    errors.extend("Missing required file: " + name for name in sorted(required - tracked))
    forbidden = {"local", ".codex", "bin", "obj", "node_modules"}
    secret = re.compile(r"(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY-----)")
    for name in sorted(tracked):
        path = ROOT / name
        if (set(path.relative_to(ROOT).parts) & forbidden or path.suffix.lower() in {".sqlite", ".db", ".log"}
                or path.name in {".env", "auth.json"}):
            errors.append("Private/generated file tracked: " + name)
        if not path.is_file():
            errors.append("Tracked file missing: " + name)
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            continue
        if path.suffix.lower() in {".md", ".json", ".yml", ".yaml"}:
            if re.search(r"(?<![A-Za-z0-9])[A-Za-z]:[\\/]", text):
                errors.append("Personal absolute path: " + name)
        if secret.search(text):
            errors.append("Potential secret: " + name)
        if path.suffix == ".json":
            try:
                json.loads(text)
            except ValueError as error:
                errors.append(f"Invalid JSON: {name}: {error}")
        if path.suffix != ".md":
            continue
        text = re.sub(r"```.*?```", "", text, flags=re.S)
        for match in re.findall(r"\]\((?:<([^>]+)>|([^\s)]+))\)", text):
            target = match[0] or match[1]
            if target.startswith(("http:", "https:", "mailto:", "#")):
                continue
            target_path = (path.parent / unquote(target.split("#", 1)[0])).resolve()
            if not target_path.is_relative_to(ROOT) or target_path.relative_to(ROOT).as_posix() not in tracked:
                errors.append(f"Link outside tracked public files: {name}: {target}")
    return errors


if __name__ == "__main__":
    errors = validate()
    print(json.dumps({"result": "failed" if errors else "passed", "errors": errors,
                      "limits": ["静态公开文件检查不替代秘密历史、许可或产品验收"]}, ensure_ascii=False, indent=2))
    sys.exit(bool(errors))
