using FaqRag.WebApi.Models;
using FaqRag.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddHttpClient<QdrantService>();
builder.Services.AddHttpClient<OllamaChatService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/embedding", async (
    FaqAskRequest request,
    OllamaEmbeddingService embeddingService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest("Question is required.");
    }

    var embedding = await embeddingService.GenerateEmbeddingAsync(request.Question, cancellationToken);
    return Results.Ok(embedding);
});

app.MapPost("/api/faq/create", async (
    List<FaqItem> items,
    QdrantService qdrantService,
    CancellationToken cancellationToken) =>
{
    if (items.Count == 0)
    {
        return Results.BadRequest("At least one FAQ item is required.");
    }

    await qdrantService.UpsertAsync(items, cancellationToken);
    return Results.Ok(new { inserted = items.Count });
});

app.MapPost("/api/faq/search", async (
    FaqSearchRequest request,
    QdrantService qdrantService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Query))
    {
        return Results.BadRequest("Query is required.");
    }

    var results = await qdrantService.SearchAsync(request.Query, request.Limit, cancellationToken);
    return Results.Ok(results);
});

app.MapPost("/api/faq/ask/stream", async (
    FaqAskRequest request,
    HttpContext context,
    QdrantService qdrantService,
    OllamaChatService chatService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Question is required.", cancellationToken);
        return;
    }

    var matches = await qdrantService.SearchAsync(request.Question, 3, cancellationToken);

    context.Response.ContentType = "text/plain; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache";

    await foreach (var token in chatService.StreamAnswerAsync(request.Question, matches, cancellationToken))
    {
        await context.Response.WriteAsync(token, cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }
});

app.Run();
