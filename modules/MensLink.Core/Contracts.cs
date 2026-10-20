using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace MensLink.Core;

public sealed class ContractException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed record ImportPreview(string Root, string Path, long Size, long ModifiedTicks);
public sealed record TextSnapshot(string Text, string RawHash, string TextHash);
public sealed record TextChunk(string Text, int StartChar, int EndChar, int LineStart, int LineEnd);
public sealed record ReadRequest(string Operation, string LibraryId, string? Query = null,
    string? SourceId = null, string? RevisionId = null, string? ChunkId = null,
    int Limit = 8, int MaxChars = 6000);

public static class Wire
{
    public const int MaxQueryCodePoints = 500;

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Json);
    public static T Parse<T>(string value) => JsonSerializer.Deserialize<T>(value, Json)
        ?? throw new ContractException("invalid_argument");
    public static int Length(string value) => value.EnumerateRunes().Count();
    public static string Slice(string value, int count) => string.Concat(value.EnumerateRunes()
        .Take(Math.Max(0, count)).Select(x => x.ToString()));
    public static Dictionary<string, object?> Error(string code) => new()
    {
        ["schema_version"] = "menslink-p1/v1", ["result_status"] = "unavailable", ["error"] = code
    };

    public static string Budget(Dictionary<string, object?> response, int maxChars)
    {
        if (maxChars is < 1024 or > 24000) throw new ContractException("invalid_argument");
        string encoded = Serialize(response);
        if (Length(encoded) > maxChars || Encoding.UTF8.GetByteCount(encoded) > maxChars * 4)
            throw new ContractException("budget_too_small");
        return encoded;
    }
}
