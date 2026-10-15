using BudgetBoard.Service.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace BudgetBoard.WebAPI.Services;

/// <summary>
/// Encrypts notification secrets with the ASP.NET data-protection key ring.
/// </summary>
public sealed class DataProtectionRenewalAlertSecretProtector(IDataProtectionProvider provider)
    : IRenewalAlertSecretProtector
{
    public const string Purpose = "BudgetBoard.RenewalAlerts.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        return _protector.Protect(plaintext);
    }

    /// <inheritdoc />
    public string Unprotect(string ciphertext)
    {
        return _protector.Unprotect(ciphertext);
    }
}
