using AskMyDocs.Labs;

// Labs ka dispatcher.
//
//   dotnet run -- lab1   embeddings aur cosine similarity
//   dotnet run -- lab2   vector store: store + search + filter
//   dotnet run -- lab3   chunking (ye wala bilkul free hai, koi API call nahi)
//   dotnet run -- lab4   mini RAG: retrieve -> augment -> generate
//   dotnet run -- lab5   Semantic Kernel: plugin + auto function calling
//   dotnet run -- lab6   Agent Framework: agent + tools + session
//   dotnet run -- lab7   multi-agent sequential workflow

Console.OutputEncoding = System.Text.Encoding.UTF8;

var which = args.FirstOrDefault()?.ToLowerInvariant();

try
{
    switch (which)
    {
        case "lab1": await Lab01_Embeddings.RunAsync(); break;
        case "lab2": await Lab02_VectorStore.RunAsync(); break;
        case "lab3": await Lab03_Chunking.RunAsync(); break;
        case "lab4": await Lab04_MiniRag.RunAsync(); break;
        case "lab5": await Lab05_SemanticKernel.RunAsync(); break;
        case "lab6": await Lab06_Agent.RunAsync(); break;
        case "lab7": await Lab07_MultiAgent.RunAsync(); break;
        default:
            Console.WriteLine("AskMyDocs Labs");
            Console.WriteLine();
            Console.WriteLine("  dotnet run -- lab1    Embedding kya hai, cosine similarity");
            Console.WriteLine("  dotnet run -- lab2    Vector store: upsert, search, filter");
            Console.WriteLine("  dotnet run -- lab3    Chunking (API key ke bina chalta hai)");
            Console.WriteLine("  dotnet run -- lab4    Mini RAG end-to-end");
            Console.WriteLine("  dotnet run -- lab5    Semantic Kernel");
            Console.WriteLine("  dotnet run -- lab6    Microsoft Agent Framework");
            Console.WriteLine("  dotnet run -- lab7    Multi-agent workflow");
            Console.WriteLine();
            Console.WriteLine("Pehle appsettings.json me Ai:ApiKey bhar lena (lab3 ko chhodkar).");
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"GADBAD: {ex.GetType().Name}");
    Console.WriteLine(ex.Message);
    Console.WriteLine();
    Console.WriteLine("Aam wajah:");
    Console.WriteLine("  * appsettings.json me Ai:ApiKey khali hai");
    Console.WriteLine("  * Ai:Endpoint galat hai (Ollama ke liye http://localhost:11434/v1)");
    Console.WriteLine("  * Embedding dimension mismatch - DocChunk/Faq me 768 likha hai (nomic-embed-text),");
    Console.WriteLine("    OpenAI ka text-embedding-3-small use kar rahe ho to use 1536 karo");
}
