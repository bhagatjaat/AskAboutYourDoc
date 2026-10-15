using Microsoft.Extensions.VectorData;

namespace AskMyDocs.Api.Models;

/// <summary>
/// Ek document ka ek chunk. Yahi record vector DB me store hota hai.
///
/// Teen tarah ki properties hoti hain har vector record me:
///   [VectorStoreKey]    -> primary key (Qdrant me Guid ya ulong hi chalta hai)
///   [VectorStoreData]   -> normal metadata (filter/display ke liye)
///   [VectorStoreVector] -> wo property jiska embedding banega aur jis par search hogi
///
/// Dhyan do: Embedding property ka type `string` hai, `float[]` nahi.
/// Jab collection ko ek IEmbeddingGenerator diya jata hai, to library khud
/// is string ko embed karke float vector bana deti hai. Isse humein
/// "embedding banao -> store karo" wala manual step likhna hi nahi padta.
/// </summary>
public sealed class DocChunk
{
    [VectorStoreKey]
    public Guid Id { get; set; }

    /// <summary>Kis document se aaya. IsIndexed = true isliye ki hum is par filter kar sakein.</summary>
    [VectorStoreData(IsIndexed = true)]
    public string DocumentId { get; set; } = string.Empty;

    [VectorStoreData]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Document ke andar is chunk ka number (0, 1, 2...). Citation me kaam aata hai.</summary>
    [VectorStoreData]
    public int ChunkIndex { get; set; }

    /// <summary>Asli text. Yahi LLM ko context me bheja jayega.</summary>
    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 768 = Ollama ke nomic-embed-text ki dimension.
    /// Agar tum OpenAI ka text-embedding-3-small use karoge to ye 1536 karna padega,
    /// warna store "dimension mismatch" error dega.
    /// </summary>
    [VectorStoreVector(768, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Embedding { get; set; } = string.Empty;
}
