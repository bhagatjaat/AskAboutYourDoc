using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AskMyDocs.Labs;

/// <summary>
/// LAB 5 - Semantic Kernel.
///
/// Kernel kya hai? Ek DI container jo AI services + tumhare C# functions ko
/// ek jagah rakhta hai, aur LLM ko bolta hai "ye functions available hain,
/// zaroorat ho to bula lena".
///
/// 2026 ki asli baat (ye zaroor samajh lo):
///   Semantic Kernel ab MAINTENANCE MODE me hai. Microsoft Agent Framework
///   (Microsoft.Agents.AI) usi team ka agla version hai - SK + AutoGen ko
///   mila ke banaya gaya, April 2026 me GA hua.
///
///   To SK kyun padh rahe ho?
///    a) Interview me abhi bhi poocha jata hai.
///    b) Lakhon lines production SK code chal raha hai - maintain karna padega.
///    c) Concepts (plugin, function calling, prompt template) Agent Framework
///       me bhi wahi hain, bas naam aur namespace badle hain.
///
///   NAYA code Agent Framework me likho - Lab 6 dekho.
/// </summary>
public static class Lab05_SemanticKernel
{
    /// <summary>
    /// Plugin = ek normal C# class. [KernelFunction] lagao, bas.
    /// SK reflection se JSON schema bana ke LLM ko bata deta hai.
    /// </summary>
    public sealed class ItsmPlugin
    {
        [KernelFunction]
        [Description("Kisi ticket ka current status batata hai")]
        public string GetTicketStatus(
            [Description("Ticket number jaise INC-1001")] string ticketId)
        {
            Console.WriteLine($"   >> [tool chala] GetTicketStatus({ticketId})");
            return ticketId.Equals("INC-1001", StringComparison.OrdinalIgnoreCase)
                ? "In Progress, assigned to Rahul Verma, priority P2"
                : $"{ticketId} nahi mila";
        }

        [KernelFunction]
        [Description("Priority ke hisaab se SLA ghante batata hai")]
        public int GetSlaHours(
            [Description("Priority: P1, P2, P3 ya P4")] string priority)
        {
            Console.WriteLine($"   >> [tool chala] GetSlaHours({priority})");
            return priority.ToUpperInvariant() switch
            {
                "P1" => 4,
                "P2" => 8,
                "P3" => 24,
                _ => 72
            };
        }
    }

    public static async Task RunAsync()
    {
        Console.WriteLine("=== LAB 5: Semantic Kernel ===\n");

        // ---------- Kernel banao ----------
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(AiConfig.ChatModel, AiConfig.CreateOpenAIClient());
        builder.Plugins.AddFromObject(new ItsmPlugin(), "Itsm");

        var kernel = builder.Build();

        // ---------- Part A: prompt function (template ke saath) ----------
        Console.WriteLine("--- A. Prompt function: prompt hi ek function hai ---\n");

        var summarize = kernel.CreateFunctionFromPrompt(
            """
            Neeche diye ticket description ko {{$style}} style me ek line me summarise karo.

            Description: {{$description}}
            """);

        var summary = await kernel.InvokeAsync(summarize, new KernelArguments
        {
            ["description"] = "User ka Outlook baar baar crash ho raha hai, " +
                              "profile recreate karne par bhi theek nahi hua, " +
                              "ab email bhej nahi pa raha.",
            ["style"] = "formal"
        });

        Console.WriteLine($"Summary: {summary}\n");
        Console.WriteLine("Note: {{$description}} ek template variable hai. Prompt ko");
        Console.WriteLine("string concatenation se banane ke bajay aise reusable rakho.\n");

        // ---------- Part B: auto function calling ----------
        Console.WriteLine("--- B. Auto function calling: LLM khud tool chunta hai ---\n");

        var settings = new OpenAIPromptExecutionSettings
        {
            // Ye ek line poora function-calling loop chala deti hai.
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.1
        };

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory("Tum ek IT support assistant ho. Tools use karke exact jawab do.");

        history.AddUserMessage("INC-1001 ka status kya hai aur uska SLA kitne ghante ka hai?");

        Console.WriteLine("User: INC-1001 ka status kya hai aur uska SLA kitne ghante ka hai?\n");

        var reply = await chatService.GetChatMessageContentAsync(history, settings, kernel);

        Console.WriteLine($"\nAssistant: {reply}\n");

        Console.WriteLine("Seekho:");
        Console.WriteLine("  * Humne kabhi nahi likha 'pehle GetTicketStatus chalao phir GetSlaHours'.");
        Console.WriteLine("    LLM ne description padhke khud decide kiya, aur do tool chalaye.");
        Console.WriteLine("  * Description hi tumhara control hai. Ghatiya description = galat tool.");
        Console.WriteLine("  * Agent Framework me yahi cheez `tools:` parameter se hoti hai - Lab 6.");
    }
}
