using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MensLink.Core;
using MensLink.Sqlite;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

Console.OutputEncoding = new UTF8Encoding(false);
string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string work = Path.Combine(repo, "local/mcp-checks", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
Directory.CreateDirectory(work);
var catalog = new Catalog(Path.Combine(work, "data")); string library = catalog.Initialize();
string source = Path.Combine(repo, "tests/fixtures/knowledge.md");
catalog.Import(TextFiles.Preview(Path.GetDirectoryName(source)!, source), true);
string executable = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")
    ?? throw new InvalidOperationException("Run this probe through scripts/dev.ps1."), "dotnet.exe");
string cli = Path.Combine(repo, "apps/MensLink.Cli/bin/Debug/net10.0/menslink.dll");
string mcp = Path.Combine(repo, "integrations/codex/MensLink.Mcp/bin/Debug/net10.0/menslink-mcp.dll");
var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
foreach (string arg in new[] { cli, "serve", "--data-root", catalog.Root }) info.ArgumentList.Add(arg);
using var host = Process.Start(info)!;
Task<string> hostErrors = host.StandardError.ReadToEndAsync();
var results = new List<object>(); bool passed = false;
void Assert(bool test, string message) { if (!test) throw new Exception(message); results.Add(new { name = message, status = "passed" }); }
try
{
    string ready = await ReadPipe.Call(new("status", library));
    Assert(!JsonDocument.Parse(ready).RootElement.TryGetProperty("error", out _), "named pipe read host ready");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
    var transport = new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "MensLink-local-probe", Command = executable, Arguments = [mcp]
    });
    await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
    var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
    Assert(tools.Select(t => t.Name).OrderBy(x => x).SequenceEqual(new[] { "fetch", "search", "status" }), "tools/list exactly search fetch status");
    async Task<JsonElement> Call(string name, Dictionary<string, object?> args)
    {
        var result = await client.CallToolAsync(name, args, cancellationToken: timeout.Token);
        string raw = result.Content.OfType<TextContentBlock>().First().Text;
        return JsonDocument.Parse(raw).RootElement.Clone();
    }
    var status = await Call("status", new() { ["library_id"] = library });
    Assert(status.GetProperty("source_count").GetInt32() == 1, "MCP status reads approved library");
    var unknown = await Call("status", new() { ["library_id"] = library, ["path"] = "outside" });
    Assert(unknown.GetProperty("error").GetString() == "invalid_argument", "MCP unknown arguments rejected before IPC");
    var search = await Call("search", new() { ["library_id"] = library, ["query"] = "E_POLICY_403" });
    var hit = search.GetProperty("hits")[0];
    Assert(hit.GetProperty("excerpt").GetString()!.Contains("E_POLICY_403"), "MCP search returns actual fixture");
    var fetchArgs = new Dictionary<string, object?>() { ["library_id"] = library, ["source_id"] = hit.GetProperty("source_id").GetString(),
        ["revision_id"] = hit.GetProperty("revision_id").GetString(), ["chunk_id"] = hit.GetProperty("chunk_id").GetString() };
    var fetch = await Call("fetch", fetchArgs);
    Assert(fetch.GetProperty("hit").GetProperty("citation").GetString() == hit.GetProperty("citation").GetString(), "MCP search to fetch preserves citation identity");
    catalog.Withdraw(hit.GetProperty("source_id").GetString()!);
    var withdrawn = await Call("fetch", fetchArgs);
    Assert(withdrawn.TryGetProperty("error", out _), "MCP old fetch rejected after withdrawal");
    host.Kill(entireProcessTree: true); await host.WaitForExitAsync();
    var unavailable = await Call("status", new() { ["library_id"] = library });
    Assert(unavailable.GetProperty("error").GetString() == "service_unavailable", "MCP disconnected host gives structured unavailable");
    passed = true;
}
catch (Exception error) { results.Add(new { name = "protocol_path", status = "failed", error = error.Message }); Console.Error.WriteLine(error); }
finally
{
    if (!host.HasExited) { host.Kill(entireProcessTree: true); await host.WaitForExitAsync(); }
    File.WriteAllText(Path.Combine(work, "host.stderr.log"), await hostErrors, new UTF8Encoding(false));
    File.WriteAllText(Path.Combine(work, "receipt.json"), JsonSerializer.Serialize(new
    {
        schema = "menslink-mcp-probe/v1", result = passed ? "passed" : "failed", checks = results,
        access_path = "official_csharp_sdk_client_to_stdio_bridge_to_named_pipe_host",
        codex_desktop_native_verified = false, obsidian_loaded = false,
        limitation = "仅为本机产品协议探针；没有注册或验证当前Codex Desktop原生工具卡"
    }, new JsonSerializerOptions(Wire.Json) { WriteIndented = true }), new UTF8Encoding(false));
}
Console.WriteLine($"MCP probe: {(passed ? "passed" : "failed")}; receipt: {Path.Combine(work, "receipt.json")}");
return passed ? 0 : 1;
