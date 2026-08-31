using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FaqRag.WebApi.Models;

namespace FaqRag.WebApi.Services;

public class OllamaChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaChatService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(configuration["Ollama:Url"] ?? "http://localhost:11434");
        _model = configuration["Ollama:ChatModel"] ?? "llama3.2";
    }

    public async IAsyncEnumerable<string> StreamAnswerAsync(
        string question,
        IReadOnlyList<FaqSearchResult> context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(question, context);

        var body = new
        {
            model = _model,
            prompt,
            stream = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (root.TryGetProperty("response", out var responsePart))
            {
                var token = responsePart.GetString();
                if (!string.IsNullOrEmpty(token))
                {
                    yield return token;
                }
            }

            if (root.TryGetProperty("done", out var done) && done.GetBoolean())
            {
                yield break;
            }
        }
    }

    private static string BuildPrompt(string question, IReadOnlyList<FaqSearchResult> context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Answer the user's question using only the FAQ context below.");
        builder.AppendLine("If the context does not contain enough information, say that you do not know based on the available FAQ data.");
        builder.AppendLine();
        builder.AppendLine("FAQ context:");

        for (var i = 0; i < context.Count; i++)
        {
            builder.AppendLine($"{i + 1}. Question: {context[i].Item.Question}");
            builder.AppendLine($"   Answer: {context[i].Item.Answer}");
        }

        builder.AppendLine();
        builder.AppendLine($"User question: {question}");
        builder.Append("Answer:");

        return builder.ToString();
    }
}
