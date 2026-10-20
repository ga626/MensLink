using System.Text.Json;
using MensLink.Core;
using Microsoft.Data.Sqlite;

namespace MensLink.Sqlite;

public sealed class Catalog
{
    public string Root { get; }
    private string Database => Path.Combine(Root, "catalog.sqlite");
    public Catalog(string root) => Root = Path.GetFullPath(root);

    public string Initialize()
    {
        Directory.CreateDirectory(Root);
        if (Directory.EnumerateFileSystemEntries(Root).Any())
            throw new ContractException("data_directory_not_empty");
        using var connection = Open(create: true);
        using var transaction = connection.BeginTransaction();
        Run(connection, transaction, """
            CREATE TABLE meta(key TEXT PRIMARY KEY,value TEXT NOT NULL);
            CREATE TABLE sources(id TEXT PRIMARY KEY,uri TEXT UNIQUE NOT NULL,title TEXT NOT NULL,
              agent_allowed INTEGER NOT NULL,withdrawn INTEGER NOT NULL DEFAULT 0,revision_id TEXT);
            CREATE TABLE revisions(id TEXT PRIMARY KEY,source_id TEXT NOT NULL REFERENCES sources(id),
              version_id TEXT NOT NULL,artifact_id TEXT NOT NULL,raw_hash TEXT NOT NULL,text_hash TEXT NOT NULL,
              captured_at TEXT NOT NULL,text TEXT NOT NULL,UNIQUE(source_id,raw_hash));
            CREATE TABLE chunks(id TEXT PRIMARY KEY,revision_id TEXT NOT NULL REFERENCES revisions(id),
              start_char INTEGER NOT NULL,end_char INTEGER NOT NULL,line_start INTEGER NOT NULL,line_end INTEGER NOT NULL,
              text TEXT NOT NULL);
            CREATE VIRTUAL TABLE chunk_fts USING fts5(chunk_id UNINDEXED,text,tokenize='trigram');
            """);
        string libraryId = Guid.NewGuid().ToString();
        foreach (var (key, value) in new[] { ("schema", "p1-v1"), ("library_id", libraryId), ("policy_epoch", "1"), ("generation", "0") })
            Run(connection, transaction, "INSERT INTO meta VALUES($key,$value)", ("$key", key), ("$value", value));
        transaction.Commit();
        return libraryId;
    }

    public string LibraryId { get { using var c = Open(readOnly: true); return Meta(c, "library_id"); } }

