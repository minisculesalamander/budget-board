namespace BudgetBoard.Service.Models;

public interface IPlanningSettingsUpdateRequest
{
    int FinancialYearStartMonth { get; }
}

public class PlanningSettingsUpdateRequest : IPlanningSettingsUpdateRequest
{
    public int FinancialYearStartMonth { get; set; } = 1;
}

public interface IYearPlanCellUpdateRequest
{
    DateOnly Month { get; }
    string Category { get; }
    string? Amount { get; }
}

public class YearPlanCellUpdateRequest : IYearPlanCellUpdateRequest
{
    public DateOnly Month { get; set; } = DateOnly.MinValue;
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Planned amount, or an arithmetic expression such as 800+200.
    /// Null or blank clears the month.
    /// </summary>
    public string? Amount { get; set; }
}

public interface IYearPlanResponse
{
    int FinancialYearStartMonth { get; }
    int StartYear { get; }
    int YearCount { get; }
    IReadOnlyList<DateOnly> Months { get; }
    IReadOnlyList<YearPlanSectionResponse> Sections { get; }
    IReadOnlyList<YearPlanMonthTotalResponse> MonthTotals { get; }
    IReadOnlyList<YearPlanYearTotalResponse> YearTotals { get; }
}

public class YearPlanResponse : IYearPlanResponse
{
    public int FinancialYearStartMonth { get; set; } = 1;
    public int StartYear { get; set; }
    public int YearCount { get; set; }
    public IReadOnlyList<DateOnly> Months { get; set; } = [];
    public IReadOnlyList<YearPlanSectionResponse> Sections { get; set; } = [];
    public IReadOnlyList<YearPlanMonthTotalResponse> MonthTotals { get; set; } = [];
    public IReadOnlyList<YearPlanYearTotalResponse> YearTotals { get; set; } = [];
}

public class YearPlanSectionResponse
{
    public string Kind { get; set; } = string.Empty;
    public IReadOnlyList<YearPlanParentResponse> Parents { get; set; } = [];
}

public class YearPlanParentResponse
{
    public string Category { get; set; } = string.Empty;
    public IReadOnlyList<YearPlanCellResponse> Cells { get; set; } = [];
    public IReadOnlyList<YearPlanChildResponse> Children { get; set; } = [];
}

public class YearPlanChildResponse
{
    public string Category { get; set; } = string.Empty;
    public IReadOnlyList<YearPlanCellResponse> Cells { get; set; } = [];
}

public class YearPlanCellResponse
{
    public DateOnly Month { get; set; }
    public Guid? BudgetId { get; set; }
    public decimal? Planned { get; set; }
    public decimal Actual { get; set; }
}

public class YearPlanMonthTotalResponse
{
    public DateOnly Month { get; set; }
    public int FinancialYear { get; set; }
    public decimal PlannedIncome { get; set; }
    public decimal ActualIncome { get; set; }
    public decimal PlannedExpense { get; set; }
    public decimal ActualExpense { get; set; }
    public decimal PlannedSavings { get; set; }
    public decimal ActualSavings { get; set; }
}

public class YearPlanYearTotalResponse
{
    public int FinancialYear { get; set; }
    public decimal PlannedIncome { get; set; }
    public decimal ActualIncome { get; set; }
    public decimal PlannedExpense { get; set; }
    public decimal ActualExpense { get; set; }
    public decimal PlannedSavings { get; set; }
    public decimal ActualSavings { get; set; }
}
