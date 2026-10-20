"use strict";

const { Plugin, ItemView, Notice, setIcon } = require("obsidian");
const { PreviewReader } = require("./reader");
const VIEW = "menslink-workbench";

const MESSAGES = {
  invalid_argument: "请输入 1–500 个字符的字面查询。",
  preview_not_configured: "预览尚未配置，请从项目预览入口创建合成测试库。",
  unexpected_vault: "此预览只允许在它创建的合成 Vault 中运行。",
  candidate_mismatch: "预览文件与冻结源码不一致，请重新生成预览。",
  incompatible_library: "资料库身份或协议不一致，请重新打开项目预览。",
  library_not_initialized: "测试资料库不存在，请从预览入口重新创建。",
  service_unavailable: "本地查询程序未响应。检查预览文件后点击“重新连接”。",
  version_not_found_or_not_authorized: "这个版本已不可读取，请重新检索。",
  policy_changed: "资料权限已变化，请重新检索。",
  index_unavailable: "资料库暂时不可读取，请稍后重新连接。",
  budget_too_small: "命中超过读取预算，请缩短查询或选择更具体的词。",
};
const message = e => MESSAGES[e.code] || "读取未完成，请重新连接后再试。";

function el(parent, tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  parent.append(node);
  return node;
}

function button(parent, label, icon, handler, className = "") {
  const b = el(parent, "button", className);
  b.type = "button";
  if (icon) { const i = el(b, "span", "ml-icon"); i.setAttribute("aria-hidden", "true"); setIcon(i, icon); }
  el(b, "span", "", label);
  b.addEventListener("click", handler);
  return b;
}

class Workbench extends ItemView {
  getViewType() { return VIEW; }
  getDisplayText() { return "MensLink 资料工作台"; }
  getIcon() { return "library"; }

  constructor(leaf, plugin) {
    super(leaf);
    this.plugin = plugin;
    this.hits = [];
    this.sourceId = "";
    this.searchGeneration = 0;
    this.detailGeneration = 0;
    this.activeHit = null;
    this.epoch = null;
    this.closed = false;
  }

  async onOpen() {
    this.contentEl.empty();
    this.root = el(this.contentEl, "div", "ml-workbench");
    const header = el(this.root, "header", "ml-header");
    const title = el(header, "div", "ml-title-group");
    el(title, "h1", "", "资料工作台");
    el(title, "p", "ml-muted", "在来源之间检索，在固定版本中核对。");
    el(header, "span", "ml-preview-label", "开发预览 · 合成资料");
    const toolbar = el(this.root, "div", "ml-toolbar");
    const form = el(toolbar, "form", "ml-search-form");
    const label = el(form, "label", "ml-search-label", "检索资料");
    this.input = el(label, "input", "ml-query");
    this.input.type = "search";
    this.input.placeholder = "输入中文短词、原句或代码";
    this.input.value = "引用";
    this.input.autocomplete = "off";
    this.submit = button(form, "检索", "search", () => {} , "mod-cta");
    this.submit.type = "submit";
    form.addEventListener("submit", event => { event.preventDefault(); void this.search(); });
    this.reconnect = button(toolbar, "重新连接", "refresh-cw", () => void this.connect());
    const main = el(this.root, "div", "ml-columns");
    this.sources = el(main, "nav", "ml-sources");
    this.sources.setAttribute("aria-label", "合成资料来源");
    this.results = el(main, "section", "ml-results");
    this.results.setAttribute("aria-label", "检索结果");
    this.details = el(main, "section", "ml-detail");
    this.details.setAttribute("aria-label", "固定版本详情");
    this.details.setAttribute("aria-busy", "false");
    const footer = el(this.root, "footer", "ml-footer");
    this.connection = el(footer, "span", "ml-connection", "正在连接本地资料库…");
    el(footer, "span", "ml-muted", "只读查询 · 不扫描 Vault · 不向 Agent 外发");
    this.live = el(this.root, "p", "ml-feedback");
    this.live.setAttribute("role", "status");
    this.live.setAttribute("aria-live", "polite");
    this.resize = new ResizeObserver(([entry]) => {
      this.root.classList.toggle("ml-narrow", entry.contentRect.width < 900);
      this.root.classList.toggle("ml-compact", entry.contentRect.width < 600);
    });
    this.resize.observe(this.root);
    this.drawSources();
    this.emptyDetail();
    await this.connect();
  }

  async onClose() {
    this.closed = true;
    this.searchGeneration++;
    this.detailGeneration++;
    this.resize?.disconnect();
    this.reader?.close();
  }

  say(text, error = false) {
    if (this.closed) return;
    this.live.textContent = text;
    this.live.classList.toggle("ml-error", error);
  }

  observe(value) {
    if (this.epoch !== null && this.epoch !== value.policy_epoch) {
      this.hits = [];
      this.emptyDetail();
    }
    this.epoch = value.policy_epoch;
  }

