using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using NextThought.Core;

namespace NextThought.Platform;

public sealed record RetrievalResult(IReadOnlyList<SourceCitation> Sources, string Method, string? Warning, double Seconds, IReadOnlyList<RankedChunk> SemanticScores);
public sealed class RetrievalService(AppPaths paths, EmbeddingModelStore? modelStore = null) : IDisposable
{
    public const double MinimumSimilarity = .55;
    private readonly EmbeddingModelStore models = modelStore ?? new(paths);
    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyList<RetrievalChunk> chunks = [];
    private KeywordIndex? lexical;
    private QwenEmbedder? embedder;
    private Dictionary<string, float[]>? vectors;
    private string? snapshot, warning;
    private string? fittedSnapshot;
    private bool disposed;
    public int CorpusBuildCount { get; private set; }
    public int DocumentEmbeddingCount { get; private set; }
    public int CachedVectorsLoaded { get; private set; }
    public static string Identity => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { model = EmbeddingModelStore.Manifest, QwenEmbedder.PipelineVersion, chunker = DocumentChunks.Version }, JsonDefaults.Options))));
    private string CacheFile => Path.Combine(paths.Root, "indexes", "vectors-" + Identity + ".bin");
    public async Task<RetrievalResult> SearchAsync(string query, bool semantic, CancellationToken ct = default, IReadOnlyList<MaterialDocument>? documents = null, double minimumSimilarity = MinimumSimilarity)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this); var clock = Stopwatch.StartNew();
            return await Task.Run(async () =>
            {
                Refresh(documents); ct.ThrowIfCancellationRequested();
                if (!semantic || chunks.Count == 0) return Lexical(semantic ? "资料库为空。" : null);
                try
                {
                    if (!models.IsInstalled) return Lexical("尚未准备语义模型，已使用关键词检索。");
                    if (chunks.Count > 20000) return Lexical("资料块超过语义索引上限，已使用关键词检索，请缩小资料库。");
                    if (embedder is null) { await models.VerifyAsync(ct).ConfigureAwait(false); embedder = new(models); }
                    if (fittedSnapshot != snapshot)
                    { chunks = DocumentChunks.FitTokenBudget(chunks, embedder.TokenCount, EmbeddingModelStore.Manifest.MaxTokens); lexical = new KeywordIndex(chunks); fittedSnapshot = snapshot; }
                    if (vectors is null)
                    {
                        try { vectors = VectorCache.Load(CacheFile, Identity, EmbeddingModelStore.Manifest.Dimension); CachedVectorsLoaded = vectors.Count; }
                        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException) { vectors = []; warning = "向量缓存损坏或不兼容，已重新构建。"; }
                    }
                    int dirty = 0;
                    foreach (var chunk in chunks)
                    {
                        ct.ThrowIfCancellationRequested(); if (vectors.ContainsKey(chunk.Id)) continue;
                        vectors[chunk.Id] = embedder.Embed(chunk.Text, false, ct); DocumentEmbeddingCount++; dirty++;
                        if (dirty % 8 == 0) await SaveAsync(ct).ConfigureAwait(false);
                    }
                    if (dirty > 0) await SaveAsync(ct).ConfigureAwait(false);
                    var vector = embedder.Embed(query, true, ct);
                    var semanticRanks = chunks.Select(c => new RankedChunk(c, Dot(vector, vectors[c.Id]))).OrderByDescending(r => r.Score).ToArray();
                    var lexicalRanks = lexical!.SearchRanked(query).Select((r, i) => (r.Chunk.Id, Rank: i + 1)).ToDictionary(p => p.Id, p => p.Rank);
                    var combined = semanticRanks.Select((r, i) => (r, Rank: i + 1)).Where(p => p.r.Score >= minimumSimilarity)
                        .OrderByDescending(p => 1.0 / (60 + p.Rank) + (lexicalRanks.TryGetValue(p.r.Chunk.Id, out var rank) ? 1.0 / (60 + rank) : 0)).Select(p => p.r.Chunk);
                    return new RetrievalResult(DocumentChunks.Citations(combined), "语义 + 关键词", warning, clock.Elapsed.TotalSeconds, semanticRanks);
                }
                catch (Exception ex) when (ex is OnnxRuntimeException or InvalidDataException or FileNotFoundException)
                { embedder?.Dispose(); embedder = null; return Lexical("语义组件暂不可用，已使用关键词检索：" + ex.Message); }
                RetrievalResult Lexical(string? note) => new(lexical!.Search(query), "关键词", note, clock.Elapsed.TotalSeconds, []);
            }, ct).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    private void Refresh(IReadOnlyList<MaterialDocument>? supplied)
    {
        string signature;
        if (supplied is not null) signature = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(supplied, JsonDefaults.Options))));
        else
        {
            var root = new MaterialStore(paths).Root;
            signature = Directory.Exists(root) ? string.Join('|', Directory.EnumerateDirectories(root).Where(p => Guid.TryParse(Path.GetFileName(p), out _)).Select(p => Path.Combine(p, "document.json")).Where(File.Exists).OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => { var file = new FileInfo(p); return $"{file.FullName}:{file.Length}:{file.LastWriteTimeUtc.Ticks}"; })) : "empty";
        }
        if (signature == snapshot) return;
        chunks = DocumentChunks.Build(supplied ?? new MaterialStore(paths).List()); lexical = new KeywordIndex(chunks); snapshot = signature; CorpusBuildCount++;
    }
    private Task SaveAsync(CancellationToken ct) => VectorCache.SaveAsync(CacheFile, Identity, EmbeddingModelStore.Manifest.Dimension, chunks.Where(c => vectors!.ContainsKey(c.Id)).ToDictionary(c => c.Id, c => vectors![c.Id]), ct);
    private static double Dot(float[] left, float[] right) { double sum = 0; for (int i = 0; i < left.Length; i++) sum += (double)left[i] * right[i]; return sum; }
    /// <summary>§3.5 document-settings: Mac rebuildSemanticIndex — discard the cached vectors and
    /// rebuild the corpus on the next search (the keyword index is rebuilt immediately).</summary>
    public void RebuildIndex()
    {
        gate.Wait();
        try
        {
            embedder?.Dispose();
            embedder = null;
            vectors = null;
            fittedSnapshot = null;
            snapshot = null;
            lexical = null;
            chunks = [];
            warning = null;
            try { if (File.Exists(CacheFile)) File.Delete(CacheFile); }
            catch (IOException) { }
        }
        finally { gate.Release(); }
    }

    public void Dispose() { gate.Wait(); try { if (disposed) return; disposed = true; embedder?.Dispose(); embedder = null; } finally { gate.Release(); } }
}
