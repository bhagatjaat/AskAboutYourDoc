using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
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

    /// <summary>
    /// ~10 lakh characters (300-400 page ki kitaab). Isse bade document ke saare
    /// chunks ek hi batch me embed karne me itna time lagta hai ki request timeout ho jaati hai.
    /// </summary>
    public const int MaxChars = 1_000_000;

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
            return ExtractPdf(buffer, ct);
        }

        if (extension == ".docx")
        {
            return ExtractDocx(buffer, ct);
        }

        using var reader = new StreamReader(buffer);
        var text = await reader.ReadToEndAsync(ct);
        return text.Length > MaxChars ? throw TooBig() : text;
    }

    // ------------------------------------------------------------------ PDF

    private static string ExtractPdf(Stream stream, CancellationToken ct)
    {
        // SkipMissingFonts: kisi ek page ka font toota ho to poori PDF reject na ho,
        // bas us font ke letters chhoot jaayein.
        using var pdf = PdfDocument.Open(stream, new ParsingOptions { SkipMissingFonts = true });

        var options = new ContentOrderTextExtractor.Options
        {
            SeparateParagraphsWithDoubleNewline = true,
            ReplaceWhitespaceWithSpace = true
        };

        var pages = new List<string>();
        var total = 0;

        foreach (var page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var text = ContentOrderTextExtractor.GetText(page, options).ReplaceLineEndings("\n");

            // PDF me har line page ki chaudai par wrap hoke "\n" se khatam hoti hai.
            // Use space bana do, sirf paragraph break ("\n\n") rehne do - warna chunker
            // har line ko alag sentence samajh ke beech me "." daal deta hai.
            text = Regex.Replace(text, @"(?<!\n)\n(?!\n)", " ");

            // NFKC "ﬁ" jaise ligature ko "fi" bana deta hai, warna "ﬁle" search me
            // "file" se match nahi hota.
            text = text.Normalize(NormalizationForm.FormKC);

            total += text.Length;
            if (total > MaxChars)
            {
                throw TooBig();
            }

            pages.Add(text);
        }

        // Page ke beech bhi blank line, taaki ek page ka aakhri aur agle page ka
        // pehla paragraph jud ke ek na ban jaayein.
        return string.Join("\n\n", pages);
    }

    // ----------------------------------------------------------------- DOCX

    private const string WordNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string WordStrictNs = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    private const string MarkupCompatibilityNs = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private static readonly XmlReaderSettings DocxXmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        // Chhoti si ZIP andar se GBs ki XML me na khul jaaye (zip bomb).
        MaxCharactersInDocument = 50_000_000
    };

    private static string ExtractDocx(Stream stream, CancellationToken ct)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var body = zip.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("Ye sahi .docx nahi lag rahi - andar word/document.xml nahi mila.");

        var paragraphs = new List<string>();
        var total = 0;

        // Footnotes/endnotes me aksar kaam ki baat hoti hai. Header/footer (company
        // naam, page number) har page par repeat hote hain, wo jaan-boojh ke chhode hain.
        foreach (var part in new[] { body, zip.GetEntry("word/footnotes.xml"), zip.GetEntry("word/endnotes.xml") })
        {
            if (part is not null)
            {
                ReadParagraphs(part, paragraphs, ref total, ct);
            }
        }

        return string.Join("\n\n", paragraphs);
    }

    /// <summary>
    /// Word ki XML ko XmlReader se node-by-node padhte hain, poora DOM nahi banate.
    /// DOM banane wali library (OpenXml SDK) jaan-boojh ke bahut gehri nesting wali
    /// 2 KB ki file par stack overflow se poora API process gira deti thi.
    /// </summary>
    private static void ReadParagraphs(ZipArchiveEntry part, List<string> paragraphs, ref int total, CancellationToken ct)
    {
        using var xml = XmlReader.Create(part.Open(), DocxXmlSettings);

        // Text box ka paragraph bahar wale paragraph ke andar khulta hai. Har khule
        // <w:p> ka apna buffer rakhte hain, warna dono ka text aapas me chipak jaata.
        var open = new Stack<StringBuilder>();
        var inText = false;

        xml.Read();
        while (!xml.EOF)
        {
            if (xml.NodeType == XmlNodeType.Element && IsSkipped(xml))
            {
                xml.Skip();   // poora hissa chhodo; reader khud agle node par aa jaata hai
                continue;
            }

            var current = open.Count > 0 ? open.Peek() : null;

            switch (xml.NodeType)
            {
                case XmlNodeType.Element when IsWord(xml):
                    switch (xml.LocalName)
                    {
                        case "p" when !xml.IsEmptyElement: open.Push(new StringBuilder()); break;
                        case "t": inText = !xml.IsEmptyElement; break;
                        case "tab" or "ptab": current?.Append('\t'); break;
                        case "br" or "cr": current?.Append('\n'); break;
                        case "noBreakHyphen": current?.Append('-'); break;
                    }
                    break;

                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace
                    when inText:
                    current?.Append(xml.Value);
                    break;

                case XmlNodeType.EndElement when IsWord(xml) && xml.LocalName == "t":
                    inText = false;
                    break;

                case XmlNodeType.EndElement when IsWord(xml) && xml.LocalName == "p" && open.Count > 0:
                    var text = open.Pop().ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        total += text.Length;
                        if (total > MaxChars)
                        {
                            throw TooBig();
                        }

                        paragraphs.Add(text);
                    }

                    ct.ThrowIfCancellationRequested();
                    break;
            }

            xml.Read();
        }
    }

    // mc:Fallback = usi text box ki purani (VML) copy - Choice wali copy pehle hi padh li.
    // w:del / w:moveFrom = track changes me delete hua, ya purani jagah se move hua text.
    private static bool IsSkipped(XmlReader xml) =>
        (xml.NamespaceURI == MarkupCompatibilityNs && xml.LocalName == "Fallback")
        || (IsWord(xml) && xml.LocalName is "del" or "moveFrom");

    private static bool IsWord(XmlReader xml) => xml.NamespaceURI is WordNs or WordStrictNs;

    private static InvalidDataException TooBig() => new(
        $"Document bahut bada hai - ek baar me {MaxChars:N0} characters tak hi index karte hain. " +
        "File ko chhote hisson me tod ke upload karo.");
}
