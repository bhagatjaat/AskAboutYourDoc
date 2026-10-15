using System.ClientModel;
using System.Text.Json;
using AskMyDocs.Api.Agents;
using AskMyDocs.Api.Models;
using AskMyDocs.Api.Rag;
using CommunityToolkit.VectorData.InMemory;
using CommunityToolkit.VectorData.Qdrant;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using OpenAI;
using Qdrant.Client;
var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Config padho
// ---------------------------------------------------------------------------
var ai = builder.Configuration.GetSection("Ai");
var apiKey = string.IsNullOrWhiteSpace(ai["ApiKey"]) ? "not-needed-for-local" : ai["ApiKey"]!;
var endpoint = ai["Endpoint"] ?? "https://api.openai.com/v1";
var chatModel = ai["ChatModel"] ?? "gpt-4o-mini";
var embeddingModel = ai["EmbeddingModel"] ?? "text-embedding-3-small";

var vectorCfg = builder.Configuration.GetSection("VectorStore");
var storeKind = vectorCfg["Kind"] ?? "InMemory";
var collectionName = vectorCfg["Collection"] ?? "askmydocs";
var qdrantHost = vectorCfg["QdrantHost"] ?? "localhost";
var qdrantPort = int.TryParse(vectorCfg["QdrantPort"], out var parsedPort) ? parsedPort : 6334;
// ---------------------------------------------------------------------------
// 2. LLM clients
//
// OpenAIClient ko custom Endpoint dene se ye kisi bhi OpenAI-compatible server
// se baat kar leta hai: OpenAI, Azure OpenAI, ya local Ollama
// (http://localhost:11434/v1). Code ek hi rehta hai - sirf config badalti hai.
// ---------------------------------------------------------------------------
var openAi = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

builder.Services.AddSingleton<IChatClient>(_ =>
    openAi.GetChatClient(chatModel)
          .AsIChatClient()
          .AsBuilder()
          .UseFunctionInvocation()   // tool calling ka loop ye chalata hai
          .Build());

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
    openAi.GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator());

// ---------------------------------------------------------------------------
// 3. Vector store
//
// Dono branches ka return type ek hi hai: VectorStoreCollection<Guid, DocChunk>.
// Yahi Microsoft.Extensions.VectorData ka asli fayda hai - InMemory se Qdrant
// par jaane me baaki ek line code nahi badalta.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<VectorStoreCollection<Guid, DocChunk>>(sp =>
{
    var generator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

    if (storeKind.Equals("Qdrant", StringComparison.OrdinalIgnoreCase))
    {
        var client = new QdrantClient(qdrantHost, qdrantPort);
        return new QdrantCollection<Guid, DocChunk>(
            client,
            collectionName,
            ownsClient: true,
            new QdrantCollectionOptions { EmbeddingGenerator = generator });
    }

    return new InMemoryCollection<Guid, DocChunk>(
        collectionName,
        new InMemoryCollectionOptions { EmbeddingGenerator = generator });
});

// ---------------------------------------------------------------------------
// 4. Hamari apni services
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IngestionService>();
builder.Services.AddSingleton<RagService>();
builder.Services.AddSingleton<SupportTools>();
builder.Services.AddSingleton<SupportAgentFactory>();

const string AngularCors = "angular";
builder.Services.AddCors(o => o.AddPolicy(AngularCors, policy => policy
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();
app.UseCors(AngularCors);

// ---------------------------------------------------------------------------
// 5. Endpoints
// ---------------------------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    store = storeKind,
    chatModel,
    embeddingModel
}));

// ---- Document upload (Indexing phase) ----
app.MapPost("/api/documents", async (
    IngestRequest request,
    IngestionService ingestion,
    CancellationToken ct) =>
{
    var result = await ingestion.IngestAsync(request, ct);
    return Results.Ok(result);
});

app.MapGet("/api/documents", (IngestionService ingestion) => Results.Ok(ingestion.List()));

// ---- Plain RAG (koi agent nahi, sirf retrieve + generate) ----
app.MapPost("/api/ask", async (AskRequest request, RagService rag, CancellationToken ct) =>
{
    var answer = await rag.AskAsync(request.Question, ct);
    return Results.Ok(answer);
});

// ---- Agent (tools + memory) ----
app.MapPost("/api/agent/ask", async (
    AskRequest request,
    SupportAgentFactory factory,
    CancellationToken ct) =>
{
    var agent = factory.CreateAgent();
    var (session, sessionId) = await factory.GetOrCreateSessionAsync(agent, request.SessionId, ct);

    var response = await agent.RunAsync(request.Question, session, cancellationToken: ct);

    // Kaunse tool chale - ye debugging ke liye sona hai.
    var toolsUsed = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<FunctionCallContent>()
        .Select(c => c.Name)
        .Distinct()
        .ToList();

    return Results.Ok(new AgentAskResponse(response.Text, sessionId, toolsUsed));
});

// ---- Agent streaming (Server-Sent Events) ----
app.MapPost("/api/agent/stream", async (
    HttpContext http,
    AskRequest request,
    SupportAgentFactory factory,
    CancellationToken ct) =>
{
    http.Response.Headers.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";

    var agent = factory.CreateAgent();
    var (session, sessionId) = await factory.GetOrCreateSessionAsync(agent, request.SessionId, ct);

    await WriteEventAsync(http, new { type = "session", sessionId }, ct);

    await foreach (var update in agent.RunStreamingAsync(request.Question, session, cancellationToken: ct))
    {
        foreach (var call in update.Contents.OfType<FunctionCallContent>())
        {
            await WriteEventAsync(http, new { type = "tool", name = call.Name }, ct);
        }

        if (!string.IsNullOrEmpty(update.Text))
        {
            await WriteEventAsync(http, new { type = "token", text = update.Text }, ct);
        }
    }

    await WriteEventAsync(http, new { type = "done" }, ct);
});

app.Run();

static async Task WriteEventAsync(HttpContext http, object payload, CancellationToken ct)
{
    await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n", ct);
    await http.Response.Body.FlushAsync(ct);
}
