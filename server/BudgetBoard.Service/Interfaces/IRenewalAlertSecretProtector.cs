namespace BudgetBoard.Service.Interfaces;

/// <summary>
/// Encrypts notification secrets before they are written to the database.
/// </summary>
public interface IRenewalAlertSecretProtector
{
    /// <summary>
    /// Encrypts a secret for storage.
    /// </summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypts a secret previously produced by <see cref="Protect"/>.
    /// </summary>
    string Unprotect(string ciphertext);
}
