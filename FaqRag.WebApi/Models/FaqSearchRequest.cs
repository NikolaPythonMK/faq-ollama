namespace FaqRag.WebApi.Models;

public class FaqSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 3;
}
