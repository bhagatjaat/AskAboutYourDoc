using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.VectorData;

namespace AskMyDocs.Labs;

/// <summary>
/// LAB 2 - Vector database.
///
/// Lab 1 me humne har candidate ka embedding banakar khud compare kiya.
/// 4 lines ke liye theek hai. 4 lakh lines ke liye? Har query par 4 lakh
/// cosine calculate karna pagalpan hai.
///
/// Vector DB yahi problem solve karta hai: wo vectors ko aise index karta hai
/// (HNSW, IVF) ki "nearest" dhundhne me poori list scan na karni pade.
///
/// Yahan InMemory store use kar rahe hain - seekhne ke liye perfect,
/// kyunki API bilkul wahi hai jo Qdrant/Azure AI Search ki hai.
/// </summary>
public static class Lab02_VectorStore
{
    /// <summary>Ek chhota record - ek FAQ entry.</summary>
    public sealed class Faq
    {
        [VectorStoreKey]
        public Guid Id { get; set; }

        [VectorStoreData(IsIndexed = true)]
        public string Category { get; set; } = "";

        [VectorStoreData]
        public string Question { get; set; } = "";

        [VectorStoreData]
        public string Answer { get; set; } = "";

        // Is string ka embedding banega aur isi par search hogi.
        [VectorStoreVector(768, DistanceFunction = DistanceFunction.CosineSimilarity)]
        public string Embedding { get; set; } = "";
    }

    public static async Task RunAsync()
    {
        Console.WriteLine("=== LAB 2: Vector store me store aur search ===\n");

        var generator = AiConfig.CreateEmbeddingGenerator();

        // Collection ko generator diya -> ab wo khud string ko vector banayega.
        var collection = new InMemoryCollection<Guid, Faq>(
            "faqs",
            new InMemoryCollectionOptions { EmbeddingGenerator = generator });

        await collection.EnsureCollectionExistsAsync();

        (string Category, string Question, string Answer)[] seed =
        [
            ("Access", "VPN connect nahi ho raha",
                "VPN client restart karo, phir IT se naya certificate maango."),
            ("Access", "Password reset kaise karein",
                "Self-service portal par jao, Forgot Password par click karo, OTP email par aayega."),
            ("Hardware", "Laptop bahut slow chal raha hai",
                "Startup apps band karo, RAM upgrade ke liye INC raise karo."),
            ("Hardware", "Printer offline dikha raha hai",
                "Printer ko power cycle karo aur print spooler service restart karo."),
            ("Leave", "Leave balance kahan dekhein",
                "HRMS portal > My Profile > Leave Balance me dikhta hai."),
        ];

        var records = seed.Select(s => new Faq
        {
            Id = Guid.NewGuid(),
            Category = s.Category,
            Question = s.Question,
            Answer = s.Answer,
            Embedding = $"{s.Question} {s.Answer}"   // question + answer dono embed karo
        });

        await collection.UpsertAsync(records);
        Console.WriteLine($"{seed.Length} FAQ store ho gaye.\n");

        // ---------- Search 1: plain semantic search ----------
        // Sawal English me, data Hinglish me - phir bhi match hona chahiye.
        const string query = "I forgot my login credentials, how do I recover access?";
        Console.WriteLine($"QUERY: \"{query}\"  (English me, jabki data Hinglish me hai)\n");

        await foreach (var hit in collection.SearchAsync(query, top: 3))
        {
            Console.WriteLine($"  [{hit.Score:F4}] ({hit.Record.Category}) {hit.Record.Question}");
            Console.WriteLine($"           -> {hit.Record.Answer}");
        }

        Console.WriteLine();
        Console.WriteLine("Dekha? Query me 'password' shabd hai hi nahi - 'login credentials' hai.");
        Console.WriteLine("Phir bhi password reset wala FAQ top par aaya. Keyword search yahan fail hoti.\n");

        // ---------- Search 2: filter ke saath ----------
        Console.WriteLine("QUERY: \"device dheema hai\" + filter Category == 'Hardware'\n");

        var options = new VectorSearchOptions<Faq>
        {
            Filter = f => f.Category == "Hardware"
        };

        await foreach (var hit in collection.SearchAsync("device dheema hai", top: 2, options))
        {
            Console.WriteLine($"  [{hit.Score:F4}] ({hit.Record.Category}) {hit.Record.Question}");
        }

        Console.WriteLine();
        Console.WriteLine("Seekho:");
        Console.WriteLine("  * Filter + vector search ek saath - isse 'hybrid' kaam nikalta hai.");
        Console.WriteLine("    Production me ye zaroori hai: tenant id, department, date range...");
        Console.WriteLine("  * InMemoryCollection ko QdrantCollection se badlo - baaki code same.");
        Console.WriteLine("    Yahi Microsoft.Extensions.VectorData ka poora point hai.");
    }
}
