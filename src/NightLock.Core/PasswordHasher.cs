using System.Security.Cryptography;

namespace NightLock.Core;

public sealed class PasswordVerifier
{
    public string Algorithm { get; set; } = "PBKDF2-SHA256";
    public int Iterations { get; set; } = 210_000;
    public string SaltBase64 { get; set; } = "";
    public string HashBase64 { get; set; } = "";
}

/// <summary>
/// @spec spec://modules/core/INFRA-001-windows-runtime-baseline#password-storage
/// @spec spec://modules/core/FEAT-002-parent-password-override#password-setup
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 210_000;

    public static PasswordVerifier Create(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
        {
            throw new ArgumentException("Parent password must contain at least 4 characters.", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return new PasswordVerifier
        {
            Iterations = Iterations,
            SaltBase64 = Convert.ToBase64String(salt),
            HashBase64 = Convert.ToBase64String(hash)
        };
    }

    public static bool Verify(string password, PasswordVerifier? verifier)
    {
        if (verifier is null || string.IsNullOrEmpty(verifier.SaltBase64) || string.IsNullOrEmpty(verifier.HashBase64))
        {
            return false;
        }

        if (!string.Equals(verifier.Algorithm, "PBKDF2-SHA256", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(verifier.SaltBase64);
            var expected = Convert.FromBase64String(verifier.HashBase64);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Math.Max(100_000, verifier.Iterations),
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }
}
