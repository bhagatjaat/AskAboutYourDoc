using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AskMyDocs.Labs;

/// <summary>
/// LAB 7 - Multi-agent workflow.
///
/// Ek agent se sab kuch karwane ki koshish mat karo. Jaise team me alag-alag
/// log hote hain, waise hi har agent ka ek kaam rakho:
///
///   Triage  -> ticket ko category aur priority do
///   Solver  -> solution likho
///   Editor  -> customer ke liye polite banao
///
/// Fayda: har agent ka prompt chhota aur focused rehta hai, debug karna
/// aasan hota hai, aur ek agent badalne se baaki nahi tootte.
///
/// AgentWorkflowBuilder teen pattern deta hai:
///   BuildSequential - ek ke baad ek (yahi lab me)
///   BuildConcurrent - sab saath me, phir result merge
///   Handoff         - agent khud decide kare ki kise pass kare
/// </summary>
public static class Lab07_MultiAgent
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== LAB 7: Multi-agent workflow ===\n");

        var chat = AiConfig.CreateChatClient();

        AIAgent triage = new ChatClientAgent(
            chat,
            instructions:
                """
                Tum IT ticket triage karte ho.
                Input: user ki complaint.
                Output: exactly 2 line -
                  Category: <Hardware|Software|Network|Access>
                  Priority: <P1|P2|P3|P4> (ek chhoti wajah ke saath)
                Aur kuch mat likho.
                """,
            name: "TriageAgent");

        AIAgent solver = new ChatClientAgent(
            chat,
            instructions:
                """
                Tum L2 support engineer ho. Upar di gayi category/priority
                aur complaint dekhkar maximum 4 numbered troubleshooting steps do.
                Technical bhasha chalegi. Koi greeting mat likho.
                """,
            name: "SolverAgent");

        AIAgent editor = new ChatClientAgent(
            chat,
            instructions:
                """
                Tum customer communication likhte ho.
                Upar ke technical steps ko end-user ke liye simple Hinglish me
                dobara likho. Short greeting + steps + ek closing line.
                Jargon hata do.
                """,
            name: "EditorAgent");

        // Teeno ko ek pipeline me jodo.
        var workflow = AgentWorkflowBuilder.BuildSequential(triage, solver, editor);

        const string complaint =
            "Subah se office WiFi par kuch bhi load nahi ho raha. " +
            "Mobile hotspot par sab chalta hai. Poori team ka yahi haal hai, " +
            "kal ka client demo atka hua hai.";

        Console.WriteLine($"COMPLAINT:\n  {complaint}\n");
        Console.WriteLine(new string('-', 72));

        var input = new List<ChatMessage> { new(ChatRole.User, complaint) };

        // RunStreamingAsync pehle ek StreamingRun handle deta hai,
        // phir WatchStreamAsync() se events aate hain.
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, input);

        // Agent executors messages jama karte rehte hain aur TurnToken milne par
        // hi LLM ko call karte hain. Ye line hata do to workflow bina ek bhi
        // LLM call kiye chupchaap khatam ho jaata hai.
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        string? currentAgent = null;

        await foreach (var evt in run.WatchStreamAsync())
        {
            switch (evt)
            {
                // Streaming me har agent apna jawab chhote tukdon (tokens) me
                // bhejta hai. Agent badla matlab pichhle wale ka kaam khatam.
                case AgentResponseUpdateEvent update:
                    if (update.ExecutorId != currentAgent)
                    {
                        if (currentAgent is not null)
                        {
                            Console.WriteLine("\n" + new string('-', 72));
                        }

                        currentAgent = update.ExecutorId;
                        Console.WriteLine($"\n### {update.Update.AuthorName ?? update.ExecutorId} ka output ###");
                    }

                    Console.Write(update.Update.Text);
                    break;

                // Workflow agent ki exception throw nahi karta, event bhejta hai.
                // Inhe ignore kiya to Ollama band hone par bhi kuch pata nahi chalega.
                case ExecutorFailedEvent failed:
                    throw failed.Data ?? new InvalidOperationException($"{failed.ExecutorId} fail ho gaya");

                case WorkflowErrorEvent error:
                    throw error.Exception ?? new InvalidOperationException("Workflow fail ho gaya");
            }
        }

        if (currentAgent is not null)
        {
            Console.WriteLine("\n" + new string('-', 72));
        }

        Console.WriteLine();
        Console.WriteLine("Seekho:");
        Console.WriteLine("  * Har agent ka output agle agent ka input ban gaya.");
        Console.WriteLine("  * Ek hi mega-prompt likhne se ye kyun behtar hai:");
        Console.WriteLine("      - har step alag se test ho sakta hai");
        Console.WriteLine("      - Triage ke liye sasta model, Solver ke liye mehnga model lagao");
        Console.WriteLine("      - galti kahan hui, turant pata chal jata hai");
        Console.WriteLine("  * Par yaad rakho: har agent = ek extra LLM call = extra paisa aur latency.");
        Console.WriteLine("    Jab tak ek agent se kaam chal raha hai, ek hi rakho.");
    }
}
