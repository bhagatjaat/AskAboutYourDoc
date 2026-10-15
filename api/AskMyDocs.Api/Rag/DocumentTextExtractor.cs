using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AskMyDocs.Api.Rag;

/// <summary>
/// RAG ka sabse pehla step: file -> plain text.
///
/// Chunking, embedding aur search sirf text samajhte hain. PDF aur Word file
/// text nahi hain - PDF compressed binary hai, .docx andar se XML files ka ZIP.
/// Isliye unhe yahan text me badalte hain, aage ki pipeline bilkul wahi rehti hai.
///
/// Paragraphs ke beech blank line ("\n\n") rakhte hain, kyunki TextChunker
/// usi par paragraph todta hai.
/// </summary>
public static class DocumentTextExtractor
{
    public static readonly IReadOnlyList<string> SupportedExtensions =
        [".txt", ".md", ".json", ".csv", ".pdf", ".docx"];

    public static async Task<string> ExtractAsync(Stream file, string fileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".doc")
        {
            throw new NotSupportedException(
                "Purani .doc file support nahi hai. Word me 'Save As' se .docx bana ke upload karo.");
        }

        if (!SupportedExtensions.Contains(extension))
        {
            throw new NotSupportedException(
                $"'{extension}' file support nahi hai. Ye chalengi: {string.Join(", ", SupportedExtensions)}");
        }

        // PDF aur ZIP (docx) parsers file me aage-peeche jaate hain,
        // isliye pehle poori file memory me le aate hain.
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        if (extension == ".pdf")
        {
            return ExtractPdf(buffer);
        }

        if (extension == ".docx")
        {
            return ExtractDocx(buffer);
        }

        using var reader = new StreamReader(buffer);
        return await reader.ReadToEndAsync(ct);
    }

    private static string ExtractPdf(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);

        var options = new ContentOrderTextExtractor.Options
        {
            SeparateParagraphsWithDoubleNewline = true,
            ReplaceWhitespaceWithSpace = true
        };

        // Page ke beech bhi blank line, taaki ek page ka aakhri aur agle page ka
        // pehla paragraph jud ke ek na ban jaayein.
        return string.Join("\n\n", pdf.GetPages().Select(page => ContentOrderTextExtractor.GetText(page, options)));
    }

    private static string ExtractDocx(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;

        if (body is null)
        {
            return string.Empty;
        }

        // Word ka har paragraph (table cells ke andar wale bhi) alag paragraph banta hai.
        return string.Join("\n\n", body
            .Descendants<Paragraph>()
            .Select(ParagraphText)
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    /// <summary>
    /// Paragraph.InnerText line break aur tab ko gira deta hai ("Line1Line2"),
    /// aur track-changes me delete hua text bhi le aata hai. Isliye khud padhte hain.
    /// </summary>
    private static string ParagraphText(Paragraph paragraph)
    {
        var sb = new StringBuilder();

        foreach (var element in paragraph.Descendants())
        {
            switch (element)
            {
                case Text text: sb.Append(text.Text); break;
                case TabChar: sb.Append('\t'); break;
                case Break or CarriageReturn: sb.Append('\n'); break;
            }
        }

        return sb.ToString();
    }
}
