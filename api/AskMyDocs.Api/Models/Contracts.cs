namespace AskMyDocs.Api.Models;

/// <summary>Document upload karne ke liye request.</summary>
public sealed record IngestRequest(string FileName, string Content);

/// <summary>Upload ke baad kya hua.</summary>
public sealed record IngestResponse(string DocumentId, string FileName, int ChunkCount);

/// <summary>Sawal poochne ke liye request. SessionId optional hai (agent ki memory ke liye).</summary>
public sealed record AskRequest(string Question, string? SessionId = null);

/// <summary>Ek citation - answer kis chunk se bana.</summary>
public sealed record SourceRef(int Marker, string FileName, int ChunkIndex, double Score, string Snippet);

/// <summary>RAG ka final jawab + sources.</summary>
public sealed record AskResponse(string Answer, IReadOnlyList<SourceRef> Sources, int RetrievedCount);

/// <summary>Agent ka jawab - isme ye bhi aata hai ki agent ne kaunse tool chalaye.</summary>
public sealed record AgentAskResponse(string Answer, string SessionId, IReadOnlyList<string> ToolsUsed);

/// <summary>Uploaded documents ki list.</summary>
public sealed record DocumentSummary(string DocumentId, string FileName, int ChunkCount, DateTimeOffset UploadedAt);
