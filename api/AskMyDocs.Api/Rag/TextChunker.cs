using System.Text;

namespace AskMyDocs.Api.Rag;

/// <summary>
/// Chunking = bade document ko chhote tukdon me todna.
///
/// Ye RAG ka sabse under-rated step hai. Log sochte hain "LLM accha nahi hai",
/// 90% case me chunking kharab hoti hai.
///
/// Rules jo yahan follow kiye gaye hain:
///  1. Paragraph boundary par todo, beech-wale shabd par nahi.
///  2. Overlap rakho - taki ek sentence do chunks ke beech me kat ke gayab na ho jaye.
///  3. Bahut chhota chunk (30 char se kam) phenk do - wo noise hai.
/// </summary>
public static class TextChunker
{
    public const int DefaultMaxChars = 1200;
    public const int DefaultOverlapChars = 200;

    public static IReadOnlyList<string> Chunk(
        string text,
        int maxChars = DefaultMaxChars,
        int overlapChars = DefaultOverlapChars)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (overlapChars >= maxChars)
        {
            throw new ArgumentException("Overlap maxChars se chhota hona chahiye.", nameof(overlapChars));
        }

        // Step 1: paragraph par todo (blank line = paragraph break).
        var paragraphs = text
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var para in paragraphs)
        {
            // Ek hi paragraph agar limit se bada hai, use sentence-wise todo.
            if (para.Length > maxChars)
            {
                FlushInto(chunks, current);
                foreach (var piece in SplitLongParagraph(para, maxChars))
                {
                    chunks.Add(piece);
                }
                continue;
            }

            // Agar add karne se limit cross ho rahi hai, pehle current chunk band karo.
            if (current.Length + para.Length + 2 > maxChars && current.Length > 0)
            {
                var finished = current.ToString().Trim();
                chunks.Add(finished);

                // Overlap: pichle chunk ka aakhri hissa naye chunk me carry forward.
                current.Clear();
                current.Append(TakeTail(finished, overlapChars));
                current.Append("\n\n");
            }

            current.Append(para).Append("\n\n");
        }

        FlushInto(chunks, current);

        return chunks
            .Select(c => c.Trim())
            .Where(c => c.Length >= 30)
            .ToList();
    }

    private static void FlushInto(List<string> chunks, StringBuilder buffer)
    {
        if (buffer.Length > 0)
        {
            chunks.Add(buffer.ToString().Trim());
            buffer.Clear();
        }
    }

    private static string TakeTail(string s, int count)
        => s.Length <= count ? s : s[^count..];

    private static IEnumerable<string> SplitLongParagraph(string para, int maxChars)
    {
        var sentences = para.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var buffer = new StringBuilder();

        foreach (var raw in sentences)
        {
            var sentence = raw.Trim();
            if (sentence.Length == 0)
            {
                continue;
            }

            if (buffer.Length + sentence.Length + 2 > maxChars && buffer.Length > 0)
            {
                yield return buffer.ToString().Trim();
                buffer.Clear();
            }

            buffer.Append(sentence).Append(". ");
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString().Trim();
        }
    }
}
