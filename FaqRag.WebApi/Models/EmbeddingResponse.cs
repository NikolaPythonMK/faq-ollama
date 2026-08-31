using Newtonsoft.Json;
namespace FaqRag.WebApi.Models
{
    public class EmbeddingResponse
    {
        [JsonProperty("model")]
        public string Model { get; set; } = string.Empty;
        [JsonProperty("embeddings")]
        public List<List<float>> Embeddings { get; set; } = [];
    }
}
