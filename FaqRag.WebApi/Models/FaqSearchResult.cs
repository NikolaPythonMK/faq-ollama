namespace FaqRag.WebApi.Models;

public class FaqSearchResult
{
    public double Score { get; set; }
    public FaqItem Item { get; set; } = new();
}
