using System.Text;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace AskMyDocs.Labs;

/// <summary>
/// LAB 4 - Poora RAG, 60 lines me.
///
/// Ye lab dikhata hai ki RAG koi jadoo nahi hai. Teen step:
///
///   1. RETRIEVE - sawal se milte-julte chunks nikalo
///   2. AUGMENT  - unhe prompt me context ki tarah daalo
///   3. GENERATE - LLM se bolo "sirf is context se jawab do"
///
/// Pehle hum bina RAG ke wahi sawal poochenge, taki farq dikhe.
/// </summary>
public static class Lab04_MiniRag
{
    public sealed class Chunk
    {
        [VectorStoreKey] public Guid Id { get; set; }
        [VectorStoreData] public string Text { get; set; } = "";

        [VectorStoreVector(768, DistanceFunction = DistanceFunction.CosineSimilarity)]
        public string Embedding { get; set; } = "";
    }

    // Company-specific knowledge. Ye kisi LLM ki training me nahi hai.
    private static readonly string[] KnowledgeBase =
    [
        "WorkElevate ka ITSM module P1 ticket ke liye 4 ghante ka SLA deta hai. " +
        "P2 ke liye 8 ghante, P3 ke liye 24 ghante aur P4 ke liye 72 ghante.",

        "WorkElevate me LLM provider configure karne ke liye Admin > LLM Settings " +
        "me jana hota hai. Wahan Gemini, ChatGPT aur WELLM teen options milte hain.",

        "Agar koi ticket 3 working days tak Awaiting User state me rehta hai, " +
        "system use automatically Closed kar deta hai aur user ko mail jata hai.",

        "Script Governance page par har script ka approve ya reject hota hai. " +
        "Reject karte waqt reason likhna mandatory hai."
    ];

    public static async Task RunAsync()
    {
        Console.WriteLine("=== LAB 4: Mini RAG ===\n");

        const string question = "P1 ticket ka SLA kitne ghante ka hai?";

        var chat = AiConfig.CreateChatClient();

        // ---------- Pehle: bina RAG ke ----------
        Console.WriteLine("### BINA RAG ###");
        Console.WriteLine($"Sawal: {question}\n");

        var naive = await chat.GetResponseAsync(question);
        Console.WriteLine(naive.Text);
        Console.WriteLine();
        Console.WriteLine(">> LLM ko WorkElevate ke SLA ka pata hi nahi hai. Ya to mana karega,");
        Console.WriteLine(">> ya - jo zyada khatarnak hai - confidently koi general number bata dega.");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine();

        // ---------- Ab: RAG ke saath ----------
        Console.WriteLine("### RAG KE SAATH ###\n");

        var generator = AiConfig.CreateEmbeddingGenerator();
        var collection = new InMemoryCollection<Guid, Chunk>(
            "kb", new InMemoryCollectionOptions { EmbeddingGenerator = generator });

        await collection.EnsureCollectionExistsAsync();
        await collection.UpsertAsync(KnowledgeBase.Select(t => new Chunk
        {
            Id = Guid.NewGuid(),
            Text = t,
            Embedding = t
        }));

        // --- Step 1: RETRIEVE ---
        var context = new StringBuilder();
        var marker = 1;

        Console.WriteLine("[1] RETRIEVE - vector store se nikle chunks:");
        await foreach (var hit in collection.SearchAsync(question, top: 2))
        {
            Console.WriteLine($"    [{marker}] score {hit.Score:F4} | {Truncate(hit.Record.Text, 60)}");
            context.AppendLine($"[{marker}] {hit.Record.Text}");
            marker++;
        }
        Console.WriteLine();

        // --- Step 2: AUGMENT ---
        var prompt =
            $"""
            Neeche CONTEXT diya hai. Sirf isi ke basis par jawab do.
            Har fact ke aage uska marker likho jaise [1].
            Agar jawab context me nahi hai to bolo "documents me nahi mila".

            CONTEXT:
            {context}

            SAWAL: {question}
            """;

        Console.WriteLine("[2] AUGMENT - LLM ko bheja ja raha prompt:");
        Console.WriteLine(Indent(prompt));
        Console.WriteLine();

        // --- Step 3: GENERATE ---
        Console.WriteLine("[3] GENERATE - LLM ka jawab:");
        var grounded = await chat.GetResponseAsync(
            prompt,
            new ChatOptions { Temperature = 0.1f });

        Console.WriteLine(Indent(grounded.Text));
        Console.WriteLine();

        Console.WriteLine("Seekho:");
        Console.WriteLine("  * LLM ko 'train' nahi kiya gaya - sirf sahi text prompt me daala gaya.");
        Console.WriteLine("  * Fine-tuning mehngi hai aur data badalne par dobara karni padti hai.");
        Console.WriteLine("    RAG me data badlo, index refresh karo, kaam khatam.");
        Console.WriteLine("  * Citation marker isliye maangte hain taki user verify kar sake.");
        Console.WriteLine("    Bina citation wala RAG production me bharosa nahi jeet pata.");
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    private static string Indent(string s)
        => string.Join("\n", s.Split('\n').Select(l => "    " + l));
}
