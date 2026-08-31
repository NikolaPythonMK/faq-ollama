using FaqRag.WebApi.Models;
using Newtonsoft.Json;
using System.Text;

namespace FaqRag.WebApi.Services
{
    public class OllamaEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaEndpoint;
        private readonly string _modelName;

        public OllamaEmbeddingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            var ollamaUrl = configuration["Ollama:Url"] ?? "http://localhost:11434";
            _ollamaEndpoint = $"{ollamaUrl.TrimEnd('/')}/api/embed";
            _modelName = configuration["Ollama:EmbeddingModel"] ?? "mxbai-embed-large";
        }

        public async Task<EmbeddingResponse> GenerateEmbeddingAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            var requestBody = new
            {
                model = _modelName,
                input
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(_ollamaEndpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            var embeddingResponse = JsonConvert.DeserializeObject<EmbeddingResponse>(responseString)
                ?? throw new InvalidOperationException("Could not deserialize Ollama embedding response.");

            return embeddingResponse;
        }
    }
}
