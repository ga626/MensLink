"""只在项目 local 下创建合成 Obsidian Vault；不修改日用配置或自动启用插件。"""
from pathlib import Path
import argparse
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
PREVIEWS = ROOT / "local/obsidian-preview"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate", required=True, type=Path)
    parser.add_argument("--obsidian", type=Path, help="省略时仅准备，不启动")
    parser.add_argument("--debug-port", type=int, help="仅测试实例使用的本机 CDP 端口")
    parser.add_argument("--hidden", action="store_true", help="后台自动验证 helper；交互预览省略")
    options = parser.parse_args()
    candidate = options.candidate.resolve()
    if not candidate.is_relative_to((PREVIEWS / "candidates").resolve()):
        raise SystemExit("candidate must be inside this project's preview candidates")
    manifest_file = candidate / "candidate.json"
    manifest = json.loads(manifest_file.read_text(encoding="utf-8"))
    if manifest["kind"] != "synthetic_development_preview":
        raise SystemExit("unexpected candidate kind")
    for name, sha in manifest["files"].items():
        file = (candidate / name).resolve()
        if not file.is_relative_to(candidate) or hashlib.sha256(file.read_bytes()).hexdigest() != sha:
            raise SystemExit("candidate digest mismatch: " + name)
    work = PREVIEWS / "sessions" / (datetime.datetime.now().strftime("%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:6])
    work.mkdir(parents=True)
    vault = work / "vault"
    config = vault / ".obsidian"
    plugin = config / "plugins/menslink-preview"
    shutil.copytree(candidate / "plugin", plugin)
    profile = work / "profile"
    profile.mkdir()
    data = work / "data"
    exe = candidate / "cli/menslink.exe"
    runtime = ROOT / "local/toolchains/dotnet"
    env = os.environ.copy()
    env.update(DOTNET_ROOT=str(runtime), DOTNET_ROOT_X64=str(runtime))
    receipt = {"schema": "menslink-preview-session/v1", "kind": manifest["kind"],
               "source_sha": manifest["source_sha"], "candidate_root": str(candidate),
               "candidate_manifest_sha256": hashlib.sha256(manifest_file.read_bytes()).hexdigest(),
               "session_root": str(work), "vault_root": str(vault), "profile_root": str(profile),
               "data_root": str(data), "result": "preparing", "commands": []}

    def save():
        (work / "session.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def cli(command, *args):
        argv = list(map(str, [exe, command, "--data-root", data, *args]))
        result = subprocess.run(argv, capture_output=True, text=True, encoding="utf-8", env=env, timeout=12,
                                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        receipt["commands"].append({"argv": argv, "exit_code": result.returncode,
                                    "stdout": result.stdout, "stderr": result.stderr})
        save()
        if result.returncode:
            raise SystemExit("preview preparation failed; see session.json")
        return json.loads(result.stdout)

    save()
    library = cli("init")["library_id"]
    sources = []
    for index, file in enumerate(sorted((candidate / "fixtures").iterdir())):
        preview = cli("preview", "--root", candidate / "fixtures", "--file", file)
        preview_file = work / f"import-preview-{index}.json"
        preview_file.write_text(json.dumps(preview, ensure_ascii=False), encoding="utf-8")
        # 显式仅对本次合成样本作确认；不接受任意用户文件作为参数。
        imported = cli("import", "--manifest", preview_file)
        sources.append({"source_id": imported["source_id"], "title": file.stem, "agent_allowed": imported["agent_allowed"]})
    settings = {"synthetic": True, "sourceSha": manifest["source_sha"], "vaultRoot": str(vault),
                "candidateRoot": str(candidate), "dataRoot": str(data), "runtimeRoot": str(runtime),
                "libraryId": library, "sources": sources}
    (plugin / "data.json").write_text(json.dumps(settings, ensure_ascii=False, indent=2), encoding="utf-8")
    (config / "community-plugins.json").write_text('["menslink-preview"]', encoding="utf-8")
    (config / "core-plugins.json").write_text("[]", encoding="utf-8")
    (config / "app.json").write_text(json.dumps({"checkForUpdates": False}), encoding="utf-8")
    (config / "appearance.json").write_text(json.dumps({"theme": "obsidian"}), encoding="utf-8")
    (vault / "开始.md").write_text("# MensLink 开发预览\n\n本 Vault 仅含合成资料。\n\n请仅在此测试 Vault 启用 MensLink 插件，再从命令面板打开“MensLink：打开资料工作台”。\n\n可以检索“引用”“版本”“本地”，选择命中核对正文并复制固定引用。\n\n此预览只读；导入管理、持久任务与正式安装尚未接入。\n", encoding="utf-8")
    (profile / "obsidian.json").write_text(json.dumps({"vaults": {uuid.uuid4().hex[:16]: {
        "path": str(vault), "ts": int(time.time() * 1000), "open": True}}, "updateDisabled": True}), encoding="utf-8")
    receipt.update(result="prepared", library_id=library, sources=sources)
    save()
    # 所有路径都包在单独参数内；只启动指定的 Obsidian，不调用 shell 展开。
    if options.obsidian:
        obsidian = options.obsidian.resolve()
        if not obsidian.is_file() or obsidian.name.lower() != "obsidian.exe":
            raise SystemExit("expected an installed Obsidian.exe")
        argv = [str(obsidian), '--user-data-dir="' + str(profile) + '"', "--disable-background-networking"]
        if options.debug_port:
            if not 1024 <= options.debug_port <= 65535:
                raise SystemExit("invalid local debug port")
            argv += [f"--remote-debugging-port={options.debug_port}", "--remote-debugging-address=127.0.0.1"]
        # Start-Process 的参数使用 Windows 原生命令行引号；单引号只在 PowerShell 表达式中转义。
        quote = lambda value: "'" + value.replace("'", "''") + "'"
        launcher = work / "launch.ps1"
        text = "$ErrorActionPreference='Stop'\n$previewHost = Start-Process -FilePath " + quote(str(obsidian))
        text += " -ArgumentList @(" + ",".join(quote(a) for a in argv[1:]) + ") -PassThru"
        if options.hidden:
            text += " -WindowStyle Hidden"
        text += " -RedirectStandardOutput " + quote(str(work / "host.stdout.log"))
        text += " -RedirectStandardError " + quote(str(work / "host.stderr.log"))
        text += "\n$previewHost.Id | Set-Content -LiteralPath " + quote(str(work / "host-pid.txt")) + " -Encoding UTF8\n"
        launcher.write_text(text, encoding="utf-8-sig")
        # GUI 子进程可能继承 PowerShell 的管道句柄；写文件避免已启动却等不到 EOF。
        with (work / "launch.stdout.log").open("w", encoding="utf-8") as out, \
             (work / "launch.stderr.log").open("w", encoding="utf-8") as err:
            result = subprocess.run(["pwsh", "-NoProfile", "-File", str(launcher)], stdout=out, stderr=err, timeout=20)
        if result.returncode:
            receipt["result"] = "launch_failed"
            save()
            raise SystemExit("test host launch failed; see launch.stderr.log")
        receipt.update(host_pid=int((work / "host-pid.txt").read_text(encoding="utf-8-sig").strip()),
                       host_argv=argv, result="launched_not_yet_verified")
        save()
    print(json.dumps({"session": str(work), "vault": str(vault), "source_sha": manifest["source_sha"],
                      "state": receipt["result"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