  async connect() {
    if (this.connecting) return;
    this.connecting = true;
    this.reconnect.disabled = true;
    this.submit.disabled = true;
    this.searchGeneration++;
    this.detailGeneration++;
    this.reader?.close();
    this.hits = [];
    this.emptyDetail();
    this.drawResults("正在连接本地资料库…");
    try {
      this.reader = new PreviewReader(this.plugin.config, this.app.vault.adapter.getBasePath());
      const status = await this.reader.read("status");
      if (this.closed) return;
      this.observe(status);
      this.connection.textContent = `本地资料库已连接 · ${status.source_count} 个可读来源`;
      this.root.classList.remove("ml-offline");
      this.submit.disabled = false;
      await this.search();
    } catch (error) {
      if (this.closed) return;
      this.root.classList.add("ml-offline");
      this.connection.textContent = "本地资料库未连接";
      this.drawResults("暂时无法检索。检查预览文件，再点击“重新连接”。");
      this.say(message(error), true);
    } finally {
      this.connecting = false;
      if (!this.closed) this.reconnect.disabled = false;
    }
  }

  drawSources() {
    this.sources.replaceChildren();
    el(this.sources, "h2", "ml-pane-title", "资料来源");
    const all = button(this.sources, "全部资料", "library", () => this.filter(""), "ml-source");
    all.setAttribute("aria-pressed", String(!this.sourceId));
    for (const source of this.plugin.config?.sources || []) {
      const b = button(this.sources, source.title, "file-text", () => this.filter(source.source_id), "ml-source");
      b.setAttribute("aria-pressed", String(source.source_id === this.sourceId));
      el(b, "small", "ml-source-note", source.agent_allowed ? "导入时允许 Agent" : "导入时仅本机");
    }
    el(this.sources, "p", "ml-source-help ml-muted", "清单来自本次合成导入回执；点击可筛选已返回命中。完整导入与授权管理仍待接入。");
  }

  filter(sourceId) {
    this.sourceId = sourceId;
    this.drawSources();
    this.emptyDetail();
    this.drawResults();
    const first = this.visibleHits()[0];
    if (first) void this.openHit(first);
  }

  visibleHits() { return this.hits.filter(h => !this.sourceId || h.source_id === this.sourceId); }

  async search() {
    const query = this.input.value.trim();
    if (!query || [...query].length > 500) { this.say(MESSAGES.invalid_argument, true); this.input.focus(); return; }
    if (!this.reader || this.closed) return;
    const generation = ++this.searchGeneration;
    this.detailGeneration++;
    this.hits = [];
    this.emptyDetail();
    this.submit.disabled = true;
    this.results.setAttribute("aria-busy", "true");
    this.drawResults("正在检索固定版本…");
    this.say("正在检索…");
    try {
      const value = await this.reader.read("search", { query });
      if (generation !== this.searchGeneration || this.closed) return;
      this.observe(value);
      this.hits = value.hits;
      this.query = query;
      this.truncated = value.truncated;
      this.drawResults();
      this.say(value.truncated ? "当前只返回部分命中，请收窄查询；来源筛选也只作用于这一批结果。" : `找到 ${this.hits.length} 个片段。选择命中核对固定版本。`);
      const first = this.visibleHits()[0];
      if (first) await this.openHit(first);
    } catch (error) {
      if (generation !== this.searchGeneration || this.closed) return;
      this.drawResults("本次检索未完成，可重新检索或重新连接。");
      this.say(message(error), true);
    } finally {
      if (generation === this.searchGeneration && !this.closed) {
        this.submit.disabled = false;
        this.results.setAttribute("aria-busy", "false");
      }
    }
  }

