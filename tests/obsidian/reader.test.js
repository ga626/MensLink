"use strict";
const test = require("node:test");
const assert = require("node:assert/strict");
const { argumentsFor, PreviewReader } = require("../../integrations/obsidian/reader");

test("读取入口拒绝管理操作与额外命令", () => {
  for (const command of ["import", "init", "withdraw", "serve", "cmd.exe", "search && whoami"])
    assert.throws(() => argumentsFor(command), { code: "read_only" });
});
test("查询逐字传参，Unicode 上限按 code point", () => {
  const query = '资料 & $(whoami) "引用"';
  assert.deepEqual(argumentsFor("search", { query }), ["--query", query, "--max-chars", "20000"]);
  assert.doesNotThrow(() => argumentsFor("search", { query: "😀".repeat(500) }));
  for (const query of [" ", "😀".repeat(501), null])
    assert.throws(() => argumentsFor("search", { query }), { code: "invalid_argument" });
});
test("固定版本需要三个完整 UUID，不能把路径放进身份", () => {
  const id = "aabbccdd-1234-4567-89ab-aabbccddeeff";
  assert.deepEqual(argumentsFor("fetch", { source_id: id, revision_id: id, chunk_id: id }),
    ["--source-id", id, "--revision-id", id, "--chunk-id", id, "--max-chars", "24000"]);
  assert.throws(() => argumentsFor("fetch", { source_id: "../x", revision_id: id, chunk_id: id }), { code: "invalid_argument" });
  assert.throws(() => argumentsFor("fetch", { source_id: id, chunk_id: id }), { code: "invalid_argument" });
});
test("没有合成预览身份时拒绝配置", () => {
  assert.throws(() => new PreviewReader({}, "unused"), { code: "preview_not_configured" });
});
