using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace AskMyDocs.Labs;

/// <summary>
/// Saare labs ke liye ek hi jagah se LLM client banate hain.
///
/// Endpoint config se aata hai, isliye wahi code
///   - OpenAI          (https://api.openai.com/v1)
///   - Azure OpenAI    (https://&lt;resource&gt;.openai.azure.com/openai/v1)
///   - Local Ollama    (http://localhost:11434/v1)
/// teeno ke saath chalta hai. Ek bhi line badalne ki zaroorat nahi.
/// </summary>
public static class AiConfig
{
    private static readonly IConfigurationRoot Config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    public static string ChatModel => Config["Ai:ChatModel"] ?? "gpt-4o-mini";
    public static string EmbeddingModel => Config["Ai:EmbeddingModel"] ?? "text-embedding-3-small";

    public static OpenAIClient CreateOpenAIClient()
    {
        var apiKey = Config["Ai:ApiKey"];
        var endpoint = Config["Ai:Endpoint"] ?? "https://api.openai.com/v1";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Ollama ko key chahiye hi nahi, par client ko kuch na kuch dena padta hai.
            apiKey = "ollama";
        }

        return new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
    }

    public static IChatClient CreateChatClient() =>
        CreateOpenAIClient()
            .GetChatClient(ChatModel)
            .AsIChatClient();

    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator() =>
        CreateOpenAIClient()
            .GetEmbeddingClient(EmbeddingModel)
            .AsIEmbeddingGenerator();

    /// <summary>
    /// Cosine similarity haath se. Ye formula hi poore vector search ka dil hai:
    ///
    ///     cos(A, B) = (A . B) / (|A| * |B|)
    ///
    /// Matlab: dono vectors ke beech ka angle. 1 = bilkul same direction,
    /// 0 = bilkul alag (90 degree), -1 = ulta. Length se farq nahi padta,
    /// sirf direction se - isliye chhota aur bada text compare ho jaata hai.
    /// </summary>
    public static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException($"Dimension mismatch: {a.Length} vs {b.Length}");
        }

        double dot = 0, magA = 0, magB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
