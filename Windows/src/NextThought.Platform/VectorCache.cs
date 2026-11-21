using System.Security.Cryptography;
using System.Text;

namespace NextThought.Platform;

public static class VectorCache
{
    public static Dictionary<string, float[]> Load(string file, string identity, int dimension)
    {
        if (!File.Exists(file)) return [];
        if (new FileInfo(file).Length > 128L * 1024 * 1024) throw new InvalidDataException("向量缓存超过上限。");
        var bytes = File.ReadAllBytes(file);
        if (bytes.Length < 32 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32)), bytes.AsSpan(bytes.Length - 32))) throw new InvalidDataException("向量缓存校验失败。");
        using var stream = new MemoryStream(bytes, 0, bytes.Length - 32); using var reader = new BinaryReader(stream, Encoding.UTF8);
        if (reader.ReadString() != "NTVEC1" || reader.ReadString() != identity || reader.ReadInt32() != dimension) throw new InvalidDataException("向量缓存版本不匹配。");
        int count = reader.ReadInt32(); if (count < 0 || count > 20000) throw new InvalidDataException("向量缓存条目异常。");
        var result = new Dictionary<string, float[]>();
        for (int i = 0; i < count; i++)
        {
            var id = reader.ReadString(); if (id.Length != 64 || !id.All(Uri.IsHexDigit)) throw new InvalidDataException("向量缓存 ID 异常。");
            var values = new float[dimension]; double norm = 0;
            for (int j = 0; j < dimension; j++) { values[j] = reader.ReadSingle(); norm += (double)values[j] * values[j]; }
            if (!double.IsFinite(norm) || Math.Abs(norm - 1) > .01 || !result.TryAdd(id, values)) throw new InvalidDataException("向量缓存内容异常。");
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("向量缓存包含未识别内容。"); return result;
    }
    public static async Task SaveAsync(string file, string identity, int dimension, IReadOnlyDictionary<string, float[]> vectors, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
        {
            writer.Write("NTVEC1"); writer.Write(identity); writer.Write(dimension); writer.Write(vectors.Count);
            foreach (var (id, vector) in vectors.OrderBy(p => p.Key, StringComparer.Ordinal))
            { ct.ThrowIfCancellationRequested(); if (vector.Length != dimension) throw new InvalidDataException("向量维度不一致。"); writer.Write(id); foreach (var value in vector) writer.Write(value); }
        }
        var data = memory.ToArray(); var digest = SHA256.HashData(data); Directory.CreateDirectory(Path.GetDirectoryName(file)!); var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = File.Create(temporary)) { await output.WriteAsync(data, ct).ConfigureAwait(false); await output.WriteAsync(digest, ct).ConfigureAwait(false); }
            ct.ThrowIfCancellationRequested(); File.Move(temporary, file, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