  drawResults(state) {
    this.results.replaceChildren();
    const head = el(this.results, "div", "ml-results-head");
    el(head, "h2", "ml-pane-title", "检索命中");
    const hits = this.visibleHits();
    if (!state) el(head, "span", "ml-muted", `${hits.length} 个片段${this.truncated ? " · 部分结果" : ""}`);
    if (state || !hits.length) {
      el(this.results, "p", "ml-empty", state || (this.sourceId ? "这个来源没有已返回命中。切回全部资料，或换一个更具体的词。" : "没有找到这个词。试试“引用”“版本”或“本地”。"));
      return;
    }
    for (const hit of hits) {
      const b = el(this.results, "button", "ml-hit");
      b.type = "button";
      b.dataset.chunk = hit.chunk_id;
      b.setAttribute("aria-pressed", String(this.activeHit?.chunk_id === hit.chunk_id));
      el(b, "strong", "ml-hit-title", hit.title.replace(/\.(md|txt)$/i, ""));
      el(b, "span", "ml-hit-location ml-muted", `第 ${hit.locator.line_start}–${hit.locator.line_end} 行 · 固定版本`);
      const excerpt = el(b, "span", "ml-excerpt");
      this.highlight(excerpt, hit.excerpt.replace(/^# /, ""));
      b.addEventListener("click", () => void this.openHit(hit));
    }
  }

  highlight(parent, text) {
    const term = this.query || "";
    const at = term ? text.toLocaleLowerCase().indexOf(term.toLocaleLowerCase()) : -1;
    if (at < 0) { parent.textContent = text.slice(0, 200); return; }
    const start = Math.max(0, at - 45);
    parent.append(document.createTextNode((start ? "…" : "") + text.slice(start, at)));
    el(parent, "mark", "", text.slice(at, at + term.length));
    parent.append(document.createTextNode(text.slice(at + term.length, at + term.length + 130) + (text.length > at + term.length + 130 ? "…" : "")));
  }

  emptyDetail() {
    this.detailGeneration++;
    this.activeHit = null;
    if (!this.details) return;
    this.details.replaceChildren();
    this.details.setAttribute("aria-busy", "false");
    el(this.details, "h2", "ml-pane-title", "版本详情");
    el(this.details, "p", "ml-empty", "选择一个命中，查看固定版本、正文定位和可复制引用。");
  }

  async openHit(hit) {
    const generation = ++this.detailGeneration;
    this.activeHit = null;
    this.details.replaceChildren();
    this.details.setAttribute("aria-busy", "true");
    el(this.details, "p", "ml-empty", "正在读取这个固定版本…");
    try {
      const value = await this.reader.read("fetch", hit);
      if (generation !== this.detailGeneration || this.closed) return;
      this.observe(value);
      this.activeHit = value.hit;
      this.drawDetail(value.hit);
      this.drawResults();
    } catch (error) {
      if (generation !== this.detailGeneration || this.closed) return;
      this.emptyDetail();
      this.say(message(error), true);
    } finally {
      if (generation === this.detailGeneration && !this.closed) this.details.setAttribute("aria-busy", "false");
    }
  }

  drawDetail(hit) {
    this.details.replaceChildren();
    const head = el(this.details, "div", "ml-detail-head");
    el(head, "h2", "ml-pane-title", "版本详情");
    this.copyButton = button(head, "复制引用", "copy", () => void this.copyCitation());
    el(this.details, "h3", "ml-document-title", hit.title.replace(/\.(md|txt)$/i, ""));
    const meta = el(this.details, "dl", "ml-metadata");
    const field = (name, value, mono = false) => {
      el(meta, "dt", "", name);
      el(meta, "dd", mono ? "ml-mono" : "", value);
    };
    field("修订", hit.revision_id.slice(0, 8), true);
    field("采集时间", new Date(hit.captured_at).toLocaleString("zh-CN", { hour12: false }));
    field("定位", `第 ${hit.locator.line_start}–${hit.locator.line_end} 行 · 字符 [${hit.locator.start_char}, ${hit.locator.end_char})`);
    el(this.details, "p", "ml-reading-note ml-muted", "以下为命中片段的规范化正文；定位按 Unicode 字符计数，引用始终绑定这个修订。");
    const article = el(this.details, "pre", "ml-document", hit.excerpt);
    article.tabIndex = 0;
    el(this.details, "h3", "ml-subheading", "引用与来源");
    el(this.details, "code", "ml-citation", hit.citation);
    el(this.details, "p", "ml-uri ml-muted", hit.source_uri);
    const identity = el(this.details, "details", "ml-identity");
    el(identity, "summary", "", "查看完整版本身份");
    el(identity, "pre", "ml-identity-data", JSON.stringify({ source_id: hit.source_id, revision_id: hit.revision_id,
      content_version_id: hit.content_version_id, artifact_id: hit.artifact_id, chunk_id: hit.chunk_id,
      artifact_sha256: hit.artifact_sha256, candidate_source_sha: this.plugin.config.sourceSha }, null, 2));
  }

  async copyCitation() {
    const hit = this.activeHit;
    const generation = this.detailGeneration;
    const copyButton = this.copyButton;
    if (!hit || !copyButton) return;
    copyButton.disabled = true;
    try {
      // 复制前重新读取，避免已撤销资料继续通过旧界面外发。
      const value = await this.reader.read("fetch", hit);
      if (this.closed || generation !== this.detailGeneration) return;
      this.observe(value);
      await navigator.clipboard.writeText(value.hit.citation);
      this.say("引用已复制，包含来源、修订、片段和字符范围。");
    } catch (error) {
      if (this.closed || generation !== this.detailGeneration) return;
      if (error.code) { this.emptyDetail(); this.drawResults(); }
      this.say(error.code ? message(error) : "剪贴板不可用，请选择下方引用手动复制。", true);
    } finally { if (!this.closed) copyButton.disabled = false; }
  }
}

module.exports = class MensLinkPreview extends Plugin {
  async onload() {
    this.config = await this.loadData();
    this.registerView(VIEW, leaf => new Workbench(leaf, this));
    this.addRibbonIcon("library", "MensLink 资料工作台", () => void this.openWorkbench());
    this.addCommand({ id: "open-workbench", name: "打开资料工作台", callback: () => void this.openWorkbench() });
  }

  async openWorkbench() {
    let leaf = this.app.workspace.getLeavesOfType(VIEW)[0];
    if (!leaf) { leaf = this.app.workspace.getLeaf("tab"); await leaf.setViewState({ type: VIEW, active: true }); }
    this.app.workspace.revealLeaf(leaf);
    if (!this.config) new Notice(MESSAGES.preview_not_configured);
  }
};
