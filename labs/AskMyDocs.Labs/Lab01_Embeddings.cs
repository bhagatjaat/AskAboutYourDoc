using Microsoft.Extensions.AI;

namespace AskMyDocs.Labs;

/// <summary>
/// LAB 1 - Embedding aakhir hai kya?
///
/// Ek embedding = text ka numeric fingerprint (float ki ek lambi list).
/// Khaas baat: jin do texts ka matlab milta-julta hai, unke vectors bhi
/// aas-paas hote hain - bhale shabd alag ho.
///
/// Isi ek property par poora RAG tika hua hai.
/// </summary>
public static class Lab01_Embeddings
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== LAB 1: Embeddings aur similarity ===\n");

        var generator = AiConfig.CreateEmbeddingGenerator();

        // ---------- Part A: ek vector dekho ----------
        var vector = await generator.GenerateVectorAsync("Server down ho gaya hai");

        Console.WriteLine($"Model      : {AiConfig.EmbeddingModel}");
        Console.WriteLine($"Dimensions : {vector.Length}");
        Console.WriteLine($"Pehle 8     : [{string.Join(", ", vector.ToArray().Take(8).Select(f => f.ToString("F4")))}, ...]");
        Console.WriteLine();
        Console.WriteLine($"Dhyan do: ye {vector.Length} numbers hi 'matlab' hain. Inme se");
        Console.WriteLine("kisi ek number ka apne aap me koi meaning nahi hai.\n");

        // ---------- Part B: similarity compare karo ----------
        const string query = "Mera laptop start nahi ho raha";

        string[] candidates =
        [
            "Laptop boot nahi ho raha, screen kaali hai",     // alag shabd, same matlab
            "System power on hone par hang ho jata hai",      // thoda related
            "Printer me paper jam ho gaya hai",               // IT hai par alag issue
            "Aaj Delhi me barish ho rahi hai"                 // bilkul unrelated
        ];

        var queryVector = await generator.GenerateVectorAsync(query);

        Console.WriteLine($"QUERY: \"{query}\"\n");
        Console.WriteLine($"{"Score",-8} Text");
        Console.WriteLine(new string('-', 70));

        var scored = new List<(double Score, string Text)>();

        foreach (var candidate in candidates)
        {
            var candidateVector = await generator.GenerateVectorAsync(candidate);
            var score = AiConfig.CosineSimilarity(queryVector.Span, candidateVector.Span);
            scored.Add((score, candidate));
        }

        foreach (var (score, text) in scored.OrderByDescending(s => s.Score))
        {
            Console.WriteLine($"{score,-8:F4} {text}");
        }

        Console.WriteLine();
        Console.WriteLine("Seekho:");
        Console.WriteLine("  * Pehla candidate top par aaya jabki usme 'laptop start' shabd hai hi nahi.");
        Console.WriteLine("    SQL ka LIKE '%laptop start%' ye kabhi nahi kar sakta.");
        Console.WriteLine("  * Barish wali line sabse neeche - matlab search semantic hai, keyword nahi.");
        Console.WriteLine("  * Yahi 'semantic search' hai, aur RAG ka R isi par khada hai.");
    }
}
