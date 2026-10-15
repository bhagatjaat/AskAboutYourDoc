namespace AskMyDocs.Labs;

/// <summary>
/// LAB 3 - Chunking.
///
/// Ye lab bilkul free hai - koi API call nahi. Par RAG ki quality ka
/// 70% faisla yahin hota hai.
///
/// Kyun chunk karte hain?
///  1. Embedding model ki input limit hoti hai (8k tokens).
///  2. Poore 50-page document ka ek vector banaoge to wo "average matlab"
///     ban jayega - kisi bhi specific sawal se match nahi karega.
///  3. LLM ko sirf relevant 2-3 paragraph bhejne se cost aur hallucination
///     dono kam hote hain.
/// </summary>
public static class Lab03_Chunking
{
    private const string SampleDoc =
        """
        Leave Policy - WorkElevate

        Har employee ko saal me 24 paid leaves milti hain. Ye 1 January se
        31 December ke beech use karni hoti hain. Bachi hui leaves me se
        maximum 10 agle saal carry forward hoti hain.

        Sick Leave

        Sick leave alag se 7 din ki hoti hai. 2 din se zyada sick leave
        lene par medical certificate dena zaroori hai. Sick leave carry
        forward nahi hoti.

        Leave Apply Karne Ka Tarika

        HRMS portal par jao, My Leaves par click karo, type select karo,
        date range do aur Submit dabao. Manager ko email chala jayega.
        Approval ke baad hi leave confirm maani jayegi.

        Leave Rejection

        Agar manager 3 working days me action nahi leta, request
        automatically HR ke paas escalate ho jaati hai.
        """;

    public static Task RunAsync()
    {
        Console.WriteLine("=== LAB 3: Chunking ===\n");

        // Teen alag settings - farq khud dekho.
        ShowChunks("Bahut chhota (200 chars, 0 overlap)", SampleDoc, 200, 0);
        ShowChunks("Theek (600 chars, 100 overlap)", SampleDoc, 600, 100);
        ShowChunks("Bahut bada (3000 chars, 200 overlap)", SampleDoc, 3000, 200);

        Console.WriteLine("Seekho:");
        Console.WriteLine("  * Bahut chhota  -> context toot jata hai, 'maximum 10' kis cheez ke");
        Console.WriteLine("                     baare me hai ye pata hi nahi chalta.");
        Console.WriteLine("  * Bahut bada    -> ek hi chunk me 4 topics, embedding confuse ho jata hai,");
        Console.WriteLine("                     aur LLM ko bekaar text bhejkar paisa barbaad hota hai.");
        Console.WriteLine("  * Sweet spot    -> 500-1500 characters, 10-20% overlap. Phir tune karo.");
        Console.WriteLine();
        Console.WriteLine("  Overlap kyun? Taki heading aur uska content do alag chunks me");
        Console.WriteLine("  na bant jayein. Overlap 0 rakhke upar ka output dobara dekho.");

        return Task.CompletedTask;
    }

    private static void ShowChunks(string label, string doc, int maxChars, int overlap)
    {
        Console.WriteLine($"--- {label} ---");

        var chunks = Chunk(doc, maxChars, overlap);
        Console.WriteLine($"Total chunks: {chunks.Count}\n");

        for (var i = 0; i < chunks.Count; i++)
        {
            var preview = chunks[i].Replace("\n", " ");
            if (preview.Length > 90)
            {
                preview = preview[..90] + "...";
            }

            Console.WriteLine($"  [{i}] ({chunks[i].Length,4} ch) {preview}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Wahi chunker jo API project me hai - yahan copy isliye ki lab
    /// standalone chale aur tum isme tod-fod kar sako.
    /// </summary>
    public static List<string> Chunk(string text, int maxChars, int overlapChars)
    {
        var paragraphs = text.Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var chunks = new List<string>();
        var current = "";

        foreach (var para in paragraphs)
        {
            if (current.Length + para.Length + 2 > maxChars && current.Length > 0)
            {
                chunks.Add(current.Trim());
                current = overlapChars > 0 && current.Length > overlapChars
                    ? current[^overlapChars..] + "\n\n"
                    : "";
            }

            current += para + "\n\n";
        }

        if (current.Trim().Length > 0)
        {
            chunks.Add(current.Trim());
        }

        return chunks.Where(c => c.Length >= 30).ToList();
    }
}
