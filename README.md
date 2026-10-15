# AskMyDocs

Apne documents se baat karne wala AI assistant.
Ye `bhagat_ainotes.pdf` book ka saath chalne wala project hai.

Stack: **.NET 10 Web API + Angular 21 + Microsoft Agent Framework + Vector DB (InMemory / Qdrant)**

```
AskMyDocs/
  AskMyDocs.sln
  api/AskMyDocs.Api/        <- Web API: ingestion, RAG, agent, SSE streaming
  labs/AskMyDocs.Labs/      <- 7 chhote console labs (har concept ka alag demo)
  web/askmydocs-web/        <- Angular chat UI
  sample-docs/              <- test karne ke liye ek sample policy document
  docker-compose.yml        <- Qdrant
```

---

## 1. Setup (ek baar)

### API key

`api/AskMyDocs.Api/appsettings.json` aur `labs/AskMyDocs.Labs/appsettings.json`
dono me `Ai:ApiKey` bharo.

**OpenAI** (sabse aasan, paisa lagega):

```json
"Ai": {
  "Endpoint": "https://api.openai.com/v1",
  "ApiKey": "sk-...",
  "ChatModel": "gpt-4o-mini",
  "EmbeddingModel": "text-embedding-3-small"
}
```

**Ollama** (bilkul free, local, internet ke bina chalta hai):

```bash
ollama pull llama3.1
ollama pull nomic-embed-text
```

```json
"Ai": {
  "Endpoint": "http://localhost:11434/v1",
  "ApiKey": "",
  "ChatModel": "llama3.1",
  "EmbeddingModel": "nomic-embed-text"
}
```

> **Ollama use kar rahe ho to zaroori:** `nomic-embed-text` ki dimension 768 hai,
> 1536 nahi. `Models/DocChunk.cs` me `[VectorStoreVector(1536, ...)]` ko
> `[VectorStoreVector(768, ...)]` karna padega. Labs me `Lab02` aur `Lab04`
> ke records me bhi wahi badalna hai. Warna "dimension mismatch" error aayega.

---

## 2. Labs chalao (yahan se shuru karo)

```bash
cd labs/AskMyDocs.Labs

dotnet run -- lab3     # chunking - bina API key ke chalta hai, isse shuru karo
dotnet run -- lab1     # embedding kya hai, cosine similarity
dotnet run -- lab2     # vector store: upsert, search, filter
dotnet run -- lab4     # mini RAG: bina RAG vs RAG ke saath
dotnet run -- lab5     # Semantic Kernel: plugin + auto function calling
dotnet run -- lab6     # Agent Framework: agent + tools + session
dotnet run -- lab7     # multi-agent sequential workflow
```

Har lab apne end me "Seekho" section print karta hai - wahi asli content hai.

---

## 3. Poora project chalao

**Terminal 1 - API:**

```bash
cd api/AskMyDocs.Api
dotnet run
# http://localhost:5080/health kholkar check karo
```

**Terminal 2 - Angular:**

```bash
cd web/askmydocs-web
npm start
# http://localhost:4200
```

Browser me:

1. Left se `sample-docs/itsm-policy.md` upload karo
2. Mode chuno (Plain RAG / Agent / Agent + streaming)
3. Poocho: *"P1 ticket ka SLA kitna hai?"*

---

## 4. Qdrant par shift karo (asli vector DB)

```bash
docker compose up -d
# dashboard: http://localhost:6333/dashboard
```

`api/AskMyDocs.Api/appsettings.json` me:

```json
"VectorStore": { "Kind": "Qdrant" }
```

API restart karo. **Baaki ek line code nahi badla** - yahi
`Microsoft.Extensions.VectorData` abstraction ka poora point hai.

---

## 5. API endpoints

| Method | Route | Kya karta hai |
|---|---|---|
| GET | `/health` | Config check - kaunsa store aur model chal raha hai |
| POST | `/api/documents` | Document ingest: chunk -> embed -> store |
| GET | `/api/documents` | Indexed documents ki list |
| POST | `/api/ask` | Plain RAG: retrieve + generate + citations |
| POST | `/api/agent/ask` | Agent: tools + session memory |
| POST | `/api/agent/stream` | Agent + SSE streaming |

`api/AskMyDocs.Api/AskMyDocs.Api.http` me sab ready-made requests hain.

---

## 6. Package versions (7 Oct 2026)

| Package | Version | Kaam |
|---|---|---|
| `Microsoft.Agents.AI` | 1.23.0 | Agent framework core |
| `Microsoft.Agents.AI.Workflows` | 1.23.0 | Multi-agent workflows |
| `Microsoft.Extensions.AI` | 10.10.0 | `IChatClient`, `IEmbeddingGenerator` |
| `Microsoft.Extensions.VectorData.Abstractions` | 10.10.0 | `VectorStoreCollection<,>` |
| `CommunityToolkit.VectorData.InMemory` | 1.0.1 | In-memory vector store |
| `CommunityToolkit.VectorData.Qdrant` | 1.0.0 | Qdrant connector |
| `Microsoft.SemanticKernel` | 1.81.0 | Sirf Lab 5 ke liye |

> Vector store connectors pehle `Microsoft.SemanticKernel.Connectors.*` me the.
> Ab wo .NET Community Toolkit me shift ho gaye hain -
> `CommunityToolkit.VectorData.*`. Purane blog posts me abhi bhi purana naam
> milega, isliye confuse mat hona.
