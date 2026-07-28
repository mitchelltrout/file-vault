using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace FileVault.Service.Crypto;

public static class KeyDerivation
{
    // Production parameters (~1 second on modern hardware)
    public static readonly Argon2Params ProductionParams = new(
        DegreeOfParallelism: 4,
        MemorySize: 65536,   // 64 MB
        Iterations: 3);

    // Fast parameters for unit tests only
    public static readonly Argon2Params FastParams = new(
        DegreeOfParallelism: 1,
        MemorySize: 8192,
        Iterations: 1);

    public static VaultKey Derive(string password, byte[] salt, Argon2Params? @params = null)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(salt);

        var p = @params ?? ProductionParams;
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = p.DegreeOfParallelism,
                MemorySize = p.MemorySize,
                Iterations = p.Iterations
            };
            var keyBytes = argon2.GetBytes(32);
            return new VaultKey(keyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public static byte[] GenerateSalt() =>
        RandomNumberGenerator.GetBytes(32);
}

public record Argon2Params(int DegreeOfParallelism, int MemorySize, int Iterations);
