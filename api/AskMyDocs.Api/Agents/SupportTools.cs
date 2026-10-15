using System.ComponentModel;
using System.Text;
using AskMyDocs.Api.Rag;
using Microsoft.Extensions.AI;

namespace AskMyDocs.Api.Agents;

/// <summary>
/// Agent ke "haath-pair" = tools.
///
/// Sabse zaroori baat: LLM tumhara C# method ka naam aur [Description] padhkar hi
/// decide karta hai ki kab use call karna hai. Description hi tumhara real prompt hai.
/// Kharab description = agent galat tool uthayega.
///
/// Ye tools WorkElevate jaise ITSM domain par bane hain taki concept jaldi baithe -
/// ticket status, ticket create, aur documents me search.
/// </summary>
public sealed class SupportTools(RagService rag)
{
    // Demo data. Asli project me ye tumhari SQL repository hoti.
    private static readonly Dictionary<string, (string Status, string Owner, string Priority)> Tickets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INC-1001"] = ("In Progress", "Rahul Verma", "P2"),
        ["INC-1002"] = ("Resolved", "Priya Nair", "P3"),
        ["INC-1003"] = ("Awaiting User", "Bhagat Singh", "P1")
    };

    /// <summary>
    /// RAG ko ek tool bana diya. Ab agent khud decide karta hai ki documents
    /// dekhne ki zaroorat hai ya nahi - har sawal par blindly search nahi hoti.
    /// Isi ko "agentic RAG" kehte hain.
    /// </summary>
    [Description("Uploaded company documents me se relevant text dhundta hai. Policy, process ya product se juda koi bhi sawal ho to ye use karo.")]
    public async Task<string> SearchDocumentsAsync(
        [Description("Dhundne ke liye search query, user ke shabdon me")] string query,
        CancellationToken cancellationToken = default)
    {
        var hits = await rag.RetrieveAsync(query, 4, cancellationToken);

        if (hits.Count == 0)
        {
            return "Documents me kuch relevant nahi mila.";
        }

        var sb = new StringBuilder();
        foreach (var hit in hits)
        {
            sb.AppendLine($"--- {hit.Record.FileName} (chunk {hit.Record.ChunkIndex}, score {hit.Score:F3}) ---");
            sb.AppendLine(hit.Record.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    [Description("Ek ITSM ticket ka current status, owner aur priority batata hai.")]
    public string GetTicketStatus(
        [Description("Ticket number, jaise INC-1001")] string ticketId)
    {
        if (Tickets.TryGetValue(ticketId, out var t))
        {
            return $"Ticket {ticketId}: status={t.Status}, owner={t.Owner}, priority={t.Priority}";
        }

        // Tool ka error bhi natural language me wapas bhejo - LLM isse recover kar leta hai.
        return $"Ticket {ticketId} nahi mila. Available tickets: {string.Join(", ", Tickets.Keys)}";
    }

    [Description("Naya ITSM ticket banata hai aur ticket number return karta hai.")]
    public string CreateTicket(
        [Description("Ticket ka chhota sa title")] string title,
        [Description("Priority: P1, P2, P3 ya P4")] string priority = "P3")
    {
        var id = $"INC-{Random.Shared.Next(2000, 9999)}";
        Tickets[id] = ("New", "Unassigned", priority);
        return $"Ticket {id} ban gaya (priority {priority}): {title}";
    }

    /// <summary>
    /// C# methods ko AIFunction me badalna. AIFunctionFactory reflection se
    /// JSON schema khud bana leta hai - tumhe schema haath se likhne ki zaroorat nahi.
    /// </summary>
    public IList<AITool> AsTools() =>
    [
        AIFunctionFactory.Create(SearchDocumentsAsync, name: "search_documents"),
        AIFunctionFactory.Create(GetTicketStatus, name: "get_ticket_status"),
        AIFunctionFactory.Create(CreateTicket, name: "create_ticket")
    ];
}
