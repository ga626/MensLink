using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MensLink.Core;

public static class TextFiles
{
    public const int MaxBytes = 8 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static ImportPreview Preview(string root, string path)
    {
        root = System.IO.Path.GetFullPath(root);
        path = System.IO.Path.GetFullPath(path);
        Guard(root, path);
        if (!new[] { ".txt", ".md" }.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))
            throw new ContractException("format_not_supported");
        var file = new FileInfo(path);
        if (!file.Exists) throw new ContractException("source_unavailable");
        if (file.Length > MaxBytes) throw new ContractException("requires_limit_override");
        return new(root, path, file.Length, file.LastWriteTimeUtc.Ticks);
    }

    public static TextSnapshot Read(ImportPreview preview)
    {
        if (Preview(preview.Root, preview.Path) != preview)
            throw new ContractException("changed_since_preview");
        // 只共享读取，读取期间不允许其他句柄写入或删除。
        using var input = new FileStream(preview.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (OperatingSystem.IsWindows())
        {
            var resolved = new StringBuilder(32768);
            uint length = GetFinalPathNameByHandle(input.SafeFileHandle, resolved, (uint)resolved.Capacity, 0);
            if (length == 0 || length >= resolved.Capacity) throw new ContractException("path_unsupported");
            string final = resolved.ToString();
            if (final.StartsWith(@"\\?\")) final = final[4..];
            Guard(preview.Root, final);
            if (!string.Equals(final, preview.Path, StringComparison.OrdinalIgnoreCase))
                throw new ContractException("changed_since_preview");
        }
        if (input.Length != preview.Size || input.Length > MaxBytes)
            throw new ContractException("changed_since_preview");
        using var memory = new MemoryStream();
        input.CopyTo(memory);
        byte[] bytes = memory.ToArray();
        if (Preview(preview.Root, preview.Path) != preview) throw new ContractException("changed_since_preview");
        string text;
        try { text = StrictUtf8.GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw new ContractException("encoding_not_supported"); }
        if (text.Contains('\0')) throw new ContractException("encoding_not_supported");
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.IsNullOrWhiteSpace(text)) throw new ContractException("empty_text");
        return new(text, Hash(bytes), Hash(Encoding.UTF8.GetBytes(text)));
    }

    public static void Guard(string root, string path)
    {
        root = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        path = System.IO.Path.GetFullPath(path);
        if (path.StartsWith(@"\\")) throw new ContractException("path_unsupported");
        if (!path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ContractException("outside_scope");
        for (FileSystemInfo? entry = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
             entry != null; entry = entry is FileInfo fi ? fi.Directory : ((DirectoryInfo)entry).Parent)
            if (entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ContractException("links_not_allowed");
    }

    public static IEnumerable<TextChunk> Chunks(string text, int maxChars = 1200)
    {
        if (maxChars < Wire.MaxQueryCodePoints) throw new ArgumentOutOfRangeException(nameof(maxChars));
        int offset = 0, line = 1;
        Rune[] runes = text.EnumerateRunes().ToArray();
        while (offset < runes.Length)
        {
            int end = Math.Min(offset + maxChars, runes.Length);
            if (end < runes.Length)
            {
                // 优先段落，其次换行；末半段之外不截断，保证重叠后仍向前推进。
                int minimum = offset + Math.Max(maxChars / 2, Wire.MaxQueryCodePoints);
                int boundary = 0;
                for (int i = end - 1; i >= minimum; i--)
                {
                    if (runes[i].Value != '\n') continue;
                    if (boundary == 0) boundary = i + 1;
                    if (runes[i - 1].Value == '\n') { boundary = i + 1; break; }
                }
                if (boundary != 0) end = boundary;
            }
            int size = end - offset;
            string body = string.Concat(runes.Skip(offset).Take(size).Select(x => x.ToString()));
            int lastLine = line + body.Count(x => x == '\n');
            yield return new(body, offset, offset + size, line, lastLine);
            if (end == runes.Length) break;
            // 最大字面查询减一的重叠，使支持范围内的跨界短语完整落在某个片段中。
            int next = end - (Wire.MaxQueryCodePoints - 1);
            line += runes.Skip(offset).Take(next - offset).Count(x => x.Value == '\n');
            offset = next;
        }
    }

    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint size, uint flags);
}
