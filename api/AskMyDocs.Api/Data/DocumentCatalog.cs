using System.Collections.Concurrent;
using AskMyDocs.Api.Models;
using MySqlConnector;

namespace AskMyDocs.Api.Data;

/// <summary>
/// Uploaded documents ki list (naam, kitne chunks, kab upload hua).
///
/// Dhyan do: chunks aur embeddings yahan nahi hain - wo vector store (Qdrant) me hain.
/// Yahan sirf normal metadata hai, jiske liye normal SQL database sahi jagah hai.
///
/// Vector store ki tarah yahan bhi ek interface hai aur do implementation.
/// Program.cs config ("Catalog:Kind") dekh kar ek chunta hai - baaki code ko farak nahi padta.
/// </summary>
public interface IDocumentCatalog
{
    Task AddAsync(DocumentSummary document, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct = default);
}

/// <summary>Server restart par sab gayab. Bina database ke project chalane ke liye.</summary>
public sealed class InMemoryDocumentCatalog : IDocumentCatalog
{
    private readonly ConcurrentDictionary<string, DocumentSummary> _documents = new();

    public Task AddAsync(DocumentSummary document, CancellationToken ct = default)
    {
        _documents[document.DocumentId] = document;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DocumentSummary>>(
            _documents.Values.OrderByDescending(d => d.UploadedAt).ToList());
}

/// <summary>
/// MySQL wala catalog. Table db/mysql-init.sql se banti hai.
///
/// Plain ADO.NET (MySqlConnector) - koi ORM nahi, taki SQL saaf dikhe.
/// Har call par naya connection kholna theek hai: MySqlConnector andar se
/// connection pooling karta hai, asli TCP connection reuse hota hai.
/// </summary>
public sealed class MySqlDocumentCatalog(string connectionString) : IDocumentCatalog
{
    public async Task AddAsync(DocumentSummary document, CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // Values hamesha parameters (@id, @fileName...) se bhejo, string jodkar kabhi nahi.
        // File ka naam user deta hai - string jodne se SQL injection ho sakta hai.
        await using var command = new MySqlCommand(
            """
            INSERT INTO documents (id, file_name, chunk_count, uploaded_at)
            VALUES (@id, @fileName, @chunkCount, @uploadedAt)
            """,
            connection);

        command.Parameters.AddWithValue("@id", document.DocumentId);
        command.Parameters.AddWithValue("@fileName", document.FileName);
        command.Parameters.AddWithValue("@chunkCount", document.ChunkCount);
        // MySQL ka DATETIME timezone yaad nahi rakhta, isliye hamesha UTC save karo.
        command.Parameters.AddWithValue("@uploadedAt", document.UploadedAt.UtcDateTime);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = new MySqlCommand(
            """
            SELECT id, file_name, chunk_count, uploaded_at
            FROM documents
            ORDER BY uploaded_at DESC
            """,
            connection);

        var documents = new List<DocumentSummary>();

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            // Save karte waqt UTC tha, isliye padhte waqt bhi offset 0 (UTC) lagao.
            var uploadedAt = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc);

            documents.Add(new DocumentSummary(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                new DateTimeOffset(uploadedAt)));
        }

        return documents;
    }
}
