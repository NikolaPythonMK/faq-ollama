# FAQ RAG Chatbot

A small .NET Web API that demonstrates Retrieval-Augmented Generation (RAG) using Ollama and Qdrant.

## Stack

- .NET 9 Web API
- Ollama
- `mxbai-embed-large` for embeddings
- `llama3.2` for answer generation
- Qdrant for vector storage and similarity search

## Flow

1. FAQ questions are converted into embeddings with Ollama.
2. Embeddings and FAQ payloads are stored in Qdrant.
3. A user question is embedded and searched against Qdrant.
4. The most relevant FAQ entries are added to the prompt as context.
5. Llama 3.2 generates the answer.
6. `/api/faq/ask/stream` streams the generated answer back to the client.

## Endpoints

- `POST /api/embedding`
- `POST /api/faq/create`
- `POST /api/faq/search`
- `POST /api/faq/ask/stream`

## Local dependencies

Ollama should be available at `http://localhost:11434` and Qdrant at `http://localhost:6333` by default. These values can be changed in `FaqRag.WebApi/appsettings.json`.

Required Ollama models:

```bash
ollama pull mxbai-embed-large
ollama pull llama3.2
```

Example Qdrant Docker command:

```bash
docker run -p 6333:6333 qdrant/qdrant
```

## Run

```bash
dotnet restore
dotnet run --project FaqRag.WebApi/FaqRag.WebApi.csproj
```

Example requests are included in `FaqRag.WebApi/FaqRag.WebApi.http`.
