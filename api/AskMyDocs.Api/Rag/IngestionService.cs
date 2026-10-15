using AskMyDocs.Api.Data;
using AskMyDocs.Api.Models;
using Microsoft.Extensions.VectorData;

namespace AskMyDocs.Api.Rag;

/// <summary>
/// RAG ka "Indexing" phase. Ye offline/background kaam hai, user ke sawal se pehle hota hai.
///
///   text -> chunks -> embeddings -> vector store
///
/// Dhyan do ki yahan humne khud embedding generate nahi ki. Collection ko
/// Program.cs me EmbeddingGenerator diya gaya hai, isliye UpsertAsync ke andar
/// hi library DocChunk.Embedding (string) ko float vector me badal deti hai.
/// </summary>
public sealed class IngestionService(
    VectorStoreCollection<Guid, DocChunk> collection,
    IDocumentCatalog catalog,
    ILogger<IngestionService> logger)
{
    public async Task<IngestResponse> IngestAsync(IngestRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Content);

        await collection.EnsureCollectionExistsAsync(ct);

        var documentId = Guid.NewGuid().ToString("N");
        var pieces = TextChunker.Chunk(request.Content);

        if (pieces.Count == 0)
        {
            throw new InvalidOperationException("Is document se koi usable chunk nahi bana.");
        }

        var records = pieces.Select((text, index) => new DocChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            FileName = request.FileName,
            ChunkIndex = index,
            Text = text,
            Embedding = text   // <- isi string ka embedding banega
        });

        // Batch upsert. Ek-ek karke bhejna slow bhi hai aur mehnga bhi
        // (har call par embedding API ka round trip lagta hai).
        await collection.UpsertAsync(records, ct);

        var summary = new DocumentSummary(documentId, request.FileName, pieces.Count, DateTimeOffset.UtcNow);
        await catalog.AddAsync(summary, ct);

        logger.LogInformation(
            "Ingested {FileName} as {DocumentId} with {ChunkCount} chunks",
            request.FileName, documentId, pieces.Count);

        return new IngestResponse(documentId, request.FileName, pieces.Count);
    }

    public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct = default)
        => catalog.ListAsync(ct);
}
