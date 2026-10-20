using MensLink.Core;
using MensLink.Sqlite;

Console.OutputEncoding = new System.Text.UTF8Encoding(false);
Console.InputEncoding = new System.Text.UTF8Encoding(false);
try
{
    if (args.Length == 0 || args[0] == "help")
    {
        Console.WriteLine("MensLink development: init | preview | import | status | search | fetch | withdraw | serve. Every command requires --data-root; preview also needs --root and --file; import reads --manifest. No installation or automatic host configuration.");
        return 0;
    }
    string command = args[0];
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int i = 1; i < args.Length; i++)
    {
        string name = args[i];
        if (name == "--allow-agent") { if (!options.TryAdd(name, "true")) throw new ContractException("invalid_argument"); continue; }
        if (!name.StartsWith("--") || i + 1 >= args.Length || args[i + 1].StartsWith("--") || !options.TryAdd(name, args[++i]))
            throw new ContractException("invalid_argument");
    }
    string Required(string key) => options.TryGetValue(key, out string? value) ? value : throw new ContractException("invalid_argument");
    string[] allowed = command switch
    {
        "init" or "status" or "serve" => ["--data-root"],
        "preview" => ["--data-root", "--root", "--file"],
        "import" => ["--data-root", "--manifest", "--allow-agent"],
        "search" => ["--data-root", "--query", "--max-chars"],
        "fetch" => ["--data-root", "--source-id", "--revision-id", "--chunk-id", "--max-chars"],
        "withdraw" => ["--data-root", "--source-id"],
        _ => throw new ContractException("invalid_argument")
    };
    if (options.Keys.Any(key => !allowed.Contains(key))) throw new ContractException("invalid_argument");
    var catalog = new Catalog(Required("--data-root"));
    switch (command)
    {
        case "init": Console.WriteLine(Wire.Serialize(new { library_id = catalog.Initialize(), schema = "p1-v1" })); break;
        case "preview": Console.WriteLine(Wire.Serialize(TextFiles.Preview(Required("--root"), Required("--file")))); break;
        case "import":
            var preview = Wire.Parse<ImportPreview>(File.ReadAllText(Required("--manifest"), System.Text.Encoding.UTF8));
            Console.WriteLine(Wire.Serialize(catalog.Import(preview, options.ContainsKey("--allow-agent")))); break;
        case "withdraw": catalog.Withdraw(Required("--source-id")); Console.WriteLine("{\"withdrawn\":true}"); break;
        case "serve":
            using (var cancellation = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
                Console.Error.WriteLine($"MensLink read host: {catalog.LibraryId}; development build; Ctrl+C to stop.");
                try { await ReadPipe.Serve(catalog.LibraryId, request => catalog.Read(request), cancellation.Token); }
                catch (OperationCanceledException) { }
            }
            break;
        default:
            int budget = 6000;
            if (options.TryGetValue("--max-chars", out string? b))
            {
                if (!int.TryParse(b, out budget)) throw new ContractException("invalid_argument");
            }
            var request = new ReadRequest(command, catalog.LibraryId,
                Query: options.GetValueOrDefault("--query"), SourceId: options.GetValueOrDefault("--source-id"),
                RevisionId: options.GetValueOrDefault("--revision-id"), ChunkId: options.GetValueOrDefault("--chunk-id"), MaxChars: budget);
            string result = catalog.Read(request, agent: false);
            Console.WriteLine(result);
            using (var document = System.Text.Json.JsonDocument.Parse(result))
                if (document.RootElement.TryGetProperty("error", out _)) return 2;
            break;
    }
    return 0;
}
catch (Exception error) when (error is ContractException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
{
    Console.WriteLine(Wire.Serialize(Wire.Error(error is ContractException ce ? ce.Code : "operation_failed")));
    return 2;
}
