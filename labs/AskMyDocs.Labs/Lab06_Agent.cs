using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AskMyDocs.Labs;

/// <summary>
/// LAB 6 - Microsoft Agent Framework.
///
/// Chat vs Agent ka farq:
///
///   Chat  : tum message bhejte ho, text wapas aata hai. Bas.
///   Agent : LLM + instructions + tools + memory. Wo khud decide karta hai
///           ki kya karna hai, kitne step me karna hai, kab rukna hai.
///
/// Teen cheezein is lab me dekhoge:
///   A. Simple agent (sirf instructions)
///   B. Tools wala agent
///   C. Session = memory, aur usko save/restore karna
/// </summary>
public static class Lab06_Agent
{
    [Description("Kisi sheher ka aaj ka mausam batata hai")]
    private static string GetWeather(
        [Description("Sheher ka naam")] string city)
    {
        Console.WriteLine($"   >> [tool chala] GetWeather({city})");
        return $"{city} me abhi 31 degree, halki dhoop.";
    }

    [Description("Do number jodta hai")]
    private static double Add(double a, double b)
    {
        Console.WriteLine($"   >> [tool chala] Add({a}, {b})");
        return a + b;
    }

    [Description("Employee ka leave balance batata hai")]
    private static int GetLeaveBalance(
        [Description("Employee ka naam")] string employee)
    {
        Console.WriteLine($"   >> [tool chala] GetLeaveBalance({employee})");
        return employee.Length % 10 + 5;   // demo data
    }

    public static async Task RunAsync()
    {
        Console.WriteLine("=== LAB 6: Microsoft Agent Framework ===\n");

        var chatClient = AiConfig.CreateChatClient();

        // ---------- A. Sabse simple agent ----------
        Console.WriteLine("--- A. Simple agent (sirf instructions) ---\n");

        AIAgent simple = new ChatClientAgent(
            chatClient,
            instructions: "Tum ek seedha-saada assistant ho. Jawab 2 line se bada mat do.",
            name: "SimpleAgent");

        var reply = await simple.RunAsync("RAG ko ek line me samjhao.");
        Console.WriteLine($"Agent: {reply.Text}\n");

        // ---------- B. Tools wala agent ----------
        Console.WriteLine("--- B. Tools wala agent ---\n");

        AIAgent toolAgent = new ChatClientAgent(
            chatClient,
            instructions:
                """
                Tum ek office assistant ho.
                Jahan tool available hai wahan apni yaadasht par bharosa mat karo,
                tool chalao. Jawab chhota do.
                """,
            name: "OfficeAgent",
            description: "Mausam, calculation aur leave balance sambhalta hai",
            tools:
            [
                AIFunctionFactory.Create(GetWeather),
                AIFunctionFactory.Create(Add),
                AIFunctionFactory.Create(GetLeaveBalance)
            ]);

        Console.WriteLine("User: Bhagat ka leave balance kitna hai, aur usme 3 aur jod do?\n");

        var toolReply = await toolAgent.RunAsync(
            "Bhagat ka leave balance kitna hai, aur usme 3 aur jod do?");

        Console.WriteLine($"\nAgent: {toolReply.Text}\n");
        Console.WriteLine("Dhyan do: ek hi sawal me do tool chale, aur doosre tool ka input");
        Console.WriteLine("pehle tool ke output se aaya. Ye 'multi-step reasoning' hai.\n");

        // ---------- C. Session = memory ----------
        Console.WriteLine("--- C. Session (memory) ---\n");

        var session = await toolAgent.CreateSessionAsync();

        var r1 = await toolAgent.RunAsync("Mera naam Bhagat hai. Gwalior ka mausam batao.", session);
        Console.WriteLine($"Turn 1 -> {r1.Text}\n");

        // Agla sawal me na naam hai na sheher - sab session se aayega.
        var r2 = await toolAgent.RunAsync("Mera naam kya tha? Aur wahan kitni degree thi?", session);
        Console.WriteLine($"Turn 2 -> {r2.Text}\n");

        Console.WriteLine("Bina session ke wahi sawal:");
        var r3 = await toolAgent.RunAsync("Mera naam kya tha?");
        Console.WriteLine($"         -> {r3.Text}\n");

        // ---------- Session ko save/restore ----------
        var saved = await toolAgent.SerializeSessionAsync(session);
        Console.WriteLine($"Session JSON size: {saved.GetRawText().Length} chars");
        Console.WriteLine("Isi JSON ko Redis/SQL me rakh do - app restart hone par bhi");
        Console.WriteLine("DeserializeSessionAsync se chat wapas mil jayegi.\n");

        Console.WriteLine("Seekho:");
        Console.WriteLine("  * ChatClientAgent kisi bhi IChatClient par chalta hai - OpenAI,");
        Console.WriteLine("    Azure, Ollama. Provider badalna matlab ek line config badalna.");
        Console.WriteLine("  * Session hi memory hai. Use store karna TUMHARI zimmedari hai.");
        Console.WriteLine("  * Session kabhi unbounded mat badhne do - token cost phat jayegi.");
    }
}
