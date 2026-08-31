using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FaqRag.WebApi.Models;

namespace FaqRag.WebApi.Services;

public class QdrantService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaEmbeddingService _embeddingService;
    private readonly string _collectionName;
    private readonly int _vectorSize;

    public QdrantService(
        HttpClient httpClient,
        OllamaEmbeddingService embeddingService,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _embeddingService = embeddingService;

        _httpClient.BaseAddress = new Uri(configuration["Qdrant:Url"] ?? "http://localhost:6333");
        _collectionName = configuration["Qdrant:Collection"] ?? "faq";
        _vectorSize = int.TryParse(configuration["Qdrant:VectorSize"], out var size) ? size : 1024;
    }

    public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"/collections/{_collectionName}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }

        var body = new
        {
            vectors = new
            {
                size = _vectorSize,
                distance = "Cosine"
            }
        };

        using var createResponse = await _httpClient.PutAsJsonAsync(
            $"/collections/{_collectionName}",
            body,
            cancellationToken);

        createResponse.EnsureSuccessStatusCode();
    }

    public async Task UpsertAsync(IEnumerable<FaqItem> items, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var points = new List<object>();

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Question) || string.IsNullOrWhiteSpace(item.Answer))
            {
                continue;
            }

            var embedding = await _embeddingService.GenerateEmbeddingAsync(item.Question, cancellationToken);
            var vector = embedding.Embeddings.FirstOrDefault()
                ?? throw new InvalidOperationException("Ollama returned no embedding vector.");

            points.Add(new
            {
                id = Guid.NewGuid(),
                vector,
                payload = new
                {
                    item.Question,
                    item.Answer
                }
            });
        }

        if (points.Count == 0)
        {
            return;
        }

        using var response = await _httpClient.PutAsJsonAsync(
            $"/collections/{_collectionName}/points?wait=true",
            new { points },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<FaqSearchResult>> SearchAsync(
        string query,
        int limit = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<FaqSearchResult>();
        }

        await EnsureCollectionAsync(cancellationToken);

        var embedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        var vector = embedding.Embeddings.FirstOrDefault()
            ?? throw new InvalidOperationException("Ollama returned no embedding vector.");

        var body = new
        {
            vector,
            limit = Math.Clamp(limit, 1, 20),
            with_payload = true
        };

        using var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/search",
            body,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var results = new List<FaqSearchResult>();

        if (!document.RootElement.TryGetProperty("result", out var resultArray))
        {
            return results;
        }

        foreach (var point in resultArray.EnumerateArray())
        {
            if (!point.TryGetProperty("payload", out var payload))
            {
                continue;
            }

            var question = payload.TryGetProperty("Question", out var q)
                ? q.GetString()
                : payload.TryGetProperty("question", out q) ? q.GetString() : null;

            var answer = payload.TryGetProperty("Answer", out var a)
                ? a.GetString()
                : payload.TryGetProperty("answer", out a) ? a.GetString() : null;

            results.Add(new FaqSearchResult
            {
                Score = point.TryGetProperty("score", out var score) ? score.GetDouble() : 0,
                Item = new FaqItem
                {
                    Question = question ?? string.Empty,
                    Answer = answer ?? string.Empty
                }
            });
        }

        return results;
    }
}
