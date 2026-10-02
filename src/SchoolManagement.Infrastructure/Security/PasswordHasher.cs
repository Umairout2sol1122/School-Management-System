using System.Security.Cryptography;
using System.Text;
using SchoolManagement.Application.Common.Interfaces;

namespace SchoolManagement.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    private const int IterationCount = 100_000;
    private const int SaltByteLength = 32;
    private const int HashByteLength = 64;

    public (string Hash, string Salt) HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltByteLength);
        string salt = Convert.ToBase64String(saltBytes);

        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: saltBytes,
            iterations: IterationCount,
            hashAlgorithm: HashAlgorithmName.SHA512,
            outputLength: HashByteLength);

        string hash = Convert.ToBase64String(hashBytes);
        return (hash, salt);
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
            return false;

        try
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            byte[] expectedHashBytes = Convert.FromBase64String(hash);

            byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password: Encoding.UTF8.GetBytes(password),
                salt: saltBytes,
                iterations: IterationCount,
                hashAlgorithm: HashAlgorithmName.SHA512,
                outputLength: HashByteLength);

            return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
        }
        catch
        {
            return false;
        }
    }
}
