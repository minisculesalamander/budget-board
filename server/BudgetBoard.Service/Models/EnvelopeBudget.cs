namespace BudgetBoard.Service.Models;

public interface IEnvelopeAssignRequest
{
    DateOnly Month { get; }
    string Category { get; }
    decimal Assigned { get; }
}

public class EnvelopeAssignRequest : IEnvelopeAssignRequest
{
    public DateOnly Month { get; set; } = DateOnly.MinValue;
    public string Category { get; set; } = string.Empty;
    public decimal Assigned { get; set; } = 0;
}

public interface IEnvelopeMoveRequest
{
    DateOnly Month { get; }
    string FromCategory { get; }
    string ToCategory { get; }
    decimal Amount { get; }
}

public class EnvelopeMoveRequest : IEnvelopeMoveRequest
{
    public DateOnly Month { get; set; } = DateOnly.MinValue;
    public string FromCategory { get; set; } = string.Empty;
    public string ToCategory { get; set; } = string.Empty;
    public decimal Amount { get; set; } = 0;
}

public class EnvelopeEnabledRequest
{
    public bool Enabled { get; set; } = false;
}

public class EnvelopeAccountsRequest
{
    public IReadOnlyList<Guid> AccountIds { get; set; } = [];
}

public interface IEnvelopeMonthResponse
{
    bool Enabled { get; }
    DateOnly Month { get; }
    decimal ToBeAssigned { get; }
    decimal OnBudgetBalance { get; }
    decimal Income { get; }
    decimal OutsideActivity { get; }
    IReadOnlyList<IEnvelopeAccountResponse> Accounts { get; }
    IReadOnlyList<IEnvelopeGroupResponse> Groups { get; }
}

public interface IEnvelopeAccountResponse
{
    Guid ID { get; }
    string Name { get; }
    string InstitutionName { get; }
    decimal Balance { get; }
    bool OnBudget { get; }
}

public interface IEnvelopeGroupResponse
{
    string Name { get; }
    IReadOnlyList<IEnvelopeCategoryResponse> Envelopes { get; }
}

public interface IEnvelopeCategoryResponse
{
    string Category { get; }
    string Parent { get; }
    decimal Leftover { get; }
    decimal Assigned { get; }
    decimal Activity { get; }
    decimal Available { get; }
}

public class EnvelopeMonthResponse : IEnvelopeMonthResponse
{
    public bool Enabled { get; set; } = false;
    public DateOnly Month { get; set; } = DateOnly.MinValue;
    public decimal ToBeAssigned { get; set; } = 0;
    public decimal OnBudgetBalance { get; set; } = 0;
    public decimal Income { get; set; } = 0;
    public decimal OutsideActivity { get; set; } = 0;
    public IReadOnlyList<IEnvelopeAccountResponse> Accounts { get; set; } = [];
    public IReadOnlyList<IEnvelopeGroupResponse> Groups { get; set; } = [];
}

public class EnvelopeAccountResponse : IEnvelopeAccountResponse
{
    public Guid ID { get; set; } = Guid.Empty;
    public string Name { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public decimal Balance { get; set; } = 0;
    public bool OnBudget { get; set; } = false;
}

public class EnvelopeGroupResponse : IEnvelopeGroupResponse
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<IEnvelopeCategoryResponse> Envelopes { get; set; } = [];
}

public class EnvelopeCategoryResponse : IEnvelopeCategoryResponse
{
    public string Category { get; set; } = string.Empty;
    public string Parent { get; set; } = string.Empty;
    public decimal Leftover { get; set; } = 0;
    public decimal Assigned { get; set; } = 0;
    public decimal Activity { get; set; } = 0;
    public decimal Available { get; set; } = 0;
}
