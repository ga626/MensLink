using System.Text;
using System.Text.Json;
using MensLink.Core;
using MensLink.Sqlite;
using Microsoft.Data.Sqlite;

Console.OutputEncoding = new UTF8Encoding(false);
string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string work = Path.Combine(repo, "local/checks", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
Directory.CreateDirectory(work);
var results = new List<object>();
int failed = 0;
void Check(string name, Action action)
{
    try { action(); results.Add(new { name, status = "passed" }); Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; results.Add(new { name, status = "failed", error = e.Message }); Console.WriteLine("FAIL " + name + ": " + e.Message); }
}
void Assert(bool condition, string message = "assertion failed") { if (!condition) throw new Exception(message); }
void Reject(string code, Action action)
{
    try { action(); } catch (ContractException e) when (e.Code == code) { return; }
    throw new Exception("Expected " + code);
}
JsonElement Read(Catalog c, string query) => JsonDocument.Parse(c.Read(new("search", c.LibraryId, Query: query))).RootElement.Clone();
string sourceRoot = Path.Combine(work, "sources"); Directory.CreateDirectory(sourceRoot);
string source = Path.Combine(sourceRoot, "中文 空格😀.md");
File.WriteAllText(source, "# 检索😀\r\n人工编辑 𠮷 E_POLICY_403\r\n本地知识库，版本定位。", new UTF8Encoding(false));
var catalog = new Catalog(Path.Combine(work, "data"));
string library = catalog.Initialize();
Dictionary<string, object?> first = catalog.Import(TextFiles.Preview(sourceRoot, source), true);
JsonElement hit = Read(catalog, "人工").GetProperty("hits")[0].Clone();
Check("中文单字和双字词", () => { Assert(Read(catalog, "库").GetProperty("hits").GetArrayLength() == 1); Assert(Read(catalog, "人工").GetProperty("hits").GetArrayLength() == 1); });
Check("三字词FTS5与精确错误码", () => { Assert(Read(catalog, "知识库").GetProperty("hits").GetArrayLength() == 1); Assert(Read(catalog, "E_POLICY_403").GetProperty("hits").GetArrayLength() == 1); });
Check("Unicode code point与行号", () =>
{
    string body = hit.GetProperty("excerpt").GetString()!;
    Assert(hit.GetProperty("locator").GetProperty("end_char").GetInt32() == body.EnumerateRunes().Count());
    Assert(hit.GetProperty("locator").GetProperty("line_end").GetInt32() == 3);
    Assert(!body.Contains('\r')); Assert(body.Contains("😀"));
});
Check("重复导入复用修订且代际不变", () =>
{
    string before = Read(catalog, "人工").GetProperty("index_generation").GetRawText();
    var second = catalog.Import(TextFiles.Preview(sourceRoot, source), true);
    Assert((bool)second["reused"]! && Equals(second["revision_id"], first["revision_id"]));
    Assert(before == Read(catalog, "人工").GetProperty("index_generation").GetRawText());
});
Check("相同正文不同来源保持独立", () =>
{
    string other = Path.Combine(sourceRoot, "copy.md"); File.Copy(source, other);
    var imported = catalog.Import(TextFiles.Preview(sourceRoot, other), true);
    Assert(!Equals(imported["source_id"], first["source_id"]));
    Assert(Read(catalog, "人工").GetProperty("hits").GetArrayLength() == 2);
});
Check("默认local_only对Agent和计数不可见", () =>
{
    string secret = Path.Combine(sourceRoot, "private.txt"); File.WriteAllText(secret, "PRIVATE_MARKER 个人资料", new UTF8Encoding(false));
    catalog.Import(TextFiles.Preview(sourceRoot, secret), false);
    Assert(Read(catalog, "PRIVATE_MARKER").GetProperty("hits").GetArrayLength() == 0);
    var status = JsonDocument.Parse(catalog.Read(new("status", library))).RootElement;
    Assert(status.GetProperty("source_count").GetInt32() == 2);
    Assert(JsonDocument.Parse(catalog.Read(new("search", library, Query: "PRIVATE_MARKER"), false)).RootElement.GetProperty("hits").GetArrayLength() == 1);
});
Check("预览后变化必须重预览", () =>
{
    var preview = TextFiles.Preview(sourceRoot, source); File.AppendAllText(source, "\n第二个版本新增正文。", new UTF8Encoding(false));
    Reject("changed_since_preview", () => catalog.Import(preview, true));
});
var changed = catalog.Import(TextFiles.Preview(sourceRoot, source), true);
Check("新内容生成新修订", () => Assert(!Equals(changed["revision_id"], first["revision_id"])));
var fetchRequest = new ReadRequest("fetch", library, SourceId: hit.GetProperty("source_id").GetString(),
    RevisionId: hit.GetProperty("revision_id").GetString(), ChunkId: hit.GetProperty("chunk_id").GetString());
Check("旧ID精确取回旧正文", () =>
{
    var old = JsonDocument.Parse(catalog.Read(fetchRequest)).RootElement.GetProperty("hit");
    Assert(old.GetProperty("excerpt").GetString() == hit.GetProperty("excerpt").GetString());
    Assert(!old.GetProperty("excerpt").GetString()!.Contains("第二个版本"));
});
Check("跨库ID拒绝", () => Assert(JsonDocument.Parse(catalog.Read(fetchRequest with { LibraryId = Guid.NewGuid().ToString() })).RootElement.GetProperty("error").GetString() == "not_authorized"));
Check("旧修订不替换成当前内容", () => Assert(JsonDocument.Parse(catalog.Read(fetchRequest with { RevisionId = Guid.NewGuid().ToString() })).RootElement.TryGetProperty("error", out _)));
Check("未知请求字段拒绝", () =>
{
    try { Wire.Parse<ReadRequest>("{\"operation\":\"search\",\"library_id\":\"x\",\"path\":\"outside\"}"); }
    catch (JsonException) { return; }
    throw new Exception("unknown field accepted");
});
Check("响应预算与合法JSON", () =>
{
    string response = catalog.Read(new("search", library, Query: "人工", MaxChars: 1800));
    Assert(Wire.Length(response) <= 1800); using var parsed = JsonDocument.Parse(response);
    Assert(Encoding.UTF8.GetByteCount(response) <= 7200);
    Assert(JsonDocument.Parse(catalog.Read(new("search", library, Query: "人工", MaxChars: 100))).RootElement.TryGetProperty("error", out _));
});
Check("FTS表达式按普通文字处理", () => Assert(Read(catalog, "\" OR *").GetProperty("hits").GetArrayLength() == 0));
Check("不支持格式拒绝", () => { string f = Path.Combine(sourceRoot, "scan.pdf"); File.WriteAllText(f, "%PDF"); Reject("format_not_supported", () => TextFiles.Preview(sourceRoot, f)); });
Check("坏UTF8不静默替换", () =>
{
    string bad = Path.Combine(sourceRoot, "broken.txt"); File.WriteAllBytes(bad, [0xFF, 0xFE, 0x00]);
    Reject("encoding_not_supported", () => catalog.Import(TextFiles.Preview(sourceRoot, bad), true));
});
Check("空正文不伪装成功", () =>
{
    string empty = Path.Combine(sourceRoot, "empty.txt"); File.WriteAllText(empty, "  \n");
    Reject("empty_text", () => catalog.Import(TextFiles.Preview(sourceRoot, empty), true));
});
Check("范围外路径拒绝", () => Reject("outside_scope", () => TextFiles.Preview(Path.Combine(work, "another"), source)));
Check("不能暗自扩大既有授权", () => Reject("policy_change_requires_review", () => catalog.Import(TextFiles.Preview(sourceRoot, source), false)));
Check("不可重复初始化或覆盖库", () => Reject("data_directory_not_empty", () => catalog.Initialize()));
Check("撤销后旧fetch和搜索都不可见", () =>
{
    catalog.Withdraw(first["source_id"]!.ToString()!);
    Assert(JsonDocument.Parse(catalog.Read(fetchRequest)).RootElement.TryGetProperty("error", out _));
    Assert(Read(catalog, "第二个版本").GetProperty("hits").GetArrayLength() == 0);
    Reject("source_withdrawn", () => catalog.Import(TextFiles.Preview(sourceRoot, source), true));
});
Check("缺正文不会生成虚构命中", () => Assert(Read(catalog, "不存在的资料").GetProperty("result_status").GetString() == "insufficient_evidence"));
Check("长中文跨块在默认预算可检索且定位准确", () =>
{
    string text = new string('甲', 1198) + "跨块检索金丝雀" + new string('乙', 1400);
    string path = Path.Combine(sourceRoot, "boundary.md"); File.WriteAllText(path, text, new UTF8Encoding(false));
    catalog.Import(TextFiles.Preview(sourceRoot, path), true);
    var found = Read(catalog, "跨块检索");
    Assert(found.GetProperty("hits").GetArrayLength() > 0);
    foreach (var item in found.GetProperty("hits").EnumerateArray())
    {
        var loc = item.GetProperty("locator");
        string excerpt = item.GetProperty("excerpt").GetString()!;
        Assert(excerpt.Contains("跨块检索"));
        Assert(text[loc.GetProperty("start_char").GetInt32()..loc.GetProperty("end_char").GetInt32()] == excerpt);
    }
});
Check("最大500字符查询跨界完整召回且501字符拒绝", () =>
{
    string query = "LIMIT" + new string('z', 490) + "_END!";
    string path = Path.Combine(sourceRoot, "limit.txt");
    File.WriteAllText(path, new string('x', 1199) + query + new string('y', 1300), new UTF8Encoding(false));
    catalog.Import(TextFiles.Preview(sourceRoot, path), true);
    Assert(Read(catalog, query).GetProperty("hits").GetArrayLength() > 0);
    Assert(Read(catalog, query + "!").GetProperty("error").GetString() == "invalid_argument");
});
Check("重叠片段保留增补字符和跨段行号", () =>
{
    string text = "# 标题\n\n" + string.Concat(Enumerable.Repeat("段落😀𠮷正文。\n\n", 240)) + "末段定位";
    string path = Path.Combine(sourceRoot, "paragraphs.md"); File.WriteAllText(path, text, new UTF8Encoding(false));
    catalog.Import(TextFiles.Preview(sourceRoot, path), true);
    var found = JsonDocument.Parse(catalog.Read(new("search", library, Query: "末段定位", MaxChars: 20000))).RootElement;
    Assert(found.GetProperty("hits").GetArrayLength() > 0);
    string[] runes = text.EnumerateRunes().Select(x => x.ToString()).ToArray();
    foreach (var item in found.GetProperty("hits").EnumerateArray())
    {
        var loc = item.GetProperty("locator"); int start = loc.GetProperty("start_char").GetInt32(), end = loc.GetProperty("end_char").GetInt32();
        Assert(string.Concat(runes[start..end]) == item.GetProperty("excerpt").GetString());
        Assert(1 + runes[..start].Count(x => x == "\n") == loc.GetProperty("line_start").GetInt32());
        Assert(1 + runes[..end].Count(x => x == "\n") == loc.GetProperty("line_end").GetInt32());
    }
});
Check("旧库补索引保留修订引用且再次导入幂等", () =>
{
    var legacy = new Catalog(Path.Combine(work, "legacy-data")); legacy.Initialize();
    string text = new string('甲', 1198) + "跨块升级检索" + new string('乙', 1000);
    string path = Path.Combine(sourceRoot, "legacy.txt"); File.WriteAllText(path, text, new UTF8Encoding(false));
    var original = legacy.Import(TextFiles.Preview(sourceRoot, path), true);
    // 旧版实验库fixture：两个无重叠片段，边界固定1200；不是当前分块器的输出。
    using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(legacy.Root, "catalog.sqlite"), Pooling = false }.ToString()))
    {
        connection.Open(); using var tx = connection.BeginTransaction();
        using (var clear = connection.CreateCommand()) { clear.Transaction = tx; clear.CommandText = "DELETE FROM chunk_fts; DELETE FROM chunks"; clear.ExecuteNonQuery(); }
        foreach (int start in new[] { 0, 1200 })
        {
            string id = Guid.NewGuid().ToString(); int end = start == 0 ? 1200 : text.Length;
            using var insert = connection.CreateCommand(); insert.Transaction = tx;
            insert.CommandText = "INSERT INTO chunks VALUES($id,$revision,$start,$end,1,1,$text); INSERT INTO chunk_fts(chunk_id,text) VALUES($id,$text)";
            insert.Parameters.AddWithValue("$id", id); insert.Parameters.AddWithValue("$revision", original["revision_id"]);
            insert.Parameters.AddWithValue("$start", start); insert.Parameters.AddWithValue("$end", end); insert.Parameters.AddWithValue("$text", text[start..end]); insert.ExecuteNonQuery();
        }
        tx.Commit();
    }
    Assert(Read(legacy, "跨块升级").GetProperty("hits").GetArrayLength() == 0);
    var oldHit = Read(legacy, "甲甲甲").GetProperty("hits")[0].Clone();
    int before = Read(legacy, "甲甲甲").GetProperty("index_generation").GetInt32();
    var upgraded = legacy.Import(TextFiles.Preview(sourceRoot, path), true);
    Assert((bool)upgraded["reused"]! && Equals(upgraded["revision_id"], original["revision_id"]));
    var after = Read(legacy, "跨块升级"); Assert(after.GetProperty("hits").GetArrayLength() > 0);
    Assert(after.GetProperty("index_generation").GetInt32() == before + 1);
    var oldFetch = JsonDocument.Parse(legacy.Read(new("fetch", legacy.LibraryId, SourceId: oldHit.GetProperty("source_id").GetString(),
        RevisionId: oldHit.GetProperty("revision_id").GetString(), ChunkId: oldHit.GetProperty("chunk_id").GetString()))).RootElement.GetProperty("hit");
    Assert(oldFetch.GetProperty("citation").GetString() == oldHit.GetProperty("citation").GetString());
    Assert(oldFetch.GetProperty("excerpt").GetString() == oldHit.GetProperty("excerpt").GetString());
    legacy.Import(TextFiles.Preview(sourceRoot, path), true);
    Assert(Read(legacy, "跨块升级").GetProperty("index_generation").GetInt32() == before + 1);
    legacy.Withdraw(original["source_id"]!.ToString()!);
    Assert(Read(legacy, "跨块升级").GetProperty("hits").GetArrayLength() == 0);
});
var receipt = new { schema = "menslink-local-checks/v1", result = failed == 0 ? "passed" : "failed", checks = results,
    environment = new { os = Environment.OSVersion.ToString(), framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription },
    limitations = new[] { "本机合成样本，不是用户体验验收", "未验证Win11、PDF、Obsidian实际加载、Codex Desktop原生工具卡", "没有性能或中文总体召回率结论" } };
File.WriteAllText(Path.Combine(work, "receipt.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions(Wire.Json) { WriteIndented = true }), new UTF8Encoding(false));
Console.WriteLine($"Checks: {results.Count - failed}/{results.Count}; receipt: {Path.Combine(work, "receipt.json")}");
return failed == 0 ? 0 : 1;
