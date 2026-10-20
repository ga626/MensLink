"use strict";

const { execFile } = require("node:child_process");
const fs = require("node:fs");
const path = require("node:path");
const { createHash } = require("node:crypto");

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const hash = file => createHash("sha256").update(fs.readFileSync(file)).digest("hex");

class PreviewError extends Error {
  constructor(code) { super(code); this.code = code; }
}

// 只允许三个读取操作；不接受 shell、命令名、额外参数或数据库路径输入。
function argumentsFor(operation, input = {}) {
  if (operation === "status") return [];
  if (operation === "search") {
    if (typeof input.query !== "string" || !input.query.trim() || [...input.query].length > 500)
      throw new PreviewError("invalid_argument");
    return ["--query", input.query, "--max-chars", "20000"];
  }
  if (operation === "fetch") {
    if (![input.source_id, input.revision_id, input.chunk_id].every(v => typeof v === "string" && UUID.test(v)))
      throw new PreviewError("invalid_argument");
    return ["--source-id", input.source_id, "--revision-id", input.revision_id,
      "--chunk-id", input.chunk_id, "--max-chars", "24000"];
  }
  throw new PreviewError("read_only");
}

class PreviewReader {
  constructor(config, vaultRoot) {
    if (!config?.synthetic || !UUID.test(config.libraryId || "") || !/^[0-9a-f]{40}$/.test(config.sourceSha || ""))
      throw new PreviewError("preview_not_configured");
    const session = path.dirname(path.resolve(vaultRoot));
    if (path.resolve(config.vaultRoot) !== path.resolve(vaultRoot) ||
        path.resolve(config.dataRoot) !== path.join(session, "data") ||
        path.resolve(vaultRoot) !== path.join(session, "vault"))
      throw new PreviewError("unexpected_vault");
    const manifest = JSON.parse(fs.readFileSync(path.join(config.candidateRoot, "candidate.json"), "utf8"));
    if (manifest.source_sha !== config.sourceSha || manifest.kind !== "synthetic_development_preview")
      throw new PreviewError("candidate_mismatch");
    for (const [name, digest] of Object.entries(manifest.files)) {
      const file = path.resolve(config.candidateRoot, name);
      if (!file.startsWith(path.resolve(config.candidateRoot) + path.sep) || hash(file) !== digest)
        throw new PreviewError("candidate_mismatch");
    }
    for (const name of ["main.js", "reader.js", "styles.css", "manifest.json"])
      if (hash(path.join(vaultRoot, ".obsidian/plugins/menslink-preview", name)) !== manifest.files["plugin/" + name])
        throw new PreviewError("candidate_mismatch");
    this.config = config;
    this.executable = path.join(config.candidateRoot, "cli/menslink.exe");
    if (!manifest.files["cli/menslink.exe"]) throw new PreviewError("candidate_mismatch");
    this.children = new Set();
    this.closed = false;
  }

  async read(operation, input) {
    const args = argumentsFor(operation, input);
    if (this.closed) throw new PreviewError("view_closed");
    const result = await new Promise((resolve, reject) => {
      const child = execFile(this.executable, [operation, "--data-root", this.config.dataRoot, ...args],
        { shell: false, windowsHide: true, timeout: 8000, maxBuffer: 256 * 1024, encoding: "utf8",
          env: { ...process.env, DOTNET_ROOT: this.config.runtimeRoot, DOTNET_ROOT_X64: this.config.runtimeRoot } },
        (error, stdout) => {
          this.children.delete(child);
          if (this.closed) return reject(new PreviewError("view_closed"));
          let value;
          try { value = JSON.parse(stdout); }
          catch { return reject(new PreviewError("service_unavailable")); }
          if (value.error) return reject(new PreviewError(value.error));
          if (error) return reject(new PreviewError("service_unavailable"));
          if (value.schema_version !== "menslink-p1/v1" || value.library_id !== this.config.libraryId)
            return reject(new PreviewError("incompatible_library"));
          resolve(value);
        });
      this.children.add(child);
    });
    return result;
  }

  close() {
    this.closed = true;
    for (const child of this.children) child.kill();
    this.children.clear();
  }
}

module.exports = { PreviewReader, PreviewError, argumentsFor };
