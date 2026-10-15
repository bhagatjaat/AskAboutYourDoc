using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AskMyDocs.Api.Agents;

/// <summary>
/// Agent banane aur uski session (memory) sambhalne wali jagah.
///
/// Agent = LLM + instructions + tools + memory.
/// Chat sirf LLM hai. Agent khud decide karta hai ki kaunsa tool, kitni baar chalana hai.
/// </summary>
public sealed class SupportAgentFactory(IChatClient chatClient, SupportTools tools)
{
    private const string Instructions =
        """
        Tum "AskMyDocs" naam ki IT support assistant ho.

        Kaam karne ka tarika:
        - Policy / process / product se juda koi bhi sawal aaye to pehle search_documents chalao.
          Apni yaadasht se mat bolo.
        - Ticket ke baare me pooche to get_ticket_status chalao.
        - Naya ticket tabhi banao jab user saaf saaf bole. Banane se pehle
          title aur priority confirm karo.
        - Jawab me jis file se info aayi uska naam likho.
        - Agar documents me jawab nahi hai to seedha bol do ki nahi mila.

        Chhota, seedha aur polite jawab do.
        """;

    // Session = ek user ki chat ki memory. Yahan in-memory dictionary hai.
    // Production me ise Redis/SQL me rakhte hain (warna app restart par sab chat gayab).
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    public AIAgent CreateAgent() =>
        new ChatClientAgent(
            chatClient,
            instructions: Instructions,
            name: "AskMyDocsAgent",
            description: "Documents aur ITSM tickets par kaam karne wala support agent",
            tools: tools.AsTools());

    /// <summary>Purani session wapas do, ya nayi bana do.</summary>
    public async Task<(AgentSession Session, string SessionId)> GetOrCreateSessionAsync(
        AIAgent agent, string? sessionId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(sessionId) && _sessions.TryGetValue(sessionId, out var existing))
        {
            return (existing, sessionId);
        }

        var session = await agent.CreateSessionAsync(ct);
        var id = sessionId ?? Guid.NewGuid().ToString("N");
        _sessions[id] = session;
        return (session, id);
    }
}
