using System.Text;
using AskMyDocs.Api.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace AskMyDocs.Api.Rag;

/// <summary>
/// RAG ka "Query" phase - teen step:
///
///   R - Retrieve : sawal ka embedding banao, vector store me nearest chunks dhundo
///   A - Augment  : un chunks ko prompt me context ki tarah chipka do
///   G - Generate : LLM se sirf us context ke basis par jawab likhwao
///
/// Poora RAG yahi hai. Baaki sab tuning hai.
/// </summary>
public sealed class RagService(
    VectorStoreCollection<Guid, DocChunk> collection,
    IChatClient chatClient,
    ILogger<RagService> logger)
{
    private const int TopK = 4;

    /// <summary>
    /// Score threshold. Cosine similarity me 1.0 = bilkul same, 0 = bilkul alag.
    /// Isse neeche wale chunks hum ignore kar dete hain - warna LLM ko kachra
    /// context milta hai aur wo confidently galat jawab deta hai.
    /// </summary>
    private const double MinScore = 0.25;

    private const string SystemPrompt =
        """
        Tum ek document assistant ho. Tumhe neeche CONTEXT diya gaya hai.

        Niyam:
        1. Jawab SIRF diye gaye CONTEXT se dena hai. Apni general knowledge mat lagao.
        2. Har claim ke aage uska source marker lagao, jaise [1] ya [2].
        3. Agar CONTEXT me jawab nahi hai to saaf saaf likho:
           "Diye gaye documents me iska jawab nahi mila."
           Andaza bilkul mat lagao.
        4. Jawab chhota aur seedha rakho.
        """;

    public async Task<AskResponse> AskAsync(string question, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        // ---------- 1. RETRIEVE ----------
        var hits = await RetrieveAsync(question, TopK, ct);

        if (hits.Count == 0)
        {
            return new AskResponse(
                "Diye gaye documents me iska jawab nahi mila. Pehle koi document upload karo.",
                [], 0);
        }

        // ---------- 2. AUGMENT ----------
        var (contextBlock, sources) = BuildContext(hits);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, $"CONTEXT:\n{contextBlock}\n\nSAWAL: {question}")
        };

        // ---------- 3. GENERATE ----------
        // Temperature 0.1 - factual Q&A me creativity dushman hai.
        var response = await chatClient.GetResponseAsync(
            messages,
            new ChatOptions { Temperature = 0.1f, MaxOutputTokens = 800 },
            ct);

        logger.LogInformation("RAG answered with {SourceCount} sources", sources.Count);

        return new AskResponse(response.Text, sources, hits.Count);
    }

    /// <summary>
    /// Sirf retrieval - bina LLM ke. Agent isi ko ek tool ki tarah call karta hai,
    /// aur debugging ke liye bhi ye alag se bahut kaam aata hai.
    /// </summary>
    public async Task<IReadOnlyList<VectorSearchResult<DocChunk>>> RetrieveAsync(
        string query, int top, CancellationToken ct = default)
    {
        await collection.EnsureCollectionExistsAsync(ct);

        var results = new List<VectorSearchResult<DocChunk>>();

        // SearchAsync<string> - string directly de sakte hain kyunki collection ke paas
        // embedding generator hai. Wo khud query ka embedding banayega.
        await foreach (var hit in collection.SearchAsync(query, top, cancellationToken: ct))
        {
            if (hit.Score is null || hit.Score >= MinScore)
            {
                results.Add(hit);
            }
        }

        return results;
    }

    private static (string Context, List<SourceRef> Sources) BuildContext(
        IReadOnlyList<VectorSearchResult<DocChunk>> hits)
    {
        var sb = new StringBuilder();
        var sources = new List<SourceRef>();

        for (var i = 0; i < hits.Count; i++)
        {
            var record = hits[i].Record;
            var marker = i + 1;

            sb.AppendLine($"[{marker}] (file: {record.FileName}, chunk: {record.ChunkIndex})");
            sb.AppendLine(record.Text);
            sb.AppendLine();

            sources.Add(new SourceRef(
                marker,
                record.FileName,
                record.ChunkIndex,
                Math.Round(hits[i].Score ?? 0, 4),
                Snippet(record.Text)));
        }

        return (sb.ToString(), sources);
    }

    private static string Snippet(string text)
        => text.Length <= 180 ? text : text[..180] + "...";
}