    public Dictionary<string, object?> Import(ImportPreview preview, bool agentAllowed)
    {
        using var writer = new FileStream(Path.Combine(Root, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        TextSnapshot snapshot = TextFiles.Read(preview);
        using var c = Open();
        using var tx = c.BeginTransaction();
        string uri = new Uri(preview.Path).AbsoluteUri;
        string? sourceId = Scalar(c, tx, "SELECT id FROM sources WHERE uri=$uri", ("$uri", uri));
        if (sourceId == null)
        {
            sourceId = Guid.NewGuid().ToString();
            Run(c, tx, "INSERT INTO sources(id,uri,title,agent_allowed) VALUES($id,$uri,$title,$allowed)",
                ("$id", sourceId), ("$uri", uri), ("$title", Path.GetFileName(preview.Path)), ("$allowed", agentAllowed ? 1 : 0));
        }
        else
        {
            // 再导入不解除撤销，也不暗自扩大既有授权。
            if (Scalar(c, tx, "SELECT withdrawn FROM sources WHERE id=$id", ("$id", sourceId)) == "1")
                throw new ContractException("source_withdrawn");
            if (Scalar(c, tx, "SELECT agent_allowed FROM sources WHERE id=$id", ("$id", sourceId)) != (agentAllowed ? "1" : "0"))
                throw new ContractException("policy_change_requires_review");
        }
        string? revisionId = Scalar(c, tx, "SELECT id FROM revisions WHERE source_id=$id AND raw_hash=$hash",
            ("$id", sourceId), ("$hash", snapshot.RawHash));
        bool reused = revisionId != null;
        if (revisionId == null)
        {
            revisionId = Guid.NewGuid().ToString();
            string artifactId = Guid.NewGuid().ToString();
            Run(c, tx, "INSERT INTO revisions VALUES($id,$source,$version,$artifact,$raw,$hash,$time,$text)",
                ("$id", revisionId), ("$source", sourceId), ("$version", Guid.NewGuid().ToString()), ("$artifact", artifactId),
                ("$raw", snapshot.RawHash), ("$hash", snapshot.TextHash), ("$time", DateTimeOffset.UtcNow.ToString("o")), ("$text", snapshot.Text));
        }
        bool chunksAdded = false;
        // 已有修订只补缺失范围；旧片段和引用不删除，重复导入不反复重建。
        foreach (TextChunk chunk in TextFiles.Chunks(snapshot.Text))
        {
            if (Scalar(c, tx, "SELECT id FROM chunks WHERE revision_id=$revision AND start_char=$start AND end_char=$end",
                ("$revision", revisionId), ("$start", chunk.StartChar), ("$end", chunk.EndChar)) == null)
            {
                string chunkId = Guid.NewGuid().ToString();
                Run(c, tx, "INSERT INTO chunks VALUES($id,$revision,$start,$end,$lineStart,$lineEnd,$text)",
                    ("$id", chunkId), ("$revision", revisionId), ("$start", chunk.StartChar), ("$end", chunk.EndChar),
                    ("$lineStart", chunk.LineStart), ("$lineEnd", chunk.LineEnd), ("$text", chunk.Text));
                Run(c, tx, "INSERT INTO chunk_fts(chunk_id,text) VALUES($id,$text)", ("$id", chunkId), ("$text", chunk.Text));
                chunksAdded = true;
            }
        }
        string? current = Scalar(c, tx, "SELECT revision_id FROM sources WHERE id=$id", ("$id", sourceId));
        if (current != revisionId)
        {
            Run(c, tx, "UPDATE sources SET revision_id=$revision WHERE id=$source", ("$revision", revisionId), ("$source", sourceId));
        }
        if (current != revisionId || chunksAdded)
        {
            Run(c, tx, "UPDATE meta SET value=CAST(value AS INTEGER)+1 WHERE key='generation'");
        }
        tx.Commit();
        return new() { ["library_id"] = LibraryId, ["source_id"] = sourceId, ["revision_id"] = revisionId,
            ["reused"] = reused, ["agent_allowed"] = agentAllowed, ["raw_sha256"] = snapshot.RawHash };
    }

    public void Withdraw(string sourceId)
    {
        if (!Guid.TryParse(sourceId, out _)) throw new ContractException("invalid_argument");
        using var writer = new FileStream(Path.Combine(Root, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var c = Open();
        using var tx = c.BeginTransaction();
        if (Scalar(c, tx, "SELECT id FROM sources WHERE id=$id", ("$id", sourceId)) == null)
            throw new ContractException("source_not_found");
        Run(c, tx, "UPDATE sources SET withdrawn=1 WHERE id=$id", ("$id", sourceId));
        Run(c, tx, "UPDATE meta SET value=CAST(value AS INTEGER)+1 WHERE key IN ('policy_epoch','generation')");
        tx.Commit();
    }

    public string Read(ReadRequest request, bool agent = true)
    {
        try
        {
            if (!Guid.TryParse(request.LibraryId, out _) || request.LibraryId != LibraryId)
                throw new ContractException("not_authorized");
            if (request.MaxChars is < 1024 or > 24000) throw new ContractException("invalid_argument");
            using var c = Open(readOnly: true);
            using var tx = c.BeginTransaction();
            string epoch = Meta(c, "policy_epoch", tx);
            var response = new Dictionary<string, object?>
            {
                ["schema_version"] = "menslink-p1/v1", ["library_id"] = request.LibraryId,
                ["policy_epoch"] = int.Parse(epoch), ["index_generation"] = int.Parse(Meta(c, "generation", tx)),
                ["engine_mode"] = "lexical", ["truncated"] = false, ["coverage_gaps"] = Array.Empty<string>()
            };
            string filter = agent ? " AND s.agent_allowed=1" : "";
            switch (request.Operation)
            {
                case "status":
                    response["result_status"] = "evidence_candidates";
                    response["source_count"] = int.Parse(Scalar(c, tx, "SELECT count(*) FROM sources s WHERE s.withdrawn=0" + filter)!);
                    response["capabilities"] = new[] { "md_utf8", "txt_utf8", "lexical", "text_locator" };
                    response["sqlite_version"] = Scalar(c, tx, "SELECT sqlite_version()");
                    response["journal_mode"] = Scalar(c, tx, "PRAGMA journal_mode");
                    response["stage"] = "development_foundation";
                    break;
                case "search":
                    if (string.IsNullOrWhiteSpace(request.Query) || Wire.Length(request.Query) > Wire.MaxQueryCodePoints || request.Limit is < 1 or > 20 || request.MaxChars > 20000)
                        throw new ContractException("invalid_argument");
                    bool shortQuery = Wire.Length(request.Query) < 3;
                    if (shortQuery && int.Parse(Scalar(c, tx, "SELECT count(*) FROM chunks ch JOIN revisions r ON ch.revision_id=r.id JOIN sources s ON r.source_id=s.id WHERE s.withdrawn=0 AND s.revision_id=r.id" + filter)!) > 10000)
                        throw new ContractException("short_query_scope_too_large");
                    string condition = shortQuery ? "instr(lower(ch.text),lower($query))>0"
                        : "ch.id IN (SELECT chunk_id FROM chunk_fts WHERE chunk_fts MATCH $query)";
                    using (var command = Command(c, tx, SelectHit + " WHERE s.withdrawn=0 AND s.revision_id=r.id AND " + condition + filter + " ORDER BY s.id,ch.start_char LIMIT $limit"))
                    {
                        command.Parameters.AddWithValue("$query", shortQuery ? request.Query : "\"" + request.Query.Replace("\"", "\"\"") + "\"");
                        command.Parameters.AddWithValue("$limit", request.Limit + 1);
                        using var rows = command.ExecuteReader();
                        var hits = new List<Dictionary<string, object?>>();
                        while (rows.Read()) hits.Add(Hit(rows));
                        response["truncated"] = hits.Count > request.Limit;
                        if (hits.Count > request.Limit) hits.RemoveAt(hits.Count - 1);
                        response["hits"] = hits;
                        response["result_status"] = hits.Count == 0 ? "insufficient_evidence" : "evidence_candidates";
                        response["reason_codes"] = hits.Count == 0 ? new[] { "no_hits" } : Array.Empty<string>();
                        response["ranking"] = "deterministic_source_order_p1";
                        while (Wire.Length(Wire.Serialize(response)) > request.MaxChars && hits.Count > 0)
                        {
                            hits.RemoveAt(hits.Count - 1);
                            response["truncated"] = true;
                        }
                        if (hits.Count == 0 && (string)response["result_status"]! == "evidence_candidates")
                            throw new ContractException("budget_too_small");
                    }
                    break;
                case "fetch":
                    if (!Guid.TryParse(request.SourceId, out _) || !Guid.TryParse(request.RevisionId, out _) || !Guid.TryParse(request.ChunkId, out _))
                        throw new ContractException("invalid_argument");
                    using (var command = Command(c, tx, SelectHit + " WHERE s.id=$source AND r.id=$revision AND ch.id=$chunk AND s.withdrawn=0" + filter))
                    {
                        command.Parameters.AddWithValue("$source", request.SourceId);
                        command.Parameters.AddWithValue("$revision", request.RevisionId);
                        command.Parameters.AddWithValue("$chunk", request.ChunkId);
                        using var row = command.ExecuteReader();
                        if (!row.Read()) throw new ContractException("version_not_found_or_not_authorized");
                        var hit = Hit(row);
                        response["result_status"] = "evidence_candidates";
                        response["hit"] = hit;
                        response["has_more"] = false;
                    }
                    break;
                default: throw new ContractException("invalid_argument");
            }
            tx.Commit();
            string result = Wire.Budget(response, request.MaxChars);
            // 旧召回、缓存和在途响应不能跨越新的撤销代际。
            if (Meta(c, "policy_epoch") != epoch) throw new ContractException("policy_changed");
            return result;
        }
        catch (ContractException error) { return Wire.Serialize(Wire.Error(error.Code)); }
        catch (SqliteException) { return Wire.Serialize(Wire.Error("index_unavailable")); }
    }

    private const string SelectHit = """
        SELECT s.id,s.title,s.uri,r.id,r.version_id,r.artifact_id,r.captured_at,r.text_hash,
          ch.id,ch.text,ch.start_char,ch.end_char,ch.line_start,ch.line_end
        FROM sources s JOIN revisions r ON r.source_id=s.id JOIN chunks ch ON ch.revision_id=r.id
        """;

    private static Dictionary<string, object?> Hit(SqliteDataReader row)
    {
        string source = row.GetString(0), revision = row.GetString(3), chunk = row.GetString(8);
        int start = row.GetInt32(10), end = row.GetInt32(11);
        return new()
        {
            ["source_id"] = source, ["title"] = row.GetString(1), ["source_uri"] = row.GetString(2),
            ["revision_id"] = revision, ["content_version_id"] = row.GetString(4), ["artifact_id"] = row.GetString(5),
            ["captured_at"] = row.GetString(6), ["artifact_sha256"] = row.GetString(7), ["chunk_id"] = chunk,
            ["excerpt"] = row.GetString(9), ["text_kind"] = "native_text", ["coverage"] = "native_text_complete",
            ["locator"] = new { kind = "text", start_char = start, end_char = end, line_start = row.GetInt32(12), line_end = row.GetInt32(13) },
            ["citation"] = $"menslink:{source}/{revision}/{chunk}#chars={start}-{end}", ["locator_target"] = "normalized_artifact"
        };
    }

    private SqliteConnection Open(bool create = false, bool readOnly = false)
    {
        if (!create && !File.Exists(Database)) throw new ContractException("library_not_initialized");
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Database, Mode = create ? SqliteOpenMode.ReadWriteCreate : readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Pooling = false, DefaultTimeout = 5
        }.ToString());
        connection.Open();
        Run(connection, null, "PRAGMA foreign_keys=ON");
        if (!create && Meta(connection, "schema") != "p1-v1") { connection.Dispose(); throw new ContractException("incompatible_schema"); }
        return connection;
    }

    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction? tx, string sql)
    {
        var command = c.CreateCommand(); command.Transaction = tx; command.CommandText = sql; return command;
    }
    private static string Meta(SqliteConnection c, string key, SqliteTransaction? tx = null) =>
        Scalar(c, tx, "SELECT value FROM meta WHERE key=$key", ("$key", key)) ?? throw new ContractException("incompatible_schema");
    private static string? Scalar(SqliteConnection c, SqliteTransaction? tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var command = Command(c, tx, sql);
        foreach (var p in parameters) command.Parameters.AddWithValue(p.Key, p.Value);
        return command.ExecuteScalar()?.ToString();
    }
    private static void Run(SqliteConnection c, SqliteTransaction? tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var command = Command(c, tx, sql);
        foreach (var p in parameters) command.Parameters.AddWithValue(p.Key, p.Value);
        command.ExecuteNonQuery();
    }
}
