namespace BudgetBoard.Service.Models;

public class AutomaticRulePreviewMatch
{
    public Guid ID { get; set; }
    public DateOnly Date { get; set; }
    public string? MerchantName { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
    public Guid AccountID { get; set; }
    public string AccountName { get; set; } = string.Empty;
}

public class AutomaticRulePreviewResponse
{
    public const int MatchLimit = 25;

    public int MatchCount { get; set; }
    public IReadOnlyList<AutomaticRulePreviewMatch> Matches { get; set; } = [];
}
