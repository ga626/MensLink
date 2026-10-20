"""从干净 Git 提交导出、构建并冻结开发预览；不安装、不发布。"""
from pathlib import Path
import argparse
import datetime
import hashlib
import io
import json
import os
import shutil
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[1]
PREVIEWS = ROOT / "local/obsidian-preview"


def digest(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--label", required=True, help="本次唯一候选名称")
    options = parser.parse_args()
    if not options.label or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789-" for c in options.label):
        raise SystemExit("label must use lowercase letters, numbers and hyphens")
    subprocess.run(["git", "diff", "--exit-code", "HEAD", "--"], cwd=ROOT, check=True)
    source_sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    work = PREVIEWS / "builds" / options.label
    work.mkdir(parents=True, exist_ok=False)
    source = work / "source"
    archive = subprocess.check_output(["git", "archive", "--format=tar", source_sha], cwd=ROOT)
    with tarfile.open(fileobj=io.BytesIO(archive)) as bundle:
        bundle.extractall(source, filter="data")
    candidate = PREVIEWS / "candidates" / options.label
    candidate.mkdir(parents=True, exist_ok=False)
    cli = candidate / "cli"
    sdk = ROOT / "local/toolchains/dotnet/dotnet.exe"
    env = os.environ.copy()
    env.update(DOTNET_ROOT=str(sdk.parent), DOTNET_CLI_HOME=str(ROOT / "local/dotnet-home"),
               NUGET_PACKAGES=str(ROOT / "local/nuget"), DOTNET_CLI_TELEMETRY_OPTOUT="1",
               DOTNET_NOLOGO="1", DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE="true")
    receipt = {"schema": "menslink-preview-build/v1", "source_sha": source_sha,
               "kind": "synthetic_development_preview", "result": "in_progress", "commands": [],
               "at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
               "limitations": ["framework-dependent development candidate; requires project .NET runtime",
                               "no Release, installer, distribution audit or user acceptance"]}

    def save():
        (work / "receipt.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def run(label, argv):
        with (work / (label + ".stdout.log")).open("w", encoding="utf-8") as out, \
             (work / (label + ".stderr.log")).open("w", encoding="utf-8") as err:
            value = subprocess.run(list(map(str, argv)), cwd=source, env=env, stdout=out, stderr=err, timeout=180)
        receipt["commands"].append({"argv": list(map(str, argv)), "exit_code": value.returncode,
                                     "stdout": label + ".stdout.log", "stderr": label + ".stderr.log"})
        save()
        if value.returncode:
            receipt["result"] = "failed"
            save()
            raise SystemExit(f"{label} failed; see {work}")

    save()
    project = source / "apps/MensLink.Cli/MensLink.Cli.csproj"
    # 离线复用已经锁定的依赖；关闭本次无网络漏洞审计不构成漏洞审计结论。
    run("restore", [sdk, "restore", project, "--locked-mode", "--source", ROOT / "local/nuget", "-p:NuGetAudit=false"])
    run("publish", [sdk, "publish", project, "-c", "Release", "--no-restore", "--self-contained", "false", "-o", cli])
    shutil.copytree(source / "integrations/obsidian", candidate / "plugin")
    shutil.copytree(source / "tests/fixtures/workbench", candidate / "fixtures")
    shutil.copyfile(source / "LICENSE", candidate / "LICENSE")
    files = {p.relative_to(candidate).as_posix(): digest(p) for p in sorted(candidate.rglob("*")) if p.is_file()}
    manifest = {"schema": "menslink-preview-candidate/v1", "kind": receipt["kind"],
                "source_sha": source_sha, "files": files, "runtime": ".NET 10 project runtime; not self-contained"}
    (candidate / "candidate.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    receipt.update(result="passed", candidate=str(candidate), candidate_manifest_sha256=digest(candidate / "candidate.json"), files=files)
    save()
    print(json.dumps({"candidate": str(candidate), "source_sha": source_sha,
                      "manifest_sha256": receipt["candidate_manifest_sha256"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
